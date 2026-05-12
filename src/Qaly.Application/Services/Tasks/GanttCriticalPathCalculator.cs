using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services.Tasks;

public static class GanttCriticalPathCalculator
{
    public static void MarkCriticalPath(List<GanttTaskDto> tasks)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        var tasksWithDates = tasks
            .Where(task => task.StartDate.HasValue && task.EndDate.HasValue)
            .ToList();

        if (tasksWithDates.Count == 0)
        {
            return;
        }

        var earlyStart = new Dictionary<Guid, DateTimeOffset>();
        var earlyFinish = new Dictionary<Guid, DateTimeOffset>();

        foreach (var task in tasksWithDates.OrderBy(task => task.StartDate))
        {
            var predecessors = tasksWithDates
                .Where(candidate => task.Dependencies.Contains(candidate.Id))
                .ToList();

            var earliestStart = task.StartDate!.Value;

            if (predecessors.Count > 0)
            {
                var maxEarlyFinish = predecessors.Max(predecessor =>
                    earlyFinish.TryGetValue(predecessor.Id, out var value)
                        ? value
                        : predecessor.EndDate!.Value);

                if (maxEarlyFinish > earliestStart)
                {
                    earliestStart = maxEarlyFinish;
                }
            }

            earlyStart[task.Id] = earliestStart;
            earlyFinish[task.Id] = earliestStart.Add(task.EndDate!.Value - task.StartDate!.Value);
        }

        var lateStart = new Dictionary<Guid, DateTimeOffset>();
        var projectFinish = earlyFinish.Values.Max();

        foreach (var task in tasksWithDates.OrderByDescending(task => task.EndDate))
        {
            var successors = tasksWithDates
                .Where(candidate => candidate.Dependencies.Contains(task.Id))
                .ToList();

            var latestFinish = projectFinish;

            if (successors.Count > 0)
            {
                latestFinish = successors.Min(successor =>
                    lateStart.TryGetValue(successor.Id, out var value)
                        ? value
                        : successor.StartDate!.Value);
            }

            lateStart[task.Id] = latestFinish.Subtract(task.EndDate!.Value - task.StartDate!.Value);
        }

        foreach (var task in tasksWithDates)
        {
            if (earlyStart.TryGetValue(task.Id, out var earliestStart) &&
                lateStart.TryGetValue(task.Id, out var latestStart) &&
                Math.Abs((latestStart - earliestStart).TotalHours) < 0.01)
            {
                task.IsCriticalPath = true;
            }
        }
    }
}
