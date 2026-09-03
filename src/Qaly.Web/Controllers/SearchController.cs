using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Infrastructure.Data;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController : BaseApiController
{
    private readonly QalyDbContext _context;
    private readonly IProjectRoleCatalog _roleCatalog;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public SearchController(
        QalyDbContext context,
        IProjectRoleCatalog roleCatalog,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _context = context;
        _roleCatalog = roleCatalog;
        _taskAccessPolicy = taskAccessPolicy;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] Guid? projectId = null, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var normalized = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Ok(Array.Empty<SearchResultDto>());
        }

        var isAdmin = User.IsInRole("Admin");
        var projectQuery = _context.Projects
            .AsNoTracking()
            .Include(project => project.Members)
            .AsQueryable();

        if (projectId.HasValue)
        {
            projectQuery = projectQuery.Where(project => project.Id == projectId.Value);
        }

        if (!isAdmin)
        {
            projectQuery = projectQuery.Where(project =>
                project.OrganizationId == null
                    ? project.OwnerId == userId.Value ||
                      project.Members.Any(member => member.UserId == userId.Value)
                    : project.Organization != null &&
                      project.Organization.IsActive &&
                      (project.Organization.OwnerId == userId.Value ||
                       project.Organization.Members.Any(member =>
                           member.UserId == userId.Value &&
                           (member.Role == OrganizationRoleRules.OrganizationAdmin ||
                            member.Role == "Admin" ||
                            member.Role == "Manager")) ||
                       ((project.OwnerId == userId.Value ||
                         project.Members.Any(member => member.UserId == userId.Value)) &&
                        project.Organization.Members.Any(member => member.UserId == userId.Value))));
        }

        var accessibleProjects = await projectQuery
            .Select(project => new { project.Id, project.Name, project.OwnerId, project.OrganizationId })
            .ToListAsync(ct);
        var accessibleProjectIds = accessibleProjects.Select(project => project.Id).ToHashSet();
        var currentMemberships = await _context.ProjectMembers
            .AsNoTracking()
            .Where(member =>
                accessibleProjectIds.Contains(member.ProjectId) &&
                member.UserId == userId.Value)
            .Select(member => new { member.ProjectId, member.Role })
            .ToListAsync(ct);
        var customerScopedProjects = new HashSet<Guid>();
        var managedProjects = new HashSet<Guid>();
        foreach (var membership in currentMemberships)
        {
            var organizationId = accessibleProjects
                .First(project => project.Id == membership.ProjectId)
                .OrganizationId;
            var resolvedRole = await _roleCatalog.ResolveAsync(membership.Role, organizationId, ct);
            if (resolvedRole != null && ProjectRoleRules.IsCustomer(resolvedRole.BaseRole))
            {
                customerScopedProjects.Add(membership.ProjectId);
            }
        }
        foreach (var project in accessibleProjects)
        {
            if (await _taskAccessPolicy.CanManageProjectAsync(project.Id, project.OwnerId, ct))
            {
                managedProjects.Add(project.Id);
            }
        }

        var projectResults = accessibleProjects
            .Where(project => project.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(project => new SearchResultDto("Project", project.Id, project.Name, null, project.Id, $"/projects/{project.Id}"))
            .ToList();

        var tasks = await _taskAccessPolicy.ApplyVisibilityFilter(_context.TaskItems)
            .AsNoTracking()
            .Where(task => accessibleProjectIds.Contains(task.ProjectId) &&
                (task.Title.Contains(normalized) || (task.Description != null && task.Description.Contains(normalized))))
            .OrderByDescending(task => task.IsPinned)
            .ThenByDescending(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .Take(12)
            .ToListAsync(ct);

        var taskResults = tasks
            .Select(task => new SearchResultDto("Task", task.Id, task.Title, task.Description, task.ProjectId, $"/projects/{task.ProjectId}/tasks/{task.Id}"))
            .ToList();

        var wikiResults = await _context.WikiPages
            .AsNoTracking()
            .Include(page => page.Project)
            .Where(page => accessibleProjectIds.Contains(page.ProjectId) &&
                ((page.Visibility == "public") || (page.Visibility == "customer_safe") || !customerScopedProjects.Contains(page.ProjectId)) &&
                (page.Visibility != "private" || page.AuthorId == userId.Value || managedProjects.Contains(page.ProjectId)) &&
                (page.Title.Contains(normalized) || page.Content.Contains(normalized)))
            .OrderByDescending(page => page.UpdatedAt)
            .ThenBy(page => page.Id)
            .Take(8)
            .Select(page => new SearchResultDto("Wiki", page.Id, page.Title, page.Project.Name, page.ProjectId, $"/projects/{page.ProjectId}?tab=wiki"))
            .ToListAsync(ct);

        return Ok(projectResults.Concat(taskResults).Concat(wikiResults).Take(20).ToList());
    }
}

public sealed record SearchResultDto(
    string Type,
    Guid Id,
    string Title,
    string? Summary,
    Guid? ProjectId,
    string Url);
