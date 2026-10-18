using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Receipts;

public sealed class RecordingPolicyEvaluator : IPolicyEvaluator
{
    private readonly IPolicyEvaluator _inner;
    private readonly IDecisionReceiptStore _store;

    public RecordingPolicyEvaluator(
        IPolicyEvaluator inner,
        IDecisionReceiptStore store)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask<PolicyEvaluationResult> EvaluateAsync(
        ToolActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var evaluation = await _inner.EvaluateAsync(
            request,
            cancellationToken);

        await _store.AppendAsync(
            DecisionReceipt.From(request, evaluation),
            cancellationToken);

        return evaluation;
    }
}
