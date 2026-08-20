using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
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
            try
            {
                await SendDigestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogErrorSendingDigests(_logger, ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        LogWorkerStopping(_logger);
    }

    internal async Task SendDigestsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAiNativeAuthorizationService>();
        var now = DateTimeOffset.UtcNow;
        var staleClaim = now.AddMinutes(-30);

        var dueIds = await db.ProjectDigestSubscriptions.AsNoTracking()
            .Where(item => item.IsEnabled && item.NextDeliveryAt != null && item.NextDeliveryAt <= now &&
                (item.LastDeliveryStatus != "sending" || item.LastAttemptAt == null || item.LastAttemptAt < staleClaim))
            .OrderBy(item => item.NextDeliveryAt)
            .Select(item => item.Id)
            .Take(100)
            .ToListAsync(ct);

        foreach (var subscriptionId in dueIds)
        {
            var subscription = await db.ProjectDigestSubscriptions
                .Include(item => item.User)
                .Include(item => item.Project).ThenInclude(project => project.Organization)
                .SingleOrDefaultAsync(item => item.Id == subscriptionId, ct);
            if (subscription == null || !subscription.IsEnabled || subscription.NextDeliveryAt is not { } scheduledAt || scheduledAt > DateTimeOffset.UtcNow)
                continue;

            var systemTier = await authorization.ResolveSystemTierAsync(
                subscription.UserId, subscription.User.Role, ct);
            var projectPermission = await authorization.ResolveProjectAsync(
                subscription.Project,
                subscription.UserId,
                ProjectRoleRules.IsSystemAdmin(subscription.User.Role),
                ct);
            if (systemTier == AiNativeSystemTier.Restricted || !projectPermission.CanRead)
            {
                // A scheduled external effect must re-check the same authorization
                // boundary as interactive reads. Removing a member or restricting AI
                // access therefore stops future delivery instead of leaking stale data.
                subscription.IsEnabled = false;
                subscription.NextDeliveryAt = null;
                subscription.LastDeliveryStatus = "access_revoked";
                subscription.LastError = "Digest delivery stopped because current access no longer permits this Project.";
                subscription.LastAttemptAt = DateTimeOffset.UtcNow;
                subscription.Revision++;
                await db.SaveChangesAsync(ct);
                continue;
            }

            var deliveryKey = subscription.LastDeliveryStatus == "retry" && !string.IsNullOrWhiteSpace(subscription.LastDeliveryKey)
                ? subscription.LastDeliveryKey
                : $"project-digest:{subscription.Id}:{scheduledAt.UtcTicks}";
            subscription.LastDeliveryKey = deliveryKey;
            subscription.LastDeliveryStatus = "sending";
            subscription.LastAttemptAt = DateTimeOffset.UtcNow;
            subscription.LastError = null;
            subscription.Revision++;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                continue;
            }

            try
            {
                var digestContent = await BuildDigestContentAsync(
                    db,
                    subscription,
                    projectPermission.CanManage,
                    ct);
                await emailService.SendAsync(subscription.User.Email,
                    $"Qaly weekly digest · {subscription.Project.Name}", digestContent, ct);
                subscription.LastDeliveryAt = DateTimeOffset.UtcNow;
                subscription.LastDeliveryStatus = "delivered";
                subscription.LastError = null;
                subscription.ConsecutiveFailureCount = 0;
                subscription.NextDeliveryAt = NextScheduledDelivery(subscription, DateTimeOffset.UtcNow);
                subscription.Revision++;
                await db.SaveChangesAsync(ct);
                LogSentDigest(_logger, subscription.User.Email);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                subscription.LastDeliveryStatus = "retry";
                subscription.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                subscription.ConsecutiveFailureCount++;
                var retryMinutes = Math.Min(60, 5 * (1 << Math.Min(4, subscription.ConsecutiveFailureCount - 1)));
                subscription.NextDeliveryAt = DateTimeOffset.UtcNow.AddMinutes(retryMinutes);
                subscription.Revision++;
                await db.SaveChangesAsync(ct);
                LogDigestRetry(_logger, subscription.Id, deliveryKey, retryMinutes, ex.Message);
            }
        }
    }

    private static async Task<string> BuildDigestContentAsync(
        QalyDbContext db,
        ProjectDigestSubscription subscription,
        bool canManageProject,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var tasks = await db.TaskItems.AsNoTracking()
            .Where(task => task.ProjectId == subscription.ProjectId &&
                (canManageProject || task.AssigneeId == subscription.UserId ||
                    task.Assignees.Any(item => item.UserId == subscription.UserId)))
            .OrderBy(task => task.DueDate)
            .Take(100)
            .Select(task => new { task.Id, task.Title, task.Status, task.DueDate })
            .ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var open = tasks.Where(task => task.Status is not "Done" and not "Completed" and not "Cancelled").ToList();
        var overdue = open.Where(task => task.DueDate < now).ToList();

        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Project: {subscription.Project.Name}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"Assigned tasks: {tasks.Count}; open: {open.Count}; overdue: {overdue.Count}.");
        if (open.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Tasks requiring attention:");
            foreach (var task in open.Take(20))
            {
                var due = task.DueDate.HasValue
                    ? task.DueDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                    : "no deadline";
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"- [{task.Status}] {task.Title} · {due} · /projects/{subscription.ProjectId}/tasks/{task.Id}");
            }
        }
        sb.AppendLine();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Open Project: /projects/{subscription.ProjectId}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Delivery key: {subscription.LastDeliveryKey}");
        return sb.ToString();
    }

    private static DateTimeOffset NextScheduledDelivery(ProjectDigestSubscription subscription, DateTimeOffset after)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(subscription.TimeZoneId);
        }
        catch
        {
            zone = TimeZoneInfo.CreateCustomTimeZone("Qaly-SE-Asia", TimeSpan.FromHours(7), "Qaly SE Asia", "Qaly SE Asia");
        }
        var local = TimeZoneInfo.ConvertTime(after, zone);
        var days = (subscription.DayOfWeek - (int)local.DayOfWeek + 7) % 7;
        var candidate = local.Date.AddDays(days).AddMinutes(subscription.LocalTimeMinutes);
        if (candidate <= local.DateTime) candidate = candidate.AddDays(7);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), zone);
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

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Digest {SubscriptionId} ({DeliveryKey}) will retry in {RetryMinutes} minutes: {Message}")]
    private static partial void LogDigestRetry(ILogger logger, Guid subscriptionId, string deliveryKey, int retryMinutes, string message);
}
