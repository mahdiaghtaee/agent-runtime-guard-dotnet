namespace AgentRuntimeGuard.Receipts;

public interface IDecisionReceiptStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(
        DecisionReceipt receipt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DecisionReceipt>> QueryBySessionAsync(
        string sessionId,
        int limit,
        CancellationToken cancellationToken = default);
}
