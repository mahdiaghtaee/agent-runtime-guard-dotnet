namespace AgentRuntimeGuard.McpProxy;

internal sealed class McpStdioProxySession
{
    private readonly McpToolCallPolicyGate _gate;
    private readonly SemaphoreSlim _clientWriteLock = new(1, 1);

    public McpStdioProxySession(McpToolCallPolicyGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public async Task RunAsync(
        TextReader clientInput,
        TextWriter clientOutput,
        TextReader upstreamOutput,
        TextWriter upstreamInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientInput);
        ArgumentNullException.ThrowIfNull(clientOutput);
        ArgumentNullException.ThrowIfNull(upstreamOutput);
        ArgumentNullException.ThrowIfNull(upstreamInput);

        var clientToUpstream = PumpClientToUpstreamAsync(
            clientInput,
            clientOutput,
            upstreamInput,
            cancellationToken);

        var upstreamToClient = PumpUpstreamToClientAsync(
            upstreamOutput,
            clientOutput,
            cancellationToken);

        await Task.WhenAll(clientToUpstream, upstreamToClient);
    }

    private async Task PumpClientToUpstreamAsync(
        TextReader clientInput,
        TextWriter clientOutput,
        TextWriter upstreamInput,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await clientInput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var decision = await _gate.EvaluateAsync(
                    line,
                    cancellationToken);

                if (decision.Forward)
                {
                    await upstreamInput.WriteLineAsync(line);
                    await upstreamInput.FlushAsync();
                    continue;
                }

                if (decision.ClientResponse is not null)
                {
                    await WriteClientLineAsync(
                        clientOutput,
                        decision.ClientResponse,
                        cancellationToken);
                }
            }
        }
        finally
        {
            await upstreamInput.DisposeAsync();
        }
    }

    private async Task PumpUpstreamToClientAsync(
        TextReader upstreamOutput,
        TextWriter clientOutput,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await upstreamOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            await WriteClientLineAsync(
                clientOutput,
                line,
                cancellationToken);
        }
    }

    private async Task WriteClientLineAsync(
        TextWriter clientOutput,
        string line,
        CancellationToken cancellationToken)
    {
        await _clientWriteLock.WaitAsync(cancellationToken);
        try
        {
            await clientOutput.WriteLineAsync(line);
            await clientOutput.FlushAsync();
        }
        finally
        {
            _clientWriteLock.Release();
        }
    }
}
