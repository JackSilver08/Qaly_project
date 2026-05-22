using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Repositories;

public sealed class ProjectDashboardSummaryRepository : IProjectDashboardSummaryRepository
{
    private readonly QalyDbContext _context;

    public ProjectDashboardSummaryRepository(QalyDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectDashboardProjectInfo?> GetProjectInfoAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new ProjectDashboardProjectInfo(project.Id, project.OwnerId))
            .FirstOrDefaultAsync(ct);
    }

    public IQueryable<TaskItem> QueryProjectTasks(Guid projectId, DateTimeOffset? from, DateTimeOffset? toDate)
    {
        // Task 13 MVP rule: from/to filter by TaskItem.CreatedAt.
        // If from/to are not provided, include all current tasks of the project.
        var query = _context.TaskItems
            .Where(task => task.ProjectId == projectId);

        if (from.HasValue)
        {
            query = query.Where(task => task.CreatedAt >= from.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(task => task.CreatedAt <= toDate.Value);
        }

        return query;
    }
}
