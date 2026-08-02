using System;
using System.Collections.Generic;

namespace Qaly.Application.DTOs.Meeting;

public record AutoChecknoteRequest(
    Guid ProjectId,
    string Title,
    string TranscriptText,
    IReadOnlyList<string> Participants,
    Guid? ConsentId = null,
    Guid? RetentionPolicyId = null,
    string ProcessingMode = "local_only",
    int? RetentionDays = null,
    string? NoticeVersion = null);

public record AutoChecknoteResponseDto(
    Guid MeetingImportId,
    Guid ProjectId,
    Guid AiJobId,
    Guid DraftId,
    string Summary,
    IReadOnlyList<MeetingActionItemDto> ActionItems,
    IReadOnlyList<MeetingSourceEvidenceDto>? SummaryEvidence = null,
    IReadOnlyList<MeetingDecisionDto>? Decisions = null,
    IReadOnlyList<MeetingRiskDto>? Risks = null,
    string? Provider = null,
    string? Model = null,
    bool CacheHit = false,
    bool IsMock = false);
