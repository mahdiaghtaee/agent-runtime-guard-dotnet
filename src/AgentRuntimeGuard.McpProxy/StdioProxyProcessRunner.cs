using System.Diagnostics;

namespace AgentRuntimeGuard.McpProxy;

internal sealed class StdioProxyProcessRunner
{
    public async Task<int> RunAsync(
        McpStdioProxySession session,
        string command,
        IReadOnlyList<string> commandArguments,
        TextReader clientInput,
        TextWriter clientOutput,
        TextWriter errorOutput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(commandArguments);

        var startInfo = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in commandArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Failed to start upstream MCP server '{command}'.");
        }

        using var sessionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var sessionTask = session.RunAsync(
            clientInput,
            clientOutput,
            process.StandardOutput,
            process.StandardInput,
            sessionCancellation.Token);

        var errorTask = RelayStandardErrorAsync(
            process.StandardError,
            errorOutput,
            sessionCancellation.Token);

        var exitTask = process.WaitForExitAsync(cancellationToken);
        var firstCompleted = await Task.WhenAny(sessionTask, exitTask);

        if (firstCompleted == exitTask && !sessionTask.IsCompleted)
        {
            sessionCancellation.Cancel();
        }

        try
        {
            await sessionTask;
        }
        catch (OperationCanceledException)
            when (sessionCancellation.IsCancellationRequested)
        {
        }

        if (!process.HasExited)
        {
            await process.WaitForExitAsync(cancellationToken);
        }

        sessionCancellation.Cancel();

        try
        {
            await errorTask;
        }
        catch (OperationCanceledException)
            when (sessionCancellation.IsCancellationRequested)
        {
        }

        return process.ExitCode;
    }

    private static async Task RelayStandardErrorAsync(
        TextReader upstreamError,
        TextWriter errorOutput,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await upstreamError.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            await errorOutput.WriteLineAsync($"[upstream] {line}");
            await errorOutput.FlushAsync();
        }
    }
}
