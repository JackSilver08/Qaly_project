using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class SprintsController : ControllerBase
{
    private readonly ITaskService _taskService;

    public SprintsController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet("projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var result = await _taskService.GetSprintsAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> Create(Guid projectId, CreateSprintRequest request, CancellationToken ct)
    {
        var result = await _taskService.CreateSprintAsync(projectId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("sprints/{sprintId:guid}")]
    public async Task<IActionResult> Update(Guid sprintId, UpdateSprintRequest request, CancellationToken ct)
    {
        var result = await _taskService.UpdateSprintAsync(sprintId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("sprints/{sprintId:guid}")]
    public async Task<IActionResult> Delete(Guid sprintId, CancellationToken ct)
    {
        var result = await _taskService.DeleteSprintAsync(sprintId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("projects/{projectId:guid}/sprints/{sprintId:guid}/timeline")]
    public async Task<IActionResult> GetSprintTimeline(Guid projectId, Guid sprintId, CancellationToken ct)
    {
        var result = await _taskService.GetSprintTimelineAsync(projectId, sprintId, ct);
        return StatusCode(result.StatusCode, result);
    }
}
