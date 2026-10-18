using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Core;
using AgentRuntimeGuard.Receipts;
using Microsoft.Data.Sqlite;

namespace AgentRuntimeGuard.Tests;

public sealed class DecisionReceiptStoreTests
{
    [Fact]
    public async Task Recording_evaluator_persists_all_policy_effects_by_session()
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
                        Rule("allow-read", PolicyEffect.Allow, "read_file"),
                        Rule("deny-delete", PolicyEffect.Deny, "delete_file")
                    ]
                }),
            store);

        await evaluator.EvaluateAsync(Request("session-a", "read_file"));
        await evaluator.EvaluateAsync(Request("session-a", "delete_file"));
        await evaluator.EvaluateAsync(Request("session-a", "unknown_tool"));

        var receipts = await store.QueryBySessionAsync("session-a", 10);

        Assert.Equal(3, receipts.Count);
        Assert.Equal(PolicyEffect.RequireApproval, receipts[0].Effect);
        Assert.Equal(PolicyEffect.Deny, receipts[1].Effect);
        Assert.Equal(PolicyEffect.Allow, receipts[2].Effect);
        Assert.Equal("deny-delete", receipts[1].RuleId);
        Assert.Equal("read_file", receipts[2].ToolName);
        Assert.All(receipts, receipt => Assert.Equal("session-a", receipt.SessionId));
    }

    [Fact]
    public async Task Retention_keeps_only_the_newest_receipts()
    {
        await using var database = new TempDatabase();
        var store = new SqliteDecisionReceiptStore(
            database.Path,
            maxReceipts: 3);

        await store.InitializeAsync();

        var ids = new List<Guid>();

        for (var index = 0; index < 5; index++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);

            await store.AppendAsync(
                new DecisionReceipt(
                    Sequence: 0,
                    DecisionId: id,
                    EvaluatedAtUtc: DateTimeOffset.Parse(
                        $"2026-10-18T10:00:0{index}+00:00"),
                    Effect: PolicyEffect.Allow,
                    RuleId: "allow",
                    AgentId: "test-agent",
                    SessionId: "session-retention",
                    ToolName: $"tool-{index}",
                    Operation: "tools/call",
                    Resource: "mcp:stdio"));
        }

        var receipts = await store.QueryBySessionAsync(
            "session-retention",
            10);

        Assert.Equal(3, receipts.Count);
        Assert.Equal(ids[4], receipts[0].DecisionId);
        Assert.Equal(ids[3], receipts[1].DecisionId);
        Assert.Equal(ids[2], receipts[2].DecisionId);
    }

    [Fact]
    public async Task Query_is_scoped_to_the_requested_session()
    {
        await using var database = new TempDatabase();
        var store = new SqliteDecisionReceiptStore(database.Path);
        await store.InitializeAsync();

        await store.AppendAsync(Receipt("session-a", "tool-a"));
        await store.AppendAsync(Receipt("session-b", "tool-b"));
        await store.AppendAsync(Receipt("session-a", "tool-c"));

        var receipts = await store.QueryBySessionAsync("session-a", 10);

        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, receipt => Assert.Equal("session-a", receipt.SessionId));
        Assert.Equal("tool-c", receipts[0].ToolName);
        Assert.Equal("tool-a", receipts[1].ToolName);
    }

    [Fact]
    public async Task Receipt_schema_excludes_sensitive_payload_columns()
    {
        await using var database = new TempDatabase();
        var store = new SqliteDecisionReceiptStore(database.Path);
        await store.InitializeAsync();

        var columns = new List<string>();

        await using var connection = new SqliteConnection(
            $"Data Source={database.Path}");

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(decision_receipts);";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains("decision_id", columns);
        Assert.Contains("session_id", columns);
        Assert.Contains("tool_name", columns);
        Assert.DoesNotContain("prompt", columns);
        Assert.DoesNotContain("arguments", columns);
        Assert.DoesNotContain("reason", columns);
        Assert.DoesNotContain("tags", columns);
        Assert.Equal(10, columns.Count);
    }

    [Fact]
    public void Receipt_factory_bounds_stored_metadata()
    {
        var request = new ToolActionRequest
        {
            AgentId = new string('a', DecisionReceipt.AgentIdMaxLength + 20),
            SessionId = new string('s', DecisionReceipt.SessionIdMaxLength + 20),
            ToolName = new string('t', DecisionReceipt.ToolNameMaxLength + 20),
            Operation = new string('o', DecisionReceipt.OperationMaxLength + 20),
            Resource = new string('r', DecisionReceipt.ResourceMaxLength + 20)
        };

        var receipt = DecisionReceipt.From(
            request,
            new PolicyEvaluationResult(
                Guid.NewGuid(),
                PolicyEffect.Deny,
                new string('x', DecisionReceipt.RuleIdMaxLength + 20),
                "This reason is deliberately not part of the receipt.",
                DateTimeOffset.UtcNow));

        Assert.Equal(DecisionReceipt.AgentIdMaxLength, receipt.AgentId.Length);
        Assert.Equal(DecisionReceipt.SessionIdMaxLength, receipt.SessionId.Length);
        Assert.Equal(DecisionReceipt.ToolNameMaxLength, receipt.ToolName.Length);
        Assert.Equal(DecisionReceipt.OperationMaxLength, receipt.Operation.Length);
        Assert.Equal(DecisionReceipt.ResourceMaxLength, receipt.Resource.Length);
        Assert.Equal(DecisionReceipt.RuleIdMaxLength, receipt.RuleId!.Length);
    }

    [Fact]
    public async Task Persistence_failure_prevents_returning_an_unrecorded_decision()
    {
        var evaluator = new RecordingPolicyEvaluator(
            new PolicyEvaluator(
                new PolicyRuleSet
                {
                    DefaultEffect = PolicyEffect.Allow
                }),
            new FailingReceiptStore());

        await Assert.ThrowsAsync<IOException>(
            async () => await evaluator.EvaluateAsync(
                Request("session-failure", "read_file")));
    }

    private static PolicyRule Rule(
        string id,
        PolicyEffect effect,
        string toolName) =>
        new()
        {
            Id = id,
            Priority = 100,
            Effect = effect,
            ToolPattern = toolName,
            OperationPattern = "tools/call",
            ResourcePattern = "mcp:stdio",
            Reason = $"Matched {id}."
        };

    private static ToolActionRequest Request(
        string sessionId,
        string toolName) =>
        new()
        {
            AgentId = "mcp-stdio-client",
            SessionId = sessionId,
            ToolName = toolName,
            Operation = "tools/call",
            Resource = "mcp:stdio",
            Tags = ["mcp", "stdio"]
        };

    private static DecisionReceipt Receipt(
        string sessionId,
        string toolName) =>
        new(
            Sequence: 0,
            DecisionId: Guid.NewGuid(),
            EvaluatedAtUtc: DateTimeOffset.UtcNow,
            Effect: PolicyEffect.Allow,
            RuleId: "allow",
            AgentId: "test-agent",
            SessionId: sessionId,
            ToolName: toolName,
            Operation: "tools/call",
            Resource: "mcp:stdio");

    private sealed class FailingReceiptStore : IDecisionReceiptStore
    {
        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AppendAsync(
            DecisionReceipt receipt,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated receipt-store failure.");

        public Task<IReadOnlyList<DecisionReceipt>> QueryBySessionAsync(
            string sessionId,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DecisionReceipt>>([]);
    }

    private sealed class TempDatabase : IAsyncDisposable
    {
        private readonly string _directory =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
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
