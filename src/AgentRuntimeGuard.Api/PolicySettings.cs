using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Core;

namespace AgentRuntimeGuard.Api;

public sealed class PolicySettings
{
    public PolicyEffect DefaultEffect { get; set; } = PolicyEffect.RequireApproval;

    public string DefaultReason { get; set; } = "No policy rule matched the requested action.";

    public List<PolicyRule> Rules { get; set; } = [];

    public PolicyRuleSet ToRuleSet() =>
        new()
        {
            DefaultEffect = DefaultEffect,
            DefaultReason = DefaultReason,
            Rules = Rules
        };
}
