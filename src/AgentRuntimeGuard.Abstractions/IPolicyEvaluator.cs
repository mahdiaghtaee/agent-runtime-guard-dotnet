namespace AgentRuntimeGuard.Abstractions;

public interface IPolicyEvaluator
{
    ValueTask<PolicyEvaluationResult> EvaluateAsync(
        ToolActionRequest request,
        CancellationToken cancellationToken = default);
}
