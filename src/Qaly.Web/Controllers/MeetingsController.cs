using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/meetings")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingImportService _meetingImportService;

    public MeetingsController(IMeetingImportService meetingImportService)
    {
        _meetingImportService = meetingImportService;
    }

    [HttpPost("import/meetily")]
    public async Task<IActionResult> ImportMeetily(MeetilyImportRequest request, CancellationToken ct)
    {
        var result = await _meetingImportService.ImportMeetilyAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{meetingId:guid}/action-items/{itemIndex:int}/create-task")]
    public async Task<IActionResult> CreateTaskFromActionItem(Guid meetingId, int itemIndex, MeetingActionItemCreateRequest request, CancellationToken ct)
    {
        var result = await _meetingImportService.CreateTaskFromMeetingActionItemAsync(meetingId, itemIndex, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{meetingId:guid}/action-items")]
    public async Task<IActionResult> GetActionItems(Guid meetingId, CancellationToken ct)
    {
        var result = await _meetingImportService.GetMeetingActionItemsAsync(meetingId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{meetingId:guid}/action-items/{itemIndex:int}/link-task")]
    public async Task<IActionResult> LinkActionItemToTask(Guid meetingId, int itemIndex, [FromBody] LinkMeetingActionItemTaskRequest request, CancellationToken ct)
    {
        var result = await _meetingImportService.LinkMeetingActionItemToTaskAsync(meetingId, itemIndex, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{meetingId:guid}/action-items/{itemIndex:int}/task-link")]
    public async Task<IActionResult> GetActionItemTaskLink(Guid meetingId, int itemIndex, CancellationToken ct)
    {
        var result = await _meetingImportService.GetMeetingActionItemTaskLinkAsync(meetingId, itemIndex, ct);
        return StatusCode(result.StatusCode, result);
    }
}
