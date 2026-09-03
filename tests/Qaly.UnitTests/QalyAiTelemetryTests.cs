using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Qaly.Application.Common.Telemetry;

namespace Qaly.UnitTests;

public sealed class QalyAiTelemetryTests
{
    [Fact]
    public void AssistantTurnMetrics_ExposeOnlyBoundedOperationalTags()
    {
        var measurements = new List<Measurement>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == QalyAiTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, Tags(tags))));
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, Tags(tags))));
        meterListener.Start();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == QalyAiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        using var activity = QalyAiTelemetry.StartTurn("unsafe capability with spaces", projectScoped: true);
        QalyAiTelemetry.RecordFirstProgress(
            TimeSpan.FromMilliseconds(250),
            "unsafe capability with spaces",
            projectScoped: true);
        QalyAiTelemetry.RecordTerminal(
            TimeSpan.FromSeconds(2),
            answerAvailable: true,
            outcome: "completed",
            capabilityId: "unsafe capability with spaces",
            actualProvider: "DeepSeek / deepseek-chat",
            usedFallback: false,
            projectScoped: true,
            safeErrorCode: null,
            activity);

        measurements.Should().ContainSingle(item =>
            item.Name == "qaly.ai.assistant.turns" && item.Value == 1);
        measurements.Should().ContainSingle(item =>
            item.Name == "qaly.ai.assistant.first_progress.duration" && item.Value == 0.25);
        measurements.Should().ContainSingle(item =>
            item.Name == "qaly.ai.assistant.first_answer.duration" && item.Value == 2);
        measurements.Should().ContainSingle(item =>
            item.Name == "qaly.ai.assistant.turn.duration" && item.Value == 2);

        var terminal = measurements.Single(item => item.Name == "qaly.ai.assistant.turns");
        terminal.Tags.Should().Contain("qaly.ai.capability", "unplanned");
        terminal.Tags.Should().Contain("qaly.ai.provider", "deepseek");
        terminal.Tags.Should().Contain("qaly.ai.outcome", "completed");
        terminal.Tags.Should().Contain("qaly.ai.fallback", false);
        terminal.Tags.Should().Contain("qaly.ai.project_scoped", true);
        terminal.Tags.Keys.Should().BeSubsetOf(
        [
            "qaly.ai.capability",
            "qaly.ai.provider",
            "qaly.ai.outcome",
            "qaly.ai.fallback",
            "qaly.ai.project_scoped"
        ]);

        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("qaly.ai.capability").Should().Be("unplanned");
        activity.GetTagItem("qaly.ai.provider").Should().Be("deepseek");
    }

    private static Dictionary<string, object?> Tags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => tags.ToArray().ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private sealed record Measurement(
        string Name,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);
}
