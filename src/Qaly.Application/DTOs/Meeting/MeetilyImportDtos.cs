namespace Qaly.Application.DTOs.Meeting;

public record MeetilyImportRequest(
    Guid ProjectId,
    string Title,
    string? SourceId,
    DateTimeOffset? MeetingStartedAt,
    string? Summary,
    string? TranscriptText,
    IReadOnlyList<string>? Participants,
    IReadOnlyList<MeetilyActionItemInput>? ActionItems,
    string? RawPayloadJson,
    Guid? ConsentId = null,
    Guid? RetentionPolicyId = null,
    string ProcessingMode = "local_only",
    int? RetentionDays = null,
    string? NoticeVersion = null);

public record MeetilyActionItemInput(
    string Title,
    string? Owner,
    DateTimeOffset? DueDate,
    string? Priority,
    string? Evidence);

public record MeetilyImportResult(
    Guid MeetingImportId,
    Guid ProjectId,
    string SourceProvider,
    string SourceHash,
    bool Duplicate,
    Guid? AiJobId,
    Guid? DraftId,
    MeetingExtractionPayload Extraction);

public record MeetingExtractionPayload(
    string SchemaVersion,
    string SourceHash,
    MeetingSummaryDto Meeting,
    IReadOnlyList<MeetingActionDraftDto> ActionItems,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Warnings);

public record MeetingSummaryDto(
    string Title,
    DateTimeOffset? StartedAt,
    string? Summary,
    IReadOnlyList<string> Participants);

public record MeetingActionDraftDto(
    string Title,
    string? Description,
    string Priority,
    DateTimeOffset? DueDate,
    string? SourceEvidence,
    string? SuggestedOwnerName);

public record MeetingActionItemCreateRequest(
    Guid? AssigneeId,
    string? Title,
    string? Description,
    string? Priority,
    DateTimeOffset? DueDate,
    IReadOnlyList<Guid>? LabelIds);

public record MeetingActionItemsResponseDto(
    Guid MeetingImportId,
    IReadOnlyList<MeetingActionItemDto> Items);

public record MeetingActionItemDto(
    int ItemIndex,
    string Title,
    string? Description,
    string? SuggestedOwnerName,
    string Priority,
    DateTimeOffset? DueDate,
    string MappingStatus,
    Guid? TaskId);

public record LinkMeetingActionItemTaskRequest(
    Guid TaskId);

public record MeetingActionItemTaskLinkDto(
    Guid MeetingImportId,
    int ItemIndex,
    bool IsLinked,
    Guid? TaskId,
    string Status);

public record TaskMeetingSourceDto(
    Guid TaskId,
    Guid MeetingImportId,
    string MeetingTitle,
    DateTimeOffset? MeetingStartedAt,
    int ActionItemIndex,
    string? ActionItemTitle,
    string? ActionItemDescription,
    string? SourcePriority,
    DateTimeOffset? SourceDueDate,
    string? SourceQuote,
    string MappingStatus,
    Guid? LinkedTaskId);
