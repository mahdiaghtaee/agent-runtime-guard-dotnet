namespace AgentRuntimeGuard.Approvals;

public sealed record PendingApproval(
    Guid DecisionId,
    string? RuleId,
    string SessionId,
    string ToolName,
    string Operation,
    string Resource,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
