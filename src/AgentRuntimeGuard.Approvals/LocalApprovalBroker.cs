using System.Collections.Concurrent;

namespace AgentRuntimeGuard.Approvals;

public sealed class LocalApprovalBroker : IApprovalProvider
{
    private readonly ConcurrentDictionary<Guid, PendingState> _pending = new();
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;

    public LocalApprovalBroker(
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Approval timeout must be greater than zero.");
        }

        _timeout = timeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<ApprovalOutcome> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var createdAtUtc = _timeProvider.GetUtcNow();
        var expiresAtUtc = createdAtUtc.Add(_timeout);
        var state = new PendingState(
            new PendingApproval(
                request.DecisionId,
                request.RuleId,
                request.SessionId,
                request.ToolName,
                request.Operation,
                request.Resource,
                createdAtUtc,
                expiresAtUtc));

        if (!_pending.TryAdd(request.DecisionId, state))
        {
            throw new InvalidOperationException(
                $"Approval decision '{request.DecisionId}' is already pending.");
        }

        try
        {
            var timeoutTask = Task.Delay(
                _timeout,
                _timeProvider,
                cancellationToken);

            var completed = await Task.WhenAny(
                state.Completion.Task,
                timeoutTask);

            if (completed == state.Completion.Task)
            {
                return await state.Completion.Task;
            }

            await timeoutTask;
            return ApprovalOutcome.Expired;
        }
        finally
        {
            _pending.TryRemove(request.DecisionId, out _);
        }
    }

    public IReadOnlyList<PendingApproval> GetPending()
    {
        ExpireStaleItems();

        return _pending.Values
            .Select(state => state.View)
            .OrderBy(item => item.CreatedAtUtc)
            .ToArray();
    }

    public bool TryApprove(Guid decisionId) =>
        TryResolve(decisionId, ApprovalOutcome.Approved);

    public bool TryReject(Guid decisionId) =>
        TryResolve(decisionId, ApprovalOutcome.Rejected);

    private bool TryResolve(
        Guid decisionId,
        ApprovalOutcome outcome)
    {
        if (!_pending.TryGetValue(decisionId, out var state))
        {
            return false;
        }

        if (_timeProvider.GetUtcNow() >= state.View.ExpiresAtUtc)
        {
            _pending.TryRemove(decisionId, out _);
            state.Completion.TrySetResult(ApprovalOutcome.Expired);
            return false;
        }

        return state.Completion.TrySetResult(outcome);
    }

    private void ExpireStaleItems()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var item in _pending)
        {
            if (now < item.Value.View.ExpiresAtUtc)
            {
                continue;
            }

            if (_pending.TryRemove(item.Key, out var removed))
            {
                removed.Completion.TrySetResult(ApprovalOutcome.Expired);
            }
        }
    }

    private sealed class PendingState(PendingApproval view)
    {
        public PendingApproval View { get; } = view;

        public TaskCompletionSource<ApprovalOutcome> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
