using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] Guid? labelId = null,
        [FromQuery] string sort = "default",
        CancellationToken ct = default)
    {
        var result = await _taskService.GetByProjectAsync(projectId, status, priority, page, pageSize, search, assigneeId, labelId, sort, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("assignee/{assigneeId:guid}")]
    public async Task<IActionResult> GetByAssignee(Guid assigneeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _taskService.GetByAssigneeAsync(assigneeId, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _taskService.GetByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskDto dto, CancellationToken ct)
    {
        var result = await _taskService.CreateAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateTaskDto dto, CancellationToken ct)
    {
        var result = await _taskService.UpdateAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var result = await _taskService.UpdateStatusAsync(id, request.Status, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id}/sort-order")]
    public async Task<IActionResult> UpdateSortOrder(Guid id, [FromBody] UpdateTaskSortOrderRequest request, CancellationToken ct)
    {
        var result = await _taskService.UpdateSortOrderAsync(id, request.SortOrder, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _taskService.DeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchTaskRequest request, CancellationToken ct)
    {
        var result = await _taskService.BatchDeleteAsync(request.Ids, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("batch-status")]
    public async Task<IActionResult> BatchUpdateStatus([FromBody] BatchUpdateStatusRequest request, CancellationToken ct)
    {
        var result = await _taskService.BatchUpdateStatusAsync(request.Ids, request.Status, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id}/dates")]
    public async Task<IActionResult> UpdateDates(Guid id, [FromBody] UpdateTaskDatesRequest request, CancellationToken ct)
    {
        var result = await _taskService.UpdateDatesAsync(id, request.StartDate, request.EndDate, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id}/dependencies")]
    public async Task<IActionResult> AddDependency(Guid id, [FromBody] AddTaskDependencyRequest request, CancellationToken ct)
    {
        var result = await _taskService.AddDependencyAsync(request.PredecessorId, id, request.Type ?? "FinishToStart", ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id}/dependencies/{dependencyId:guid}")]
    public async Task<IActionResult> RemoveDependency(Guid id, Guid dependencyId, CancellationToken ct)
    {
        var result = await _taskService.RemoveDependencyAsync(dependencyId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("project/{projectId}/gantt")]
    public async Task<IActionResult> GetGanttData(Guid projectId, CancellationToken ct)
    {
        var result = await _taskService.GetGanttDataAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("/api/projects/{projectId:guid}/task-attention")]
    public async Task<IActionResult> GetProjectTaskAttention(
        Guid projectId,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] Guid? reporterId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? riskType = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sort = "risk",
        CancellationToken ct = default)
    {
        var result = await _taskService.GetAttentionByProjectAsync(
            projectId,
            assigneeId,
            reporterId,
            status,
            priority,
            riskType,
            from,
            to,
            page,
            pageSize,
            sort,
            ct);

        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("/api/projects/{projectId:guid}/tasks/{taskId:guid}/viewed")]
    public async Task<IActionResult> MarkViewed(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var result = await _taskService.MarkViewedAsync(projectId, taskId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("/api/projects/{projectId:guid}/tasks/{taskId:guid}/nudge")]
    public async Task<IActionResult> NudgeAssignee(Guid projectId, Guid taskId, [FromBody] NudgeTaskAssigneeRequest? request, CancellationToken ct)
    {
        var result = await _taskService.NudgeAssigneeAsync(projectId, taskId, request?.AssigneeId, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record UpdateTaskStatusRequest(string Status);
public sealed record UpdateTaskSortOrderRequest(int SortOrder);
public sealed record UpdateTaskDatesRequest(DateTimeOffset? StartDate, DateTimeOffset? EndDate);
public sealed record AddTaskDependencyRequest(Guid PredecessorId, string? Type);
public sealed record BatchTaskRequest(IEnumerable<Guid> Ids);
public sealed record BatchUpdateStatusRequest(IEnumerable<Guid> Ids, string Status);
public sealed record NudgeTaskAssigneeRequest(Guid? AssigneeId);
