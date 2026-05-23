using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class TimeEntriesController : ControllerBase
{
    private readonly ITimeTrackingService _timeTrackingService;

    public TimeEntriesController(ITimeTrackingService timeTrackingService)
    {
        _timeTrackingService = timeTrackingService;
    }

    [HttpPost("tasks/{taskId:guid}/time-entries")]
    public async Task<IActionResult> StartTimer(Guid taskId)
    {
        var result = await _timeTrackingService.StartTimerAsync(taskId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("time-entries/{id:guid}/stop")]
    public async Task<IActionResult> StopTimer(Guid id)
    {
        var result = await _timeTrackingService.StopTimerAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("tasks/{taskId:guid}/time-entries/manual")]
    public async Task<IActionResult> AddManualEntry(Guid taskId, CreateTimeEntryDto dto)
    {
        if (taskId != dto.TaskId) return BadRequest("Task ID mismatch.");
        var result = await _timeTrackingService.AddManualEntryAsync(dto);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("projects/{projectId:guid}/time-entries")]
    public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        var result = await _timeTrackingService.GetByProjectAsync(projectId, from, to);
        return StatusCode(result.StatusCode, result);
    }
}
