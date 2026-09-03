using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Qaly.Application.Common.Telemetry;

public static class QalyAiTelemetry
{
    public const string ActivitySourceName = "Qaly.AI";
    public const string MeterName = "Qaly.AI";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> AssistantTurns = Meter.CreateCounter<long>(
        "qaly.ai.assistant.turns",
        unit: "{turn}",
        description: "Durably accepted assistant turns by terminal outcome.");
    private static readonly Histogram<double> TurnDuration = Meter.CreateHistogram<double>(
        "qaly.ai.assistant.turn.duration",
        unit: "s",
        description: "Service time from request entry through canonical terminal persistence.");
    private static readonly Histogram<double> FirstProgressDuration = Meter.CreateHistogram<double>(
        "qaly.ai.assistant.first_progress.duration",
        unit: "s",
        description: "Service time until the first durable progress event is available.");
    private static readonly Histogram<double> FirstAnswerDuration = Meter.CreateHistogram<double>(
        "qaly.ai.assistant.first_answer.duration",
        unit: "s",
        description: "Service time until a successful answer is durably available.");

    public static Activity? StartTurn(string? capabilityId, bool projectScoped)
    {
        var activity = ActivitySource.StartActivity("assistant.turn", ActivityKind.Internal);
        activity?.SetTag("qaly.ai.capability", NormalizeTag(capabilityId, "unplanned"));
        activity?.SetTag("qaly.ai.project_scoped", projectScoped);
        return activity;
    }

    public static void RecordFirstProgress(
        TimeSpan elapsed,
        string? capabilityId,
        bool projectScoped)
    {
        var tags = BaseTags(capabilityId, projectScoped);
        FirstProgressDuration.Record(NonNegativeSeconds(elapsed), tags);
    }

    public static void RecordTerminal(
        TimeSpan elapsed,
        bool answerAvailable,
        string outcome,
        string? capabilityId,
        string? actualProvider,
        bool usedFallback,
        bool projectScoped,
        string? safeErrorCode,
        Activity? activity)
    {
        var tags = BaseTags(capabilityId, projectScoped);
        tags.Add("qaly.ai.outcome", NormalizeOutcome(outcome));
        tags.Add("qaly.ai.provider", NormalizeProvider(actualProvider));
        tags.Add("qaly.ai.fallback", usedFallback);

        var seconds = NonNegativeSeconds(elapsed);
        AssistantTurns.Add(1, tags);
        TurnDuration.Record(seconds, tags);
        if (answerAvailable)
        {
            FirstAnswerDuration.Record(seconds, tags);
        }

        var normalizedOutcome = NormalizeOutcome(outcome);
        activity?.SetTag("qaly.ai.outcome", normalizedOutcome);
        activity?.SetTag("qaly.ai.provider", NormalizeProvider(actualProvider));
        activity?.SetTag("qaly.ai.fallback", usedFallback);
        if (!string.IsNullOrWhiteSpace(safeErrorCode))
        {
            activity?.SetTag("error.type", NormalizeTag(safeErrorCode, "assistant_error"));
        }
        activity?.SetStatus(
            normalizedOutcome == "completed" ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            normalizedOutcome == "completed" ? null : "assistant turn did not complete");
    }

    private static TagList BaseTags(string? capabilityId, bool projectScoped)
        => new()
        {
            { "qaly.ai.capability", NormalizeTag(capabilityId, "unplanned") },
            { "qaly.ai.project_scoped", projectScoped }
        };

    private static string NormalizeOutcome(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "completed" => "completed",
            "failed" => "failed",
            "canceled" => "canceled",
            "state_changed" => "state_changed",
            _ => "other"
        };

    private static string NormalizeProvider(string? value)
    {
        var provider = value?.Split('/', 2, StringSplitOptions.TrimEntries)[0];
        return NormalizeTag(provider, "not_reached");
    }

    private static string NormalizeTag(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 64 || normalized.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            return fallback;
        }

        return normalized;
    }

    private static double NonNegativeSeconds(TimeSpan elapsed)
        => Math.Max(0, elapsed.TotalSeconds);
}
