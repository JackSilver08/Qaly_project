using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public class TaskAttentionSignalWorker : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogScanFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, nameof(LogScanFailed)),
        "Khong the quet canh bao timeline task.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskAttentionSignalWorker> _logger;

    public TaskAttentionSignalWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TaskAttentionSignalWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogScanFailed(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var now = DateTimeOffset.UtcNow;

        var tasks = await db.TaskItems
            .Include(task => task.Project)
            .Include(task => task.Assignees)
            .Include(task => task.ViewEvents)
            .Where(task => task.Status != "Done" && (task.AssigneeId != null || task.Assignees.Any()))
            .Take(500)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            var recipients = task.Assignees.Select(assignment => assignment.UserId).ToHashSet();
            if (task.AssigneeId.HasValue)
            {
                recipients.Add(task.AssigneeId.Value);
            }

            foreach (var userId in recipients)
            {
                foreach (var detected in DetectSignals(task, userId, now))
                {
                    var signal = await db.TaskAttentionSignals
                        .FirstOrDefaultAsync(item =>
                            item.TaskItemId == task.Id &&
                            item.UserId == userId &&
                            item.SignalType == detected.SignalType,
                            ct);

                    if (signal == null)
                    {
                        signal = new TaskAttentionSignal
                        {
                            TaskItemId = task.Id,
                            UserId = userId,
                            SignalType = detected.SignalType,
                            FirstDetectedAt = now,
                            CooldownHours = detected.CooldownHours
                        };
                        db.TaskAttentionSignals.Add(signal);
                    }
                    else if (signal.ResolvedAt.HasValue)
                    {
                        signal.ResolvedAt = null;
                        signal.FirstDetectedAt = now;
                    }

                    if (!ShouldSend(signal, now))
                    {
                        continue;
                    }

                    await notifications.CreateAsync(
                        userId,
                        detected.Message,
                        detected.SignalType,
                        task.Id,
                        nameof(TaskItem),
                        ct);
                    signal.LastSentAt = now;
                }
            }
        }

        await ResolveInactiveSignalsAsync(db, tasks, now, ct);
        await db.SaveChangesAsync(ct);
    }

    private static IEnumerable<DetectedSignal> DetectSignals(TaskItem task, Guid userId, DateTimeOffset now)
    {
        if (task.DueDate.HasValue && task.DueDate.Value < now)
        {
            yield return new("Overdue", 24, $"Công việc \"{task.Title}\" đã quá hạn. Vui lòng cập nhật trạng thái hoặc bình luận lý do.");
        }
        else if (task.DueDate.HasValue && task.DueDate.Value <= now.AddHours(24))
        {
            yield return new("DueSoon", 24, $"Công việc \"{task.Title}\" sắp tới hạn trong 24 giờ.");
        }

        if (task.StartDate.HasValue && task.StartDate.Value < now && string.Equals(task.Status, "Todo", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("StaleTodo", 24, $"Công việc \"{task.Title}\" đã tới ngày bắt đầu nhưng vẫn chưa được khởi động.");
        }

        if (IsStaleInProgress(task, now))
        {
            yield return new("StaleInProgress", 48, $"Công việc \"{task.Title}\" đang làm quá lâu so với timeline dự kiến.");
        }

        var assignedAt = task.Assignees
            .Where(assignment => assignment.UserId == userId)
            .Select(assignment => (DateTimeOffset?)assignment.AssignedAt)
            .OrderByDescending(value => value)
            .FirstOrDefault() ?? task.CreatedAt;
        var viewedAt = task.ViewEvents
            .Where(view => view.UserId == userId)
            .Select(view => (DateTimeOffset?)view.ViewedAt)
            .OrderByDescending(value => value)
            .FirstOrDefault();
        if (!viewedAt.HasValue || viewedAt.Value < assignedAt)
        {
            yield return new("Unseen", 12, $"Bạn chưa mở công việc \"{task.Title}\" sau khi được giao.");
        }
    }

    private static async Task ResolveInactiveSignalsAsync(QalyDbContext db, List<TaskItem> scannedTasks, DateTimeOffset now, CancellationToken ct)
    {
        var activeKeys = scannedTasks
            .SelectMany(task =>
            {
                var users = task.Assignees.Select(assignment => assignment.UserId).ToHashSet();
                if (task.AssigneeId.HasValue) users.Add(task.AssigneeId.Value);
                return users.SelectMany(userId => DetectSignals(task, userId, now)
                    .Select(signal => $"{task.Id:N}:{userId:N}:{signal.SignalType}"));
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var taskIds = scannedTasks.Select(task => task.Id).ToList();
        var openSignals = await db.TaskAttentionSignals
            .Where(signal => taskIds.Contains(signal.TaskItemId) && signal.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var signal in openSignals)
        {
            var key = $"{signal.TaskItemId:N}:{signal.UserId:N}:{signal.SignalType}";
            if (!activeKeys.Contains(key))
            {
                signal.ResolvedAt = now;
            }
        }
    }

    private static bool ShouldSend(TaskAttentionSignal signal, DateTimeOffset now)
        => !signal.LastSentAt.HasValue ||
           signal.LastSentAt.Value.AddHours(Math.Max(1, signal.CooldownHours)) <= now;

    private static bool IsStaleInProgress(TaskItem task, DateTimeOffset now)
    {
        if (!string.Equals(task.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
            !task.StartDate.HasValue ||
            !task.DueDate.HasValue)
        {
            return false;
        }

        var total = task.DueDate.Value - task.StartDate.Value;
        if (total.TotalMinutes <= 0)
        {
            return false;
        }

        return (now - task.StartDate.Value).TotalMinutes / total.TotalMinutes >= 0.7;
    }

    private sealed record DetectedSignal(string SignalType, int CooldownHours, string Message);
}
