using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/ai")]
public class GroupAiController : BaseApiController
{
    private readonly IGroupAiService _groupAiService;

    public GroupAiController(IGroupAiService groupAiService)
    {
        _groupAiService = groupAiService;
    }

    [HttpPost("action-items")]
    public async Task<IActionResult> ExtractActionItems(
        Guid groupId,
        GroupAiActionItemsRequest request,
        CancellationToken ct)
    {
        var result = await _groupAiService.ExtractActionItemsAsync(groupId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("../meetings/{meetingId:guid}/hook")]
    public async Task<IActionResult> LinkMeetingSummaryAndTranscript(
        Guid groupId,
        Guid meetingId,
        [FromBody] GroupMeetingHookRequest request,
        CancellationToken ct)
    {
        var result = await _groupAiService.LinkMeetingSummaryAndTranscriptAsync(
            groupId,
            meetingId,
            request.Summary,
            request.TranscriptSourceId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetChatContext(
        Guid groupId,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await _groupAiService.BuildGroupChatContextAsync(groupId, limit, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("summary")]
    public async Task<IActionResult> SummarizeDiscussion(
        Guid groupId,
        [FromBody] GroupAiSummaryRequest request,
        CancellationToken ct)
    {
        var result = await _groupAiService.SummarizeGroupDiscussionAsync(groupId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("draft-project")]
    public async Task<IActionResult> GenerateDraftProjectPayload(
        Guid groupId,
        [FromBody] GroupAiDraftProjectRequest request,
        CancellationToken ct)
    {
        var result = await _groupAiService.GenerateDraftProjectPayloadAsync(groupId, request, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public record GroupMeetingHookRequest(
    string? Summary,
    string? TranscriptSourceId);
