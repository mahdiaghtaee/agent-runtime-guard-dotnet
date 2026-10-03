using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Core;

public sealed class PolicyRuleSet
{
    public PolicyEffect DefaultEffect { get; init; } = PolicyEffect.RequireApproval;

    public string DefaultReason { get; init; } = "No policy rule matched the requested action.";

    public IReadOnlyList<PolicyRule> Rules { get; init; } = Array.Empty<PolicyRule>();
}
