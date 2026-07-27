using System.Text.Json;

namespace Qaly.Application.DTOs.Ai;

public record AiJobSourceInputDto(
    string SourceType,
    Guid? SourceEntityId,
    string? LegacySourceKey,
    string? SourceVersion,
    string? SourceHash,
    DateTimeOffset? SourceTimestamp = null);

public record CreateAiJobDto(
    string JobType,
    Guid? ProjectId,
    string SourceType,
    string? SourceId,
    string ProviderHint = "auto",
    bool Sensitive = false,
    string? SourceText = null,
    IReadOnlyList<AiJobSourceInputDto>? Sources = null,
    string? SchemaId = null,
    string SchemaVersion = "4.0",
    string? SourceVersion = null,
    string? SourceHash = null,
    Guid? ConsentId = null,
    Guid? RetentionPolicyId = null,
    decimal? MaximumEstimatedCostUsd = null,
    string CacheMode = "use",
    string Language = "vi",
    JsonElement? Options = null);

public record AiJobCreatedDto(
    Guid JobId,
    string Status,
    decimal EstimatedCostUsd,
    string CacheKey,
    Guid? DraftId,
    string? PollUrl = null,
    string? ResultUrl = null,
    string? RequestId = null);

public record AiJobSourceDto(
    string SourceType,
    Guid? SourceEntityId,
    string? LegacySourceKey,
    string? SourceVersion,
    string? SourceHash,
    DateTimeOffset? SourceTimestamp);

public record AiJobSummaryDto(
    Guid JobId,
    string JobType,
    Guid? ProjectId,
    string Status,
    int ProgressPercent,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? LastErrorCode,
    bool IsMock,
    IReadOnlyList<Guid> DraftIds,
    string? ScopeSourceType = null,
    Guid? ScopeSourceEntityId = null);

public record AiJobDetailDto(
    Guid JobId,
    string JobType,
    Guid? TenantId,
    Guid? ProjectId,
    Guid RequestedById,
    string Status,
    int ProgressPercent,
    string SchemaId,
    string SchemaVersion,
    bool Sensitive,
    bool CloudEligible,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset AvailableAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? CanceledAt,
    string? LastErrorCode,
    string? LastErrorMessage,
    bool LastErrorRetryable,
    string? SelectedProvider,
    string? SelectedModel,
    decimal EstimatedCostUsd,
    decimal? ActualCostUsd,
    bool CacheHit,
    bool IsMock,
    string? MockReason,
    IReadOnlyList<AiJobSourceDto> Sources,
    IReadOnlyList<Guid> DraftIds,
    string RowVersion);

public record AiJobResultDto(
    Guid JobId,
    string SchemaId,
    string SchemaVersion,
    JsonElement Result,
    string? ResultHash,
    IReadOnlyList<Guid> DraftIds,
    IReadOnlyList<AiJobSourceDto> Sources,
    Guid? UsageLedgerId,
    bool CacheHit,
    bool IsMock,
    string? MockReason,
    bool SourceStale = false);

public record RetryAiJobDto(string? ProviderOverride = null);

public record CancelAiJobDto(string? Reason = null);

public record AiFunctionJobRequest(
    Guid? ProjectId = null,
    string? SourceType = null,
    Guid? SourceEntityId = null,
    string? LegacySourceKey = null,
    string? SourceVersion = null,
    string? SourceHash = null,
    string? SourceText = null,
    IReadOnlyList<AiJobSourceInputDto>? Sources = null,
    string ProviderHint = "auto",
    bool Sensitive = false,
    Guid? ConsentId = null,
    Guid? RetentionPolicyId = null,
    decimal? MaximumEstimatedCostUsd = null,
    string CacheMode = "use",
    string Language = "vi",
    JsonElement? Options = null);

public record ProjectProgressSummaryRequestDto(
    string Period = "current_snapshot",
    string Language = "vi",
    string ProviderHint = "auto",
    decimal? MaximumEstimatedCostUsd = null,
    string CacheMode = "use");

public record TaskSkillSuggestionRequestDto(
    string Language = "vi",
    string ProviderHint = "auto",
    decimal? MaximumEstimatedCostUsd = null,
    string CacheMode = "use");

