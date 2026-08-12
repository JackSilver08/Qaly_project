namespace Qaly.Application.DTOs.Ai;

public static class AiSafeTestOrchestratorContract
{
    public const string CapabilityId = "demo.test.run.v1";
    public const string PreviewSchemaId = "safe_test_run_preview.v1";
    public const string ReportSchemaId = "safe_test_run_report.v1";
    public const string RendererId = "safe-test-run-review.v1";
    public const string DefaultManifestId = "ai-native-acceptance.v1";
}

public sealed record AiSafeTestSuiteDto(
    string Id,
    string Label,
    string Project,
    string Scope,
    int EstimatedSeconds);

public sealed record AiSafeTestRunEventDto(
    int Sequence,
    string SuiteId,
    string Status,
    string PublicLabel,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt = null,
    int? ExitCode = null,
    string? SafeErrorCode = null);

public sealed record AiSafeTestRunPreviewDto(
    string SchemaId,
    Guid RunId,
    string ManifestId,
    string Title,
    string Status,
    bool RequiresConfirmation,
    int EstimatedSeconds,
    decimal EstimatedExternalCost,
    IReadOnlyList<AiSafeTestSuiteDto> Suites,
    long Revision);

public sealed record AiSafeTestRunReportDto(
    string SchemaId,
    Guid RunId,
    string ManifestId,
    string Status,
    IReadOnlyList<AiSafeTestRunEventDto> Events,
    int PassedSuites,
    int FailedSuites,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? SafeSummary,
    string? SafeErrorCode,
    long Revision);

public sealed record ConfirmAiSafeTestRunRequestDto(long ExpectedRevision);
