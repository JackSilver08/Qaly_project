using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
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

    [HttpGet("{id}")]
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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectDto dto, CancellationToken ct)
    {
        var result = await _projectService.UpdateAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _projectService.DeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddProjectMemberRequest request, CancellationToken ct)
    {
        var result = await _projectService.AddMemberAsync(id, request.UserId, request.Role, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await _projectService.RemoveMemberAsync(id, userId, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record AddProjectMemberRequest(Guid UserId, string Role);
