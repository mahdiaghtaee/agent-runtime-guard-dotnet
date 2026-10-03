using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Core;

namespace AgentRuntimeGuard.Tests;

public sealed class PolicyEvaluatorTests
{
    [Fact]
    public async Task Returns_default_when_no_rule_matches()
    {
        var evaluator = new PolicyEvaluator(
            new PolicyRuleSet
            {
                DefaultEffect = PolicyEffect.RequireApproval,
                DefaultReason = "No rule matched."
            });

        var result = await evaluator.EvaluateAsync(
            Request("filesystem", "write", "notes.txt"));

        Assert.Equal(PolicyEffect.RequireApproval, result.Effect);
        Assert.Null(result.RuleId);
        Assert.Equal("No rule matched.", result.Reason);
    }

    [Fact]
    public async Task Higher_priority_rule_wins()
    {
        var evaluator = new PolicyEvaluator(
            Rules(
                Rule("allow-shell", 10, PolicyEffect.Allow, "shell", "execute", "*"),
                Rule("deny-production", 100, PolicyEffect.Deny, "shell", "execute", "production:*")));

        var result = await evaluator.EvaluateAsync(
            Request("shell", "execute", "production:web-01"));

        Assert.Equal(PolicyEffect.Deny, result.Effect);
        Assert.Equal("deny-production", result.RuleId);
    }

    [Fact]
    public async Task More_restrictive_effect_wins_when_priorities_are_equal()
    {
        var evaluator = new PolicyEvaluator(
            Rules(
                Rule("allow", 100, PolicyEffect.Allow, "database", "write", "*"),
                Rule("approval", 100, PolicyEffect.RequireApproval, "database", "write", "*")));

        var result = await evaluator.EvaluateAsync(
            Request("database", "write", "orders"));

        Assert.Equal(PolicyEffect.RequireApproval, result.Effect);
        Assert.Equal("approval", result.RuleId);
    }

    [Fact]
    public async Task Wildcard_matching_is_case_insensitive()
    {
        var evaluator = new PolicyEvaluator(
            Rules(
                Rule("read-source", 100, PolicyEffect.Allow, "FileSystem", "read", "src/*")));

        var result = await evaluator.EvaluateAsync(
            Request("filesystem", "READ", "SRC/Program.cs"));

        Assert.Equal(PolicyEffect.Allow, result.Effect);
        Assert.Equal("read-source", result.RuleId);
    }

    [Fact]
    public async Task Cancellation_is_observed_before_evaluation()
    {
        var evaluator = new PolicyEvaluator(Rules());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await evaluator.EvaluateAsync(
                Request("filesystem", "read", "README.md"),
                cancellation.Token));
    }

    private static ToolActionRequest Request(
        string tool,
        string operation,
        string resource) =>
        new()
        {
            AgentId = "test-agent",
            SessionId = "test-session",
            ToolName = tool,
            Operation = operation,
            Resource = resource
        };

    private static PolicyRuleSet Rules(params PolicyRule[] rules) =>
        new()
        {
            DefaultEffect = PolicyEffect.RequireApproval,
            Rules = rules
        };

    private static PolicyRule Rule(
        string id,
        int priority,
        PolicyEffect effect,
        string tool,
        string operation,
        string resource) =>
        new()
        {
            Id = id,
            Priority = priority,
            Effect = effect,
            ToolPattern = tool,
            OperationPattern = operation,
            ResourcePattern = resource,
            Reason = $"Matched {id}."
        };
}
