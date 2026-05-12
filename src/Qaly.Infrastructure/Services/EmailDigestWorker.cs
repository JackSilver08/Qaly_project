using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using System.Text;

namespace Qaly.Infrastructure.Services;

public partial class EmailDigestWorker : BackgroundService
{
    private readonly ILogger<EmailDigestWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EmailDigestWorker(ILogger<EmailDigestWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarting(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(7); // Next run at 7:00 AM tomorrow
            
            // If it's already past 7:00 AM today, the next run is tomorrow.
            // If it's before 7:00 AM today, the next run should be today at 7:00 AM.
            if (now.Hour < 7)
            {
                nextRun = now.Date.AddHours(7);
            }

            var delay = nextRun - now;
            LogNextRun(_logger, nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await SendDigestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogErrorSendingDigests(_logger, ex);
            }
        }

        LogWorkerStopping(_logger);
    }

    private async Task SendDigestsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var usersResult = await userService.GetActiveAsync(ct);
        if (!usersResult.IsSuccess)
        {
            LogFetchUsersWarning(_logger, usersResult.Error ?? "Unknown error");
            return;
        }

        if (usersResult.Data is not { } users)
        {
            return;
        }

        foreach (var user in users)
        {
            var digestContent = await BuildDigestContentAsync(user.Id, taskService, notificationService, ct);
            if (!string.IsNullOrEmpty(digestContent))
            {
                await emailService.SendAsync(user.Email, "Qaly daily digest", digestContent, ct);
                LogSentDigest(_logger, user.Email);
            }
        }
    }

    private static async Task<string?> BuildDigestContentAsync(Guid userId, ITaskService taskService, INotificationService notificationService, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var hasContent = false;

        // 1. Tasks: Overdue or due in next 2 days
        var tasksResult = await taskService.GetByAssigneeAsync(userId, page: 1, pageSize: 100, ct: ct);
        if (tasksResult.IsSuccess && tasksResult.Data is { } tasks)
        {
            var now = DateTimeOffset.Now;
            var threshold = now.AddDays(2);
            var relevantTasks = tasks.Items
                .Where(t => t.DueDate.HasValue && (t.DueDate.Value < threshold) && t.Status != "Done" && t.Status != "Completed")
                .ToList();

            if (relevantTasks.Count > 0)
            {
                hasContent = true;
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Tasks requiring attention:");
                foreach (var task in relevantTasks)
                {
                    if (task.DueDate is not { } dueDate)
                    {
                        continue;
                    }

                    var status = dueDate < now ? "[OVERDUE]" : "[UPCOMING]";
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- {status} {task.Title} (Due: {dueDate:yyyy-MM-dd HH:mm})");
                }
            }
        }

        // 2. Unread Notifications
        var notificationsResult = await notificationService.GetByUserAsync(userId, unreadOnly: true, ct: ct);
        if (notificationsResult.IsSuccess && notificationsResult.Data is { Count: > 0 } notifications)
        {
            if (hasContent) sb.AppendLine();
            hasContent = true;
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Recent unread notifications:");
            foreach (var notification in notifications.Take(5))
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- {notification.Message} ({notification.CreatedAt:yyyy-MM-dd HH:mm})");
            }
            if (notifications.Count > 5)
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- ... and {notifications.Count - 5} more.");
            }
        }

        return hasContent ? sb.ToString() : null;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Email Digest Worker is starting.")]
    private static partial void LogWorkerStarting(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Next email digest run scheduled for {NextRun} (in {Delay})")]
    private static partial void LogNextRun(ILogger logger, DateTime nextRun, TimeSpan delay);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error occurred while sending email digests.")]
    private static partial void LogErrorSendingDigests(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Email Digest Worker is stopping.")]
    private static partial void LogWorkerStopping(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Could not fetch active users for email digest: {Message}")]
    private static partial void LogFetchUsersWarning(ILogger logger, string message);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Sent Email Digest to {Email}")]
    private static partial void LogSentDigest(ILogger logger, string email);
}
