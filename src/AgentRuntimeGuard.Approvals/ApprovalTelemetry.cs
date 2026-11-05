using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgentRuntimeGuard.Approvals;

public static class ApprovalTelemetry
{
    public const string SourceName = "AgentRuntimeGuard.Approvals";

    public const string CompletedInstrumentName =
        "agent_runtime_guard.approval.completed";

    public const string DurationInstrumentName =
        "agent_runtime_guard.approval.duration";

    internal static readonly ActivitySource ActivitySource =
        new(SourceName);

    private static readonly Meter s_meter =
        new(SourceName);

    private static readonly Counter<long> s_completed =
        s_meter.CreateCounter<long>(
            CompletedInstrumentName,
            unit: "{approval}",
            description: "Completed approval waits by bounded outcome.");

    private static readonly Histogram<double> s_duration =
        s_meter.CreateHistogram<double>(
            DurationInstrumentName,
            unit: "ms",
            description: "Approval wait duration in milliseconds.");

    internal static Activity? StartActivity() =>
        ActivitySource.StartActivity(
            "approval.wait",
            ActivityKind.Internal);

    internal static void Record(
        string outcome,
        TimeSpan duration,
        Activity? activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        var tags = new TagList
        {
            { "outcome", outcome }
        };

        s_completed.Add(1, tags);
        s_duration.Record(
            duration.TotalMilliseconds,
            tags);

        activity?.SetTag(
            "approval.outcome",
            outcome);
    }
}
