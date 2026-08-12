using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectRolesController : BaseApiController
{
    private readonly IProjectRoleService _roleService;

    public ProjectRolesController(IProjectRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet("projects/{projectId:guid}/roles")]
    public async Task<IActionResult> GetCustomRoles(Guid projectId, CancellationToken ct)
    {
        var result = await _roleService.GetCustomRolesAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/roles")]
    public async Task<IActionResult> CreateCustomRole(Guid projectId, [FromBody] CreateProjectCustomRoleDto dto, CancellationToken ct)
    {
        var result = await _roleService.CreateCustomRoleAsync(projectId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/members/{memberId:guid}/check-conflicts")]
    public async Task<IActionResult> CheckConflicts(Guid projectId, Guid memberId, [FromBody] AssignProjectMemberRoleDto dto, CancellationToken ct)
    {
        var result = await _roleService.CheckRoleAssignConflictsAsync(projectId, memberId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/members/{memberId:guid}/assign-role")]
    public async Task<IActionResult> AssignRole(Guid projectId, Guid memberId, [FromBody] AssignProjectMemberRoleDto dto, CancellationToken ct)
    {
        var result = await _roleService.AssignRoleAsync(projectId, memberId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("projects/{projectId:guid}/members/{memberId:guid}/history")]
    public async Task<IActionResult> GetMemberRoleHistory(Guid projectId, Guid memberId, CancellationToken ct)
    {
        var result = await _roleService.GetMemberRoleHistoryAsync(projectId, memberId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("system-permissions")]
    public async Task<IActionResult> GetSystemPermissions([FromQuery] string? systemRole, [FromQuery] Guid? userId, CancellationToken ct)
    {
        var result = await _roleService.GetSystemModulePermissionsAsync(systemRole, userId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("system-permissions")]
    public async Task<IActionResult> UpdateSystemPermission([FromQuery] string? systemRole, [FromQuery] Guid? userId, [FromBody] UpdateSystemModulePermissionDto dto, CancellationToken ct)
    {
        var result = await _roleService.UpdateSystemModulePermissionAsync(systemRole, userId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }
}
