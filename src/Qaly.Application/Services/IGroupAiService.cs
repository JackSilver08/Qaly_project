using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IGroupAiService
{
    Task<Result<GroupAiActionItemsResponseDto>> ExtractActionItemsAsync(
        Guid groupId,
        GroupAiActionItemsRequest request,
        CancellationToken ct = default);

    Task<Result<GroupMeetingSessionDto>> LinkMeetingSummaryAndTranscriptAsync(
        Guid groupId,
        Guid meetingId,
        string? summary,
        string? transcriptSourceId,
        CancellationToken ct = default);

    Task<Result<string>> BuildGroupChatContextAsync(
        Guid groupId,
        int? limit = null,
        CancellationToken ct = default);

    Task<Result<GroupAiSummaryResponseDto>> SummarizeGroupDiscussionAsync(
        Guid groupId,
        GroupAiSummaryRequest request,
        CancellationToken ct = default);

    Task<Result<GroupAiDraftProjectResponseDto>> GenerateDraftProjectPayloadAsync(
        Guid groupId,
        GroupAiDraftProjectRequest request,
        CancellationToken ct = default);
}
