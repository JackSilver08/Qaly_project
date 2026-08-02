using System.Text.Json;

namespace Qaly.Application.DTOs.Ai;

public static class AiActionComposerContract
{
    public const string JobType = "action_intent_compose";
    public const string SchemaId = "ai_action_intent_envelope.v1";
    public const string SnapshotSchemaId = "ai_action_context_snapshot.v1";
    public const string ActivitySchemaId = "ai_action_activity_event.v1";
    public const string ReceiptSchemaId = "ai_action_execution_receipt.v1";
    public const string DraftType = "AiActionPlan";
    public const string ConfirmAction = "execute_action_set";
    public const string TaskCreateTool = "task.create.v1";
}

public static class AiActionActivityStages
{
    public const string UnderstandIntent = "understand_intent";
    public const string ResolveContext = "resolve_context";
    public const string CollectSources = "collect_sources";
    public const string RouteModel = "route_model";
    public const string ComposeOptions = "compose_options";
    public const string ValidateOutput = "validate_output";
    public const string AwaitConfirmation = "await_confirmation";
    public const string ExecuteCommands = "execute_commands";
    public const string PersistReceipt = "persist_receipt";
    public const string ReadBack = "read_back";
}

public static class AiActionActivityStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string WaitingUser = "waiting_user";
    public const string Succeeded = "succeeded";
    public const string Warning = "warning";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Skipped = "skipped";
}

public sealed record AiActionClientContextDto(
    string? Route = null,
    string? Module = null,
    Guid? ProjectId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    IReadOnlyList<Guid>? SelectedEntityIds = null);

public sealed record AiActionComposeRequestDto(
    string Message,
    AiActionClientContextDto? Context = null,
    string Language = "vi",
    string ModelProfile = "action_composer_strong",
    int MaximumOptions = 3,
    decimal? MaximumEstimatedCostUsd = 0.08m,
    string CacheMode = "bypass");

public sealed record AiActionProjectContextDto(
    Guid Id,
    Guid? OrganizationId,
    string Name,
    string Code,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    string SourceRef);

public sealed record AiActionMemberContextDto(
    Guid UserId,
    string Name,
    string Role,
    int ActiveTaskCount,
    int EstimatedHours,
    string SourceRef);

public sealed record AiActionSkillContextDto(
    Guid SkillId,
    string Name,
    string? Description,
    string SourceRef);

public sealed record AiActionContextSnapshotDto(
    string SchemaId,
    AiActionProjectContextDto Project,
    string SourceVersion,
    string Language,
    int MaximumOptions,
    string UserIntent,
    IReadOnlyList<AiActionMemberContextDto> Members,
    IReadOnlyList<AiActionSkillContextDto> Skills,
    IReadOnlyList<string> AllowedSourceRefs);

public sealed record AiActionTargetEntityDto(
    string Type,
    Guid Id,
    string Label);

public sealed record AiActionSkillSelectionDto(
    Guid SkillId,
    string RequiredLevel);

public sealed record AiActionTaskCommandDto(
    string CommandId,
    string ToolName,
    string ToolVersion,
    string Title,
    string? Description,
    IReadOnlyList<string> AcceptanceCriteria,
    string Priority,
    DateTimeOffset? DueDate,
    int? EstimatedHours,
    Guid? AssigneeId,
    string AssigneeMode,
    IReadOnlyList<AiActionSkillSelectionDto> RequiredSkills,
    IReadOnlyList<string> SourceRefs);

public sealed record AiActionOptionDto(
    string OptionId,
    string Label,
    string Summary,
    IReadOnlyList<string> TradeOffs,
    IReadOnlyList<AiActionTaskCommandDto> Commands);

public sealed record AiActionReviewSelectionDto(
    string SelectedOptionId,
    IReadOnlyList<string> SelectedCommandIds);

public sealed record AiActionPlanDto(
    string SchemaId,
    string SchemaVersion,
    Guid ProjectId,
    string SourceVersion,
    string UserIntent,
    string IntentType,
    decimal Confidence,
    IReadOnlyList<AiActionTargetEntityDto> TargetEntities,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<AiActionOptionDto> Options,
    AiActionReviewSelectionDto Review,
    DateTimeOffset GeneratedAt);

public sealed record AiActionCommandResultDto(
    string CommandId,
    string ToolName,
    string Status,
    Guid? EntityId,
    string? EntityLabel,
    string? EntityUrl,
    string? ErrorCode,
    string? ErrorMessage,
    int AppliedSkillCount = 0);

public sealed record AiActionExecutionReceiptDto(
    string SchemaId,
    Guid ExecutionId,
    Guid DraftId,
    string SelectedOptionId,
    IReadOnlyList<string> ConfirmedCommandIds,
    IReadOnlyList<AiActionCommandResultDto> CommandResults,
    string Status,
    string? Provider,
    string? Model,
    Guid? UsageLedgerId,
    DateTimeOffset ExecutedAt,
    IReadOnlyList<string> ReadBackLinks);

public sealed record AiActionActivityEventDto(
    Guid EventId,
    int Sequence,
    string Stage,
    string Status,
    string PublicLabel,
    JsonElement? SafeDetail,
    int? Current,
    int? Total,
    int Attempt,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationMs,
    bool Retryable,
    string? ReceiptLink);

public sealed record AiActionActivityFeedDto(
    Guid JobId,
    string JobStatus,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int LastSequence,
    bool Cancellable,
    IReadOnlyList<AiActionActivityEventDto> Events);

public sealed record AppendAiActionActivityDto(
    string Stage,
    string Status,
    string? SafeDetailJson = null,
    int? Current = null,
    int? Total = null,
    int Attempt = 1,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    bool Retryable = false,
    string? ReceiptLink = null);
