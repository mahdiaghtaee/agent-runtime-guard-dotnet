using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Core;

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    private readonly PolicyRuleSet _ruleSet;
    private readonly TimeProvider _timeProvider;

    public PolicyEvaluator(PolicyRuleSet ruleSet, TimeProvider? timeProvider = null)
    {
        _ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ValueTask<PolicyEvaluationResult> EvaluateAsync(
        ToolActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var rule = _ruleSet.Rules
            .Where(candidate => Matches(candidate, request))
            .OrderByDescending(candidate => candidate.Priority)
            .ThenByDescending(candidate => candidate.Effect)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        var result = rule is null
            ? new PolicyEvaluationResult(
                Guid.NewGuid(),
                _ruleSet.DefaultEffect,
                null,
                _ruleSet.DefaultReason,
                _timeProvider.GetUtcNow())
            : new PolicyEvaluationResult(
                Guid.NewGuid(),
                rule.Effect,
                rule.Id,
                string.IsNullOrWhiteSpace(rule.Reason)
                    ? $"Matched policy rule '{rule.Id}'."
                    : rule.Reason,
                _timeProvider.GetUtcNow());

        return ValueTask.FromResult(result);
    }

    private static bool Matches(PolicyRule rule, ToolActionRequest request) =>
        WildcardMatcher.IsMatch(rule.ToolPattern, request.ToolName)
        && WildcardMatcher.IsMatch(rule.OperationPattern, request.Operation)
        && WildcardMatcher.IsMatch(rule.ResourcePattern, request.Resource);
}
