using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;

namespace Qaly.Application.Services.Tasks;

public static class TaskQueryExtensions
{
    public static IQueryable<TaskItem> WithDetails(this IQueryable<TaskItem> query)
        => query
            .Include(task => task.Project)
            .Include(task => task.Assignee)
            .Include(task => task.Reviewer)
            .Include(task => task.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(task => task.Reporter)
            .Include(task => task.Labels)
                .ThenInclude(label => label.ProjectLabel)
            .Include(task => task.Comments)
            .Include(task => task.Attachments)
            .Include(task => task.Subtasks);

    public static IQueryable<TaskItem> WithProject(this IQueryable<TaskItem> query)
        => query.Include(task => task.Project);

    public static IOrderedQueryable<TaskItem> OrderByPlanningPriority(this IQueryable<TaskItem> query)
        => query
            .OrderBy(task => task.Status == "InProgress" ? 0 :
                task.Status == "InReview" ? 1 :
                task.Status == "Todo" ? 2 :
                task.Status == "Done" ? 3 :
                task.Status == "Cancelled" ? 4 : 5)
            .ThenBy(task => task.DueDate ?? DateTimeOffset.MaxValue)
            .ThenByDescending(task => task.CreatedAt)
            .ThenBy(task => task.Id);
}
