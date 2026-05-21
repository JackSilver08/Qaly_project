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
    string? RawPayloadJson);

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
