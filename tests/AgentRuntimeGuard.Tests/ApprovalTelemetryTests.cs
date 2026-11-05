using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Approvals;

namespace AgentRuntimeGuard.Tests;

public sealed class ApprovalTelemetryTests
{
    [Fact]
    public async Task Approval_telemetry_uses_only_bounded_outcome_tags()
    {
        var measurements = new List<Measurement>();
        var stoppedActivities = new List<Activity>();

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name
                    == ApprovalTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(
                        instrument);
                }
            }
        };

        meterListener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
            {
                measurements.Add(
                    new Measurement(
                        instrument.Name,
                        measurement,
                        ReadTags(tags)));
            });

        meterListener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) =>
            {
                measurements.Add(
                    new Measurement(
                        instrument.Name,
                        measurement,
                        ReadTags(tags)));
            });

        meterListener.Start();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == ApprovalTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId =
                (ref ActivityCreationOptions<string> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
                stoppedActivities.Add(activity)
        };

        ActivitySource.AddActivityListener(
            activityListener);

        var request = new ApprovalRequest(
            Guid.NewGuid(),
            PolicyEffect.RequireApproval,
            "rule-with-private-context",
            "session-with-private-context",
            "sensitive_tool_name",
            "tools/call",
            "mcp:stdio/private-resource");

        await InvokeAsync(
            new StaticProvider(
                ApprovalOutcome.Approved),
            request);

        await InvokeAsync(
            new StaticProvider(
                ApprovalOutcome.Rejected),
            request);

        await InvokeAsync(
            new StaticProvider(
                ApprovalOutcome.Expired),
            request);

        await Assert.ThrowsAsync<IOException>(
            async () =>
                await InvokeAsync(
                    new ThrowingProvider(),
                    request));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await InvokeAsync(
                    new CancelledProvider(),
                    request,
                    cancellation.Token));

        var completed = measurements
            .Where(item =>
                item.InstrumentName
                == ApprovalTelemetry.CompletedInstrumentName)
            .ToArray();

        Assert.Equal(5, completed.Length);
        Assert.All(
            completed,
            item =>
            {
                Assert.Equal(1, item.Value);
                Assert.Single(item.Tags);
                Assert.True(
                    item.Tags.ContainsKey("outcome"));
            });

        Assert.Equal(
            [
                "approved",
                "rejected",
                "expired",
                "error",
                "cancelled"
            ],
            completed
                .Select(item =>
                    item.Tags["outcome"])
                .ToArray());

        var durations = measurements
            .Where(item =>
                item.InstrumentName
                == ApprovalTelemetry.DurationInstrumentName)
            .ToArray();

        Assert.Equal(5, durations.Length);
        Assert.All(
            durations,
            item =>
            {
                Assert.True(item.Value >= 0);
                Assert.Single(item.Tags);
                Assert.True(
                    item.Tags.ContainsKey("outcome"));
            });

        Assert.Equal(5, stoppedActivities.Count);

        foreach (var activity in stoppedActivities)
        {
            var tags = activity.Tags
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);

            Assert.Single(tags);
            Assert.True(
                tags.ContainsKey(
                    "approval.outcome"));

            Assert.DoesNotContain(
                "decisionId",
                tags.Keys,
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "sessionId",
                tags.Keys,
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "ruleId",
                tags.Keys,
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "toolName",
                tags.Keys,
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "resource",
                tags.Keys,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private static async Task<ApprovalOutcome> InvokeAsync(
        IApprovalProvider inner,
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider =
            new InstrumentedApprovalProvider(inner);

        return await provider.RequestApprovalAsync(
            request,
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> ReadTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        foreach (var pair in tags)
        {
            result[pair.Key] =
                pair.Value?.ToString()
                ?? string.Empty;
        }

        return result;
    }

    private sealed record Measurement(
        string InstrumentName,
        double Value,
        IReadOnlyDictionary<string, string> Tags);

    private sealed class StaticProvider(
        ApprovalOutcome outcome) : IApprovalProvider
    {
        public ValueTask<ApprovalOutcome> RequestApprovalAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(outcome);
        }
    }

    private sealed class ThrowingProvider : IApprovalProvider
    {
        public ValueTask<ApprovalOutcome> RequestApprovalAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default) =>
            throw new IOException(
                "Simulated provider failure.");
    }

    private sealed class CancelledProvider : IApprovalProvider
    {
        public ValueTask<ApprovalOutcome> RequestApprovalAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Expected cancellation.");
        }
    }
}
