using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Services;
using System.Text;

namespace Qaly.Infrastructure.Services;

public class EmailDigestWorker : BackgroundService
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
        _logger.LogInformation("Email Digest Worker is starting.");

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
            _logger.LogInformation("Next email digest run scheduled for {NextRun} (in {Delay})", nextRun, delay);

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
                _logger.LogError(ex, "Error occurred while sending email digests.");
            }
        }

        _logger.LogInformation("Email Digest Worker is stopping.");
    }

    private async Task SendDigestsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var usersResult = await userService.GetActiveAsync(ct);
        if (!usersResult.IsSuccess)
        {
            _logger.LogWarning("Could not fetch active users for email digest: {Message}", usersResult.Message);
            return;
        }

        foreach (var user in usersResult.Value)
        {
            var digestContent = await BuildDigestContentAsync(user.Id, taskService, notificationService, ct);
            if (!string.IsNullOrEmpty(digestContent))
            {
                _logger.LogInformation("Sending Email Digest to {Email}: {Content}", user.Email, digestContent);
            }
        }
    }

    private async Task<string?> BuildDigestContentAsync(Guid userId, ITaskService taskService, INotificationService notificationService, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var hasContent = false;

        // 1. Tasks: Overdue or due in next 2 days
        var tasksResult = await taskService.GetByAssigneeAsync(userId, page: 1, pageSize: 100, ct: ct);
        if (tasksResult.IsSuccess)
        {
            var now = DateTimeOffset.Now;
            var threshold = now.AddDays(2);
            var relevantTasks = tasksResult.Value.Items
                .Where(t => t.DueDate.HasValue && (t.DueDate.Value < threshold) && t.Status != "Done" && t.Status != "Completed")
                .ToList();

            if (relevantTasks.Any())
            {
                hasContent = true;
                sb.AppendLine("Tasks requiring attention:");
                foreach (var task in relevantTasks)
                {
                    var status = task.DueDate.Value < now ? "[OVERDUE]" : "[UPCOMING]";
                    sb.AppendLine($"- {status} {task.Title} (Due: {task.DueDate:yyyy-MM-dd HH:mm})");
                }
            }
        }

        // 2. Unread Notifications
        var notificationsResult = await notificationService.GetByUserAsync(userId, unreadOnly: true, ct: ct);
        if (notificationsResult.IsSuccess && notificationsResult.Value.Any())
        {
            if (hasContent) sb.AppendLine();
            hasContent = true;
            sb.AppendLine("Recent unread notifications:");
            foreach (var notification in notificationsResult.Value.Take(5))
            {
                sb.AppendLine($"- {notification.Message} ({notification.CreatedAt:yyyy-MM-dd HH:mm})");
            }
            if (notificationsResult.Value.Count > 5)
            {
                sb.AppendLine($"- ... and {notificationsResult.Value.Count - 5} more.");
            }
        }

        return hasContent ? sb.ToString() : null;
    }
}
