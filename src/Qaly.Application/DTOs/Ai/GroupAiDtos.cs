namespace Qaly.Application.DTOs.Ai;

public record GroupAiActionItemsRequest(
    string? Source = null,
    int? MessageLimit = null,
    Guid? MeetingSessionId = null,
    string? TranscriptText = null);

public record GroupAiActionItemsResponseDto(
    Guid GroupId,
    string Source,
    IReadOnlyList<GroupAiActionItemDto> Items,
    IReadOnlyList<string> Warnings);

public record GroupAiActionItemDto(
    string Title,
    string? Description,
    string? SuggestedOwnerName,
    DateTimeOffset? DueDateSuggestion,
    decimal Confidence,
    string? SourceEvidence);

public record GroupMeetingSessionDto(
    Guid Id,
    Guid WorkGroupId,
    Guid StartedByUserId,
    string Provider,
    string RoomId,
    string? JoinUrl,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? TranscriptSourceId,
    string? Summary);

public record GroupAiSummaryRequest(
    int? MessageLimit = null);

public record GroupAiSummaryResponseDto(
    Guid GroupId,
    string Summary,
    IReadOnlyList<string> KeyDecisions,
    IReadOnlyList<string> UnresolvedQuestions,
    IReadOnlyList<string> Warnings);

public record GroupAiDraftProjectRequest(
    int? MessageLimit = null,
    string? ExtraInstructions = null);

public record GroupAiDraftTaskDto(
    string Title,
    string? Description,
    string? Priority,
    int? EstimateDays,
    string? SuggestedOwnerName);

public record GroupAiDraftProjectResponseDto(
    Guid GroupId,
    string DraftProjectName,
    string DraftProjectDescription,
    IReadOnlyList<GroupAiDraftTaskDto> DraftTasks,
    IReadOnlyList<string> Warnings);
