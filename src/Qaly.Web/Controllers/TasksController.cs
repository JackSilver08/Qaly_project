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
    public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] string? status = null, [FromQuery] string? priority = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _taskService.GetByProjectAsync(projectId, status, priority, page, pageSize, ct);
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
}

public sealed record UpdateTaskStatusRequest(string Status);
public sealed record UpdateTaskSortOrderRequest(int SortOrder);
