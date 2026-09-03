using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Domain.Interfaces;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectRolesController : BaseApiController
{
    private readonly IProjectRoleService _roleService;
    private readonly ISystemModuleAuthorizationService _systemAuthorization;
    private readonly ICurrentUserService _currentUser;

    public ProjectRolesController(
        IProjectRoleService roleService,
        ISystemModuleAuthorizationService systemAuthorization,
        ICurrentUserService currentUser)
    {
        _roleService = roleService;
        _systemAuthorization = systemAuthorization;
        _currentUser = currentUser;
    }

    [HttpGet("projects/{projectId:guid}/roles")]
    public async Task<IActionResult> GetCustomRoles(Guid projectId, CancellationToken ct)
    {
        var result = await _roleService.GetCustomRolesAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCustomRole(Guid projectId, [FromBody] CreateProjectCustomRoleDto dto, CancellationToken ct)
    {
        var result = await _roleService.CreateCustomRoleAsync(projectId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/members/{memberId:guid}/check-conflicts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckConflicts(Guid projectId, Guid memberId, [FromBody] AssignProjectMemberRoleDto dto, CancellationToken ct)
    {
        var result = await _roleService.CheckRoleAssignConflictsAsync(projectId, memberId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("projects/{projectId:guid}/members/{memberId:guid}/assign-role")]
    [ValidateAntiForgeryToken]
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

    [HttpGet("effective-system-permissions")]
    public async Task<IActionResult> GetEffectiveSystemPermissions(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var effective = await _systemAuthorization.ResolveAllAsync(
            _currentUser.UserId.Value,
            _currentUser.Role,
            ct);
        return Ok(effective.Select(item => new EffectiveSystemModuleAccessDto(
            item.ModuleKey,
            item.IsAllowed,
            SystemModulePermissionRules.FormatTier(item.AiTier),
            item.Source)));
    }

    [HttpPut("system-permissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSystemPermission([FromQuery] string? systemRole, [FromQuery] Guid? userId, [FromBody] UpdateSystemModulePermissionDto dto, CancellationToken ct)
    {
        var result = await _roleService.UpdateSystemModulePermissionAsync(systemRole, userId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }
}
