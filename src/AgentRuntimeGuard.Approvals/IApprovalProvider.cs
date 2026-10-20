namespace AgentRuntimeGuard.Approvals;

public interface IApprovalProvider
{
    ValueTask<ApprovalOutcome> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}
