using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Qaly.Domain.Entities;

namespace Qaly.Application.Services;

public static class AiProgressSummaryFingerprint
{
    public static string Compute(Project project, IEnumerable<TaskItem> tasks)
        => ComputeCore(
            new StringBuilder()
                .Append("project|")
                .Append(project.Id).Append('|')
                .Append(project.Name).Append('|')
                .Append(project.Code).Append('|')
                .Append(project.Description).Append('|')
                .Append(project.Status).Append('|')
                .Append(project.StartDate?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(project.EndDate?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(project.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture)),
            tasks);

    public static string Compute(Project project, Sprint sprint, IEnumerable<TaskItem> tasks)
        => ComputeCore(
            new StringBuilder()
                .Append("sprint|")
                .Append(project.Id).Append('|')
                .Append(project.Name).Append('|')
                .Append(project.Code).Append('|')
                .Append(project.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(sprint.Id).Append('|')
                .Append(sprint.Name).Append('|')
                .Append(sprint.Status).Append('|')
                .Append(sprint.Goal).Append('|')
                .Append(sprint.StartDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(sprint.EndDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(sprint.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture)),
            tasks);

    private static string ComputeCore(StringBuilder input, IEnumerable<TaskItem> tasks)
    {
        foreach (var task in tasks.OrderBy(item => item.Id))
        {
            input.Append('\n')
                .Append(task.Id).Append('|')
                .Append(task.Title).Append('|')
                .Append(task.Description).Append('|')
                .Append(task.Status).Append('|')
                .Append(task.Priority).Append('|')
                .Append(task.StartDate?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(task.DueDate?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(task.EstimatedHours?.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(task.IsPrivate).Append('|')
                .Append(task.ContributesToProgress).Append('|')
                .Append(task.IsDeleted).Append('|')
                .Append(task.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(task.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(task.RowVersion));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString())))
            .ToLowerInvariant();
    }
}
