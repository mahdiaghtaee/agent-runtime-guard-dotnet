using System.Diagnostics;

namespace AgentRuntimeGuard.Approvals;

public sealed class InstrumentedApprovalProvider : IApprovalProvider
{
    private readonly IApprovalProvider _inner;

    public InstrumentedApprovalProvider(IApprovalProvider inner)
    {
        _inner = inner
            ?? throw new ArgumentNullException(nameof(inner));
    }

    public async ValueTask<ApprovalOutcome> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity =
            ApprovalTelemetry.StartActivity();

        var started = Stopwatch.GetTimestamp();

        try
        {
            var outcome =
                await _inner.RequestApprovalAsync(
                    request,
                    cancellationToken);

            Record(
                OutcomeName(outcome),
                started,
                activity);

            return outcome;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            Record(
                "cancelled",
                started,
                activity);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                "cancelled");

            throw;
        }
        catch
        {
            Record(
                "error",
                started,
                activity);

            activity?.SetStatus(
                ActivityStatusCode.Error);

            throw;
        }
    }

    private static void Record(
        string outcome,
        long started,
        Activity? activity) =>
        ApprovalTelemetry.Record(
            outcome,
            Stopwatch.GetElapsedTime(started),
            activity);

    private static string OutcomeName(
        ApprovalOutcome outcome) =>
        outcome switch
        {
            ApprovalOutcome.Approved => "approved",
            ApprovalOutcome.Rejected => "rejected",
            ApprovalOutcome.Expired => "expired",
            _ => "unknown"
        };
}
