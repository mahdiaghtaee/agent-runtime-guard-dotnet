using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Core;
using AgentRuntimeGuard.McpProxy;

namespace AgentRuntimeGuard.Tests;

public sealed class McpStdioProxySessionTests
{
    [Fact]
    public async Task Allowed_tool_call_reaches_upstream_and_result_returns_to_client()
    {
        var evaluator = new PolicyEvaluator(
            Rules(
                Rule(
                    "allow-echo",
                    PolicyEffect.Allow,
                    "echo")));

        var session = CreateSession(evaluator);
        var clientInput = new LinePipe();
        var clientOutput = new LinePipe();
        var upstreamInput = new LinePipe();
        var upstreamOutput = new LinePipe();
        var received = new List<string>();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var proxyTask = session.RunAsync(
            clientInput.Reader,
            clientOutput.Writer,
            upstreamOutput.Reader,
            upstreamInput.Writer,
            cancellation.Token);

        var serverTask = RunFakeUpstreamAsync(
            upstreamInput,
            upstreamOutput,
            received,
            cancellation.Token);

        const string request =
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"echo","arguments":{"value":"hello"}}}""";

        await clientInput.WriteAsync(request, cancellation.Token);
        clientInput.CompleteWriter();

        await Task.WhenAll(proxyTask, serverTask);

        var response = await clientOutput.ReadAsync(cancellation.Token);

        Assert.Single(received);
        Assert.Equal(request, received[0]);

        using var responseDocument = JsonDocument.Parse(response);
        Assert.Equal(1, responseDocument.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(
            "called:echo",
            responseDocument.RootElement
                .GetProperty("result")
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString());
    }

    [Fact]
    public async Task Denied_tool_call_never_reaches_upstream()
    {
        var evaluator = new PolicyEvaluator(
            Rules(
                Rule(
                    "deny-delete",
                    PolicyEffect.Deny,
                    "dangerous_delete")));

        var session = CreateSession(evaluator);
        var clientInput = new LinePipe();
        var clientOutput = new LinePipe();
        var upstreamInput = new LinePipe();
        var upstreamOutput = new LinePipe();
        var received = new List<string>();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var proxyTask = session.RunAsync(
            clientInput.Reader,
            clientOutput.Writer,
            upstreamOutput.Reader,
            upstreamInput.Writer,
            cancellation.Token);

        var serverTask = RunFakeUpstreamAsync(
            upstreamInput,
            upstreamOutput,
            received,
            cancellation.Token);

        const string request =
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"dangerous_delete","arguments":{"secret":"top-secret"}}}""";

        await clientInput.WriteAsync(request, cancellation.Token);
        clientInput.CompleteWriter();

        await Task.WhenAll(proxyTask, serverTask);

        var response = await clientOutput.ReadAsync(cancellation.Token);

        Assert.Empty(received);
        Assert.DoesNotContain("top-secret", response, StringComparison.Ordinal);

        using var responseDocument = JsonDocument.Parse(response);
        var root = responseDocument.RootElement;

        Assert.Equal(2, root.GetProperty("id").GetInt32());
        Assert.Equal(
            McpToolCallPolicyGate.PolicyBlockedErrorCode,
            root.GetProperty("error").GetProperty("code").GetInt32());

        var data = root.GetProperty("error").GetProperty("data");
        Assert.Equal("Deny", data.GetProperty("effect").GetString());
        Assert.Equal("deny-delete", data.GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task Malformed_payload_fails_closed_without_forwarding()
    {
        var evaluator = new PolicyEvaluator(Rules());
        var session = CreateSession(evaluator);

        var clientInput = new LinePipe();
        var clientOutput = new LinePipe();
        var upstreamInput = new LinePipe();
        var upstreamOutput = new LinePipe();
        var received = new List<string>();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var proxyTask = session.RunAsync(
            clientInput.Reader,
            clientOutput.Writer,
            upstreamOutput.Reader,
            upstreamInput.Writer,
            cancellation.Token);

        var serverTask = RunFakeUpstreamAsync(
            upstreamInput,
            upstreamOutput,
            received,
            cancellation.Token);

        await clientInput.WriteAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":""",
            cancellation.Token);

        clientInput.CompleteWriter();

        await Task.WhenAll(proxyTask, serverTask);

        var response = await clientOutput.ReadAsync(cancellation.Token);

        Assert.Empty(received);

        using var responseDocument = JsonDocument.Parse(response);
        Assert.Equal(
            -32700,
            responseDocument.RootElement
                .GetProperty("error")
                .GetProperty("code")
                .GetInt32());
    }

    private static McpStdioProxySession CreateSession(
        IPolicyEvaluator evaluator) =>
        new(
            new McpToolCallPolicyGate(
                evaluator,
                "test-proxy-instance"));

    private static async Task RunFakeUpstreamAsync(
        LinePipe requests,
        LinePipe responses,
        ICollection<string> received,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await requests.Reader.ReadLineAsync(cancellationToken) is { } line)
            {
                received.Add(line);

                using var requestDocument = JsonDocument.Parse(line);
                var root = requestDocument.RootElement;

                if (!root.TryGetProperty("method", out var method)
                    || !string.Equals(
                        method.GetString(),
                        "tools/call",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var id = root.GetProperty("id").GetInt32();
                var toolName = root
                    .GetProperty("params")
                    .GetProperty("name")
                    .GetString();

                var response =
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"content\":[{{\"type\":\"text\",\"text\":\"called:{toolName}\"}}]}}}}";

                await responses.WriteAsync(
                    response,
                    cancellationToken);
            }
        }
        finally
        {
            responses.CompleteWriter();
        }
    }

    private static PolicyRuleSet Rules(
        params PolicyRule[] rules) =>
        new()
        {
            DefaultEffect = PolicyEffect.RequireApproval,
            DefaultReason = "Approval is required.",
            Rules = rules
        };

    private static PolicyRule Rule(
        string id,
        PolicyEffect effect,
        string toolPattern) =>
        new()
        {
            Id = id,
            Priority = 100,
            Effect = effect,
            ToolPattern = toolPattern,
            OperationPattern = "tools/call",
            ResourcePattern = "mcp:stdio",
            Reason = $"Matched {id}."
        };

    private sealed class LinePipe
    {
        private readonly Channel<string> _channel =
            Channel.CreateUnbounded<string>(
                new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });

        public LinePipe()
        {
            Reader = new ChannelLineReader(_channel.Reader);
            Writer = new ChannelLineWriter(_channel.Writer);
        }

        public TextReader Reader { get; }

        public TextWriter Writer { get; }

        public ValueTask WriteAsync(
            string line,
            CancellationToken cancellationToken) =>
            _channel.Writer.WriteAsync(
                line,
                cancellationToken);

        public ValueTask<string> ReadAsync(
            CancellationToken cancellationToken) =>
            _channel.Reader.ReadAsync(
                cancellationToken);

        public void CompleteWriter() =>
            _channel.Writer.TryComplete();
    }

    private sealed class ChannelLineReader(
        ChannelReader<string> reader) : TextReader
    {
        public override async ValueTask<string?> ReadLineAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                return await reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }
    }

    private sealed class ChannelLineWriter(
        ChannelWriter<string> writer) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteLineAsync(string? value) =>
            writer.WriteAsync(value ?? string.Empty).AsTask();

        public override Task FlushAsync() =>
            Task.CompletedTask;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.TryComplete();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            writer.TryComplete();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
