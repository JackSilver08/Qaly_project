using System;
using System.Collections.Generic;

namespace Qaly.Application.DTOs.Meeting;

public record AutoChecknoteRequest(
    Guid ProjectId,
    string Title,
    string TranscriptText,
    IReadOnlyList<string> Participants);

public record AutoChecknoteResponseDto(
    Guid MeetingImportId,
    Guid ProjectId,
    Guid AiJobId,
    Guid DraftId,
    string Summary,
    IReadOnlyList<MeetingActionItemDto> ActionItems);
