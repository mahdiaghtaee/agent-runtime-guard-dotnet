using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Receipts;

public sealed record DecisionReceipt(
    long Sequence,
    Guid DecisionId,
    DateTimeOffset EvaluatedAtUtc,
    PolicyEffect Effect,
    string? RuleId,
    string AgentId,
    string SessionId,
    string ToolName,
    string Operation,
    string Resource)
{
    public const int AgentIdMaxLength = 128;
    public const int SessionIdMaxLength = 128;
    public const int ToolNameMaxLength = 256;
    public const int OperationMaxLength = 128;
    public const int ResourceMaxLength = 512;
    public const int RuleIdMaxLength = 128;

    public static DecisionReceipt From(
        ToolActionRequest request,
        PolicyEvaluationResult evaluation) =>
        new(
            Sequence: 0,
            DecisionId: evaluation.DecisionId,
            EvaluatedAtUtc: evaluation.EvaluatedAtUtc,
            Effect: evaluation.Effect,
            RuleId: ClipNullable(evaluation.RuleId, RuleIdMaxLength),
            AgentId: Clip(request.AgentId, AgentIdMaxLength),
            SessionId: Clip(request.SessionId, SessionIdMaxLength),
            ToolName: Clip(request.ToolName, ToolNameMaxLength),
            Operation: Clip(request.Operation, OperationMaxLength),
            Resource: Clip(request.Resource, ResourceMaxLength));

    private static string Clip(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? ClipNullable(string? value, int maxLength) =>
        value is null ? null : Clip(value, maxLength);
}
