using System.Text.Json;
using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Core;
using AgentRuntimeGuard.McpProxy;
using AgentRuntimeGuard.Receipts;

namespace AgentRuntimeGuard.Tests;

public sealed class McpReceiptIntegrationTests
{
    [Fact]
    public async Task Mcp_tool_call_is_recorded_after_policy_evaluation()
    {
        await using var database = new TempDatabase();
        var store = new SqliteDecisionReceiptStore(database.Path);
        await store.InitializeAsync();

        var evaluator = new RecordingPolicyEvaluator(
            new PolicyEvaluator(
                new PolicyRuleSet
                {
                    DefaultEffect = PolicyEffect.RequireApproval,
                    DefaultReason = "Approval required.",
                    Rules =
                    [
                        new PolicyRule
                        {
                            Id = "allow-echo",
                            Priority = 100,
                            Effect = PolicyEffect.Allow,
                            ToolPattern = "echo",
                            OperationPattern = "tools/call",
                            ResourcePattern = "mcp:stdio",
                            Reason = "Echo is allowed."
                        }
                    ]
                }),
            store);

        var gate = new McpToolCallPolicyGate(
            evaluator,
            "receipt-session");

        var decision = await gate.EvaluateAsync(
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"echo","arguments":{"secret":"not-persisted"}}}""",
            CancellationToken.None);

        Assert.True(decision.Forward);

        var receipts = await store.QueryBySessionAsync(
            "receipt-session",
            10);

        var receipt = Assert.Single(receipts);
        Assert.Equal(PolicyEffect.Allow, receipt.Effect);
        Assert.Equal("allow-echo", receipt.RuleId);
        Assert.Equal("echo", receipt.ToolName);
        Assert.Equal("tools/call", receipt.Operation);
        Assert.Equal("mcp:stdio", receipt.Resource);
    }

    [Fact]
    public async Task Receipt_query_command_outputs_bounded_json_lines()
    {
        await using var database = new TempDatabase();
        var store = new SqliteDecisionReceiptStore(database.Path);
        await store.InitializeAsync();

        await store.AppendAsync(
            new DecisionReceipt(
                Sequence: 0,
                DecisionId: Guid.NewGuid(),
                EvaluatedAtUtc: DateTimeOffset.Parse("2026-10-18T14:00:00+00:00"),
                Effect: PolicyEffect.Deny,
                RuleId: "deny-write",
                AgentId: "mcp-stdio-client",
                SessionId: "export-session",
                ToolName: "write_file",
                Operation: "tools/call",
                Resource: "mcp:stdio"));

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await McpProxyApplication.RunAsync(
            [
                "receipts",
                "--database",
                database.Path,
                "--session",
                "export-session",
                "--limit",
                "10"
            ],
            new StringReader(string.Empty),
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());

        var line = output.ToString().Trim();
        using var document = JsonDocument.Parse(line);

        var root = document.RootElement;
        Assert.Equal("Deny", root.GetProperty("Effect").GetString());
        Assert.Equal("write_file", root.GetProperty("ToolName").GetString());
        Assert.False(root.TryGetProperty("Reason", out _));
        Assert.False(root.TryGetProperty("Arguments", out _));
        Assert.False(root.TryGetProperty("Prompt", out _));
        Assert.False(root.TryGetProperty("Tags", out _));
    }

    [Fact]
    public void Proxy_receipts_are_disabled_when_option_is_absent()
    {
        var parsed = McpProxyCommandLine.TryParse(
            ["--policy", "policy.json", "--", "server"],
            out var options,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Null(options.ReceiptDatabasePath);
        Assert.Equal(10_000, options.MaxReceipts);
        Assert.Null(options.SessionId);
    }

    [Fact]
    public void Proxy_receipt_options_are_parsed_explicitly()
    {
        var parsed = McpProxyCommandLine.TryParse(
            [
                "--policy",
                "policy.json",
                "--receipts",
                ".guard/receipts.db",
                "--max-receipts",
                "250",
                "--session-id",
                "session-42",
                "--",
                "server",
                "--flag"
            ],
            out var options,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(".guard/receipts.db", options.ReceiptDatabasePath);
        Assert.Equal(250, options.MaxReceipts);
        Assert.Equal("session-42", options.SessionId);
        Assert.Equal("server", options.Command);
        Assert.Equal(["--flag"], options.CommandArguments);
    }

    private sealed class TempDatabase : IAsyncDisposable
    {
        private readonly string _directory =
            Path.Combine(
                Path.GetTempPath(),
                "agent-runtime-guard-tests",
                Guid.NewGuid().ToString("N"));

        public TempDatabase()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "receipts.db");
        }

        public string Path { get; }

        public ValueTask DisposeAsync()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
