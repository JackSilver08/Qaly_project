namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantQualityContract
{
    public const string SchemaId = "assistant_quality_evaluation.v1";
    public const string EvaluationSetVersion = "vi-native-eval@1.0.0";
}

public sealed record AiAssistantQualityEvaluationDto(
    string SchemaId,
    string EvaluationSetVersion,
    bool Passed,
    bool Useful,
    bool DeadEnd,
    bool FalseMutationSuccess,
    bool ProviderIdentityConsistent,
    bool QuestionLimitValid,
    bool GuidanceRoutesValid,
    IReadOnlyList<string> FailureCodes,
    int LatencyMs = 0,
    bool UsedFallback = false);

public sealed record AiAssistantQualityMetricsDto(
    string SchemaId,
    string EvaluationSetVersion,
    int EvaluatedTurns,
    int PassedTurns,
    int DeadEnds,
    int FalseMutationSuccesses,
    int ProviderIdentityMismatches,
    int FallbackTurns,
    double AverageLatencyMs,
    double PassRate,
    double DeadEndRate,
    DateTimeOffset GeneratedAt);
