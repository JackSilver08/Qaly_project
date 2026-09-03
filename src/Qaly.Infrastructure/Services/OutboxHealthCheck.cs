using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Infrastructure.Services;

public class OutboxHealthCheck : IHealthCheck
{
    private readonly IRepository<VectorSyncOutbox> _outboxRepo;
    private readonly IConfiguration _configuration;

    public OutboxHealthCheck(
        IRepository<VectorSyncOutbox> outboxRepo,
        IConfiguration configuration)
    {
        _outboxRepo = outboxRepo;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_configuration.GetValue<bool>("Ai:SemanticEnabled"))
            {
                return HealthCheckResult.Healthy(
                    "Vector synchronization is explicitly disabled.",
                    new Dictionary<string, object> { ["enabled"] = false });
            }

            var outbox = _outboxRepo.GetQueryable().AsNoTracking();
            var pending = await outbox.CountAsync(
                item => item.ProcessedAt == null && item.DeadLetteredAt == null,
                cancellationToken);
            var deadLettered = await outbox.CountAsync(
                item => item.DeadLetteredAt != null,
                cancellationToken);
            var oldestPendingAt = await outbox
                .Where(item => item.ProcessedAt == null && item.DeadLetteredAt == null)
                .OrderBy(item => item.SequenceNumber)
                .Select(item => (DateTimeOffset?)item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var oldestPendingAgeMinutes = oldestPendingAt.HasValue
                ? Math.Max(0, (DateTimeOffset.UtcNow - oldestPendingAt.Value).TotalMinutes)
                : 0;
            var pendingThreshold = _configuration.GetValue(
                "OperationalHealth:VectorOutboxPendingThreshold",
                200);
            var maxPendingAgeMinutes = _configuration.GetValue(
                "OperationalHealth:VectorOutboxMaxPendingAgeMinutes",
                15);
            var data = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["pending"] = pending,
                ["deadLettered"] = deadLettered,
                ["oldestPendingAgeMinutes"] = Math.Round(oldestPendingAgeMinutes, 2)
            };

            if (deadLettered > 0 ||
                pending > pendingThreshold ||
                oldestPendingAgeMinutes > maxPendingAgeMinutes)
            {
                return HealthCheckResult.Degraded(
                    "Vector outbox requires operator attention.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Vector outbox is within thresholds.",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Outbox health check failed", ex);
        }
    }
}
