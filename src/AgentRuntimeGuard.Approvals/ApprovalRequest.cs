using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Approvals;

public sealed record ApprovalRequest(
    Guid DecisionId,
    PolicyEffect Effect,
    string? RuleId,
    string SessionId,
    string ToolName,
    string Operation,
    string Resource)
{
    public const int RuleIdMaxLength = 128;
    public const int SessionIdMaxLength = 128;
    public const int ToolNameMaxLength = 256;
    public const int OperationMaxLength = 128;
    public const int ResourceMaxLength = 512;

    public static ApprovalRequest From(
        ToolActionRequest request,
        PolicyEvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(evaluation);

        return new ApprovalRequest(
            evaluation.DecisionId,
            evaluation.Effect,
            ClipNullable(evaluation.RuleId, RuleIdMaxLength),
            Clip(request.SessionId, SessionIdMaxLength),
            Clip(request.ToolName, ToolNameMaxLength),
            Clip(request.Operation, OperationMaxLength),
            Clip(request.Resource, ResourceMaxLength));
    }

    private static string Clip(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? ClipNullable(string? value, int maxLength) =>
        value is null ? null : Clip(value, maxLength);
}
