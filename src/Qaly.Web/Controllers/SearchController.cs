using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Infrastructure.Data;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly QalyDbContext _context;

    public SearchController(QalyDbContext context)
    {
        _context = context;
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
                project.OwnerId == userId.Value ||
                project.Members.Any(member => member.UserId == userId.Value));
        }

        var accessibleProjects = await projectQuery
            .Select(project => new { project.Id, project.Name, project.OwnerId })
            .ToListAsync(ct);
        var accessibleProjectIds = accessibleProjects.Select(project => project.Id).ToHashSet();
        var customerScopedProjectIds = await _context.ProjectMembers
            .AsNoTracking()
            .Where(member =>
                accessibleProjectIds.Contains(member.ProjectId) &&
                member.UserId == userId.Value &&
                member.Role == ProjectRoleRules.Customer)
            .Select(member => member.ProjectId)
            .ToListAsync(ct);
        var customerScopedProjects = customerScopedProjectIds.ToHashSet();

        var projectResults = accessibleProjects
            .Where(project => project.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(project => new SearchResultDto("Project", project.Id, project.Name, null, project.Id, $"/projects/{project.Id}"))
            .ToList();

        var tasks = await _context.TaskItems
            .AsNoTracking()
            .Include(task => task.Project)
                .ThenInclude(project => project.Members)
            .Where(task => accessibleProjectIds.Contains(task.ProjectId) &&
                (task.Title.Contains(normalized) || (task.Description != null && task.Description.Contains(normalized))))
            .OrderByDescending(task => task.IsPinned)
            .ThenByDescending(task => task.CreatedAt)
            .Take(12)
            .ToListAsync(ct);

        var taskResults = tasks.Select(task =>
        {
            var restricted = task.IsPrivate &&
                !isAdmin &&
                task.ReporterId != userId.Value &&
                task.AssigneeId != userId.Value &&
                task.Project.OwnerId != userId.Value &&
                !task.Project.Members.Any(member =>
                    member.UserId == userId.Value &&
                    (member.Role == "Owner" || member.Role == "Manager" || member.Role == "Admin"));

            var title = restricted ? $"Restricted Task #{task.Id.ToString()[..8]}" : task.Title;
            var summary = restricted ? null : task.Description;
            return new SearchResultDto("Task", task.Id, title, summary, task.ProjectId, $"/projects/{task.ProjectId}/tasks/{task.Id}");
        });

        var wikiResults = await _context.WikiPages
            .AsNoTracking()
            .Include(page => page.Project)
            .Where(page => accessibleProjectIds.Contains(page.ProjectId) &&
                ((page.Visibility == "public") || (page.Visibility == "customer_safe") || !customerScopedProjects.Contains(page.ProjectId)) &&
                (page.Title.Contains(normalized) || page.Content.Contains(normalized)))
            .OrderByDescending(page => page.UpdatedAt)
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
