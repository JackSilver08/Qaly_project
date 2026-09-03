using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed class WebhookOutboxHealthCheck : IHealthCheck
{
    private readonly QalyDbContext _db;
    private readonly IConfiguration _configuration;

    public WebhookOutboxHealthCheck(QalyDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var pendingThreshold = Math.Max(
                1,
                _configuration.GetValue("OperationalHealth:WebhookOutboxPendingThreshold", 200));
            var maxPendingAgeMinutes = Math.Max(
                1,
                _configuration.GetValue("OperationalHealth:WebhookOutboxMaxPendingAgeMinutes", 15));

            var snapshot = await _db.WebhookOutboxMessages
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Pending = group.Count(message =>
                        message.ProcessedAt == null && message.DeadLetteredAt == null),
                    DeadLetters = group.Count(message => message.DeadLetteredAt != null),
                    OldestPendingAt = group
                        .Where(message =>
                            message.ProcessedAt == null && message.DeadLetteredAt == null)
                        .Min(message => (DateTimeOffset?)message.CreatedAt)
                })
                .SingleOrDefaultAsync(cancellationToken);

            var pending = snapshot?.Pending ?? 0;
            var deadLetters = snapshot?.DeadLetters ?? 0;
            var oldestPendingMinutes = snapshot?.OldestPendingAt is { } oldest
                ? Math.Max(0, (now - oldest).TotalMinutes)
                : 0;
            var data = new Dictionary<string, object>
            {
                ["pending"] = pending,
                ["deadLetters"] = deadLetters,
                ["oldestPendingMinutes"] = Math.Round(oldestPendingMinutes, 2)
            };

            if (deadLetters > 0 || pending >= pendingThreshold || oldestPendingMinutes >= maxPendingAgeMinutes)
            {
                return HealthCheckResult.Degraded(
                    "Webhook outbox requires operator attention.",
                    data: data);
            }

            return HealthCheckResult.Healthy("Webhook outbox is within thresholds.", data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Webhook outbox health query failed.",
                exception);
        }
    }
}
