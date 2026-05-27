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
