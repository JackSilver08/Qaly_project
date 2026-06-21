using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectsController : BaseApiController
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;

    public ProjectsController(IProjectService projectService, ITaskService taskService)
    {
        _projectService = projectService;
        _taskService = taskService;
    }

    [HttpGet("{id:guid}/gantt")]
    public async Task<IActionResult> GetGanttData(Guid id, CancellationToken ct)
    {
        var result = await _taskService.GetGanttDataAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id, CancellationToken ct)
    {
        var result = await _taskService.GetTimelineAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _projectService.GetAllAsync(page, pageSize, search, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _projectService.GetByUserAsync(userId.Value, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _projectService.GetTrashAsync(page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _projectService.GetByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectDto dto, CancellationToken ct)
    {
        var result = await _projectService.CreateAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectDto dto, CancellationToken ct)
    {
        var result = await _projectService.UpdateAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _projectService.DeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await _projectService.RestoreAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/hard")]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken ct)
    {
        var result = await _projectService.HardDeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddProjectMemberRequest request, CancellationToken ct)
    {
        var result = await _projectService.AddMemberAsync(id, request.UserId, request.Role, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await _projectService.RemoveMemberAsync(id, userId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id:guid}/members/{userId:guid}/permissions")]
    public async Task<IActionResult> UpdateMemberPermissions(Guid id, Guid userId, UpdateProjectMemberPermissionsDto dto, CancellationToken ct)
    {
        var result = await _projectService.UpdateMemberPermissionsAsync(id, userId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/labels")]
    public async Task<IActionResult> GetLabels(Guid id, CancellationToken ct)
    {
        var result = await _projectService.GetLabelsAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/labels")]
    public async Task<IActionResult> CreateLabel(Guid id, CreateProjectLabelDto dto, CancellationToken ct)
    {
        var result = await _projectService.CreateLabelAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> UpdateLabel(Guid id, Guid labelId, UpdateProjectLabelDto dto, CancellationToken ct)
    {
        var result = await _projectService.UpdateLabelAsync(id, labelId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> DeleteLabel(Guid id, Guid labelId, CancellationToken ct)
    {
        var result = await _projectService.DeleteLabelAsync(id, labelId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/workload")]
    public async Task<IActionResult> GetWorkload(Guid id, CancellationToken ct)
    {
        var result = await _taskService.GetWorkloadAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record AddProjectMemberRequest(Guid UserId, string Role);
