using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

/// <summary>
/// Lets an organization owner or admin define project roles that fit how their team works.
/// Each custom role inherits a built-in role's permissions, so it can never grant more than one
/// of the built-in roles already does.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class ProjectRoleDefinitionsController : BaseApiController
{
    private readonly IProjectRoleDefinitionService _service;

    public ProjectRoleDefinitionsController(IProjectRoleDefinitionService service)
    {
        _service = service;
    }

    [HttpGet("organizations/{organizationId:guid}/role-definitions")]
    public async Task<IActionResult> GetByOrganization(Guid organizationId, CancellationToken ct)
    {
        var result = await _service.GetByOrganizationAsync(organizationId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("organizations/{organizationId:guid}/role-definitions/access")]
    public async Task<IActionResult> GetManagementAccess(Guid organizationId, CancellationToken ct)
    {
        var result = await _service.CanManageAsync(organizationId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Built-in roles and the organization's own roles, ready for a role picker.</summary>
    [HttpGet("projects/{projectId:guid}/assignable-roles")]
    public async Task<IActionResult> GetAssignableForProject(Guid projectId, CancellationToken ct)
    {
        var result = await _service.GetAssignableForProjectAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("organizations/{organizationId:guid}/role-definitions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Guid organizationId,
        CreateProjectRoleDefinitionDto dto,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(organizationId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("role-definitions/{definitionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid definitionId,
        UpdateProjectRoleDefinitionDto dto,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(definitionId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("role-definitions/{definitionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid definitionId, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(definitionId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Short onboarding guide for the caller's role in a project: what to do first, the shared
    /// workflow, and which AI questions their tier can answer.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/onboarding-guide")]
    public async Task<IActionResult> GetOnboardingGuide(
        Guid projectId,
        [FromServices] IProjectService projectService,
        [FromServices] IProjectRoleCatalog roleCatalog,
        CancellationToken ct)
    {
        var project = await projectService.GetByIdAsync(projectId, ct);
        if (!project.IsSuccess || project.Data?.Permissions == null)
        {
            return StatusCode(project.StatusCode, project);
        }

        var permissions = project.Data.Permissions;

        // A custom role stores its own key, so resolve it to the built-in role it inherits before
        // choosing a guide; otherwise every custom role would fall back to the Member guide.
        var resolved = await roleCatalog.ResolveAsync(permissions.Role, project.Data.OrganizationId, ct);
        var baseRole = resolved?.BaseRole ?? permissions.Role;

        var guide = OnboardingGuideRules.Build(
            baseRole,
            isOwner: string.Equals(baseRole, ProjectRoleRules.Owner, StringComparison.Ordinal));

        // The guide follows the inherited role; the label stays the organization's own wording.
        return Ok(Application.Common.Models.Result.Success(
            guide with { RoleLabel = permissions.RoleLabel }));
    }
}
