namespace Qaly.Application.DTOs.Ai;

public record CreateAiJobDto(
    string JobType,
    Guid ProjectId,
    string SourceType,
    string? SourceId,
    string ProviderHint = "auto",
    bool Sensitive = false,
    string? SourceText = null);

public record AiJobCreatedDto(
    Guid JobId,
    string Status,
    decimal EstimatedCostUsd,
    string CacheKey,
    Guid? DraftId);

public record ConfirmAiDraftDto(
    string? EditedPayloadJson,
    string ConfirmAction,
    string? ConfirmationNote);

public record AiDraftConfirmResultDto(
    Guid DraftId,
    string Status,
    string ConfirmAction,
    int CreatedTaskCount,
    IReadOnlyList<Guid> CreatedTaskIds);

public record AiTaskDraftPayload(IReadOnlyList<AiTaskDraftItem> Tasks);

public record AiTaskDraftItem(
    string Title,
    string? Description,
    string Priority = "Medium",
    string Status = "Todo",
    DateTimeOffset? DueDate = null,
    Guid? AssigneeId = null);