public record ConfirmAiDraftDto(
    string? EditedPayloadJson,
    string ConfirmAction,
    string? ConfirmationNote,
    string? RowVersion = null,
    string? IdempotencyKey = null);

public record PatchAiDraftDto(
    string WorkingPayloadJson,
    string RowVersion);

public record RejectAiDraftDto(
    string Reason,
    string RowVersion,
    string? IdempotencyKey = null);

public record AiDraftSummaryDto(
    Guid DraftId,
    Guid AiJobId,
    Guid ProjectId,
    string DraftType,
    string Status,
    decimal? Confidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public record AiDraftDetailDto(
    Guid DraftId,
    Guid AiJobId,
    Guid ProjectId,
    string DraftType,
    string Status,
    JsonElement OriginalPayload,
    JsonElement WorkingPayload,
    JsonElement? Warnings,
    string? SchemaId,
    decimal? Confidence,
    IReadOnlyList<AiJobSourceDto> Sources,
    string RowVersion);

public record AiDraftConfirmResultDto(
    Guid DraftId,
    string Status,
    string ConfirmAction,
    int CreatedTaskCount,
    IReadOnlyList<Guid> CreatedTaskIds,
    int AppliedSkillCount = 0);

public record AiPlatformHealthDto(
    string Status,
    bool PlatformEnabled,
    bool WorkerEnabled,
    int QueueDepth,
    int RunningCount,
    int RetryCount,
    int FailedLast24Hours,
    int ExpiredLeaseCount,
    double? OldestQueuedAgeSeconds,
    string? DegradedReason,
    DateTimeOffset CheckedAt);

public record AiBudgetScopeDto(
    string ScopeType,
    Guid ScopeId,
    Guid? OrganizationId,
    Guid? ProjectId,
    string Name);

public record AiUsageBreakdownDto(
    string Key,
    int AttemptCount,
    int SucceededCount,
    int FailedCount,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    decimal EffectiveCostUsd,
    int CacheHitCount);

public record AiUsageSnapshotDto(
    string ScopeType,
    Guid ScopeId,
    Guid? OrganizationId,
    Guid? ProjectId,
    string ScopeName,
    DateTimeOffset From,
    DateTimeOffset To,
    int AttemptCount,
    int SucceededCount,
    int FailedCount,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    decimal EffectiveCostUsd,
    int ActualCostCount,
    int EstimatedOnlyCount,
    int CacheHitCount,
    IReadOnlyList<AiUsageBreakdownDto> Daily,
    IReadOnlyList<AiUsageBreakdownDto> ByProvider,
    IReadOnlyList<AiUsageBreakdownDto> ByFunction,
    IReadOnlyList<AiUsageBreakdownDto> ByStatus,
    IReadOnlyList<AiUsageBreakdownDto> ByCache,
    DateTimeOffset CalculatedAt);

public record AiBudgetSnapshotDto(
    string ScopeType,
    Guid ScopeId,
    Guid? OrganizationId,
    Guid? ProjectId,
    string ScopeName,
    Guid? PolicyId,
    Guid? EffectivePolicyId,
    string PolicySource,
    bool IsInherited,
    bool HasEffectivePolicy,
    bool CanEdit,
    bool EditingEnabled,
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    int WarningAtPercent,
    bool HardStopEnabled,
    decimal DailyUsageUsd,
    decimal MonthlyUsageUsd,
    decimal DailyRemainingUsd,
    decimal MonthlyRemainingUsd,
    bool WarningActive,
    bool HardStopActive,
    bool AllowCloudForSensitive,
    string? Version,
    string? EffectiveVersion,
    DateTimeOffset CalculatedAt);

public record UpdateAiBudgetPolicyDto(
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    int WarningAtPercent,
    bool HardStopEnabled,
    bool AllowCloudForSensitive,
    string? Version = null,
    bool Confirmed = false);

public record AiTaskDraftPayload(IReadOnlyList<AiTaskDraftItem> Tasks);

public record AiTaskDraftItem(
    string Title,
    string? Description,
    string Priority = "Medium",
    string Status = "Todo",
    DateTimeOffset? DueDate = null,
    Guid? AssigneeId = null);
