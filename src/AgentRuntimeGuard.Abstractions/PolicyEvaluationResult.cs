namespace AgentRuntimeGuard.Abstractions;

public sealed record PolicyEvaluationResult(
    Guid DecisionId,
    PolicyEffect Effect,
    string? RuleId,
    string Reason,
    DateTimeOffset EvaluatedAtUtc);
