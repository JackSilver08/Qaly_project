using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed class AiJobQueueHealthCheck : IHealthCheck
{
    private readonly QalyDbContext _db;
    private readonly IConfiguration _configuration;

    public AiJobQueueHealthCheck(QalyDbContext db, IConfiguration configuration)
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
            var enabled = _configuration.GetValue<bool>("AiJobsV4:Enabled");
            if (!enabled)
            {
                return HealthCheckResult.Healthy(
                    "AI job processing is explicitly disabled.",
                    new Dictionary<string, object> { ["enabled"] = false });
            }

            var now = DateTimeOffset.UtcNow;
            var workerEnabled = _configuration.GetValue<bool>("AiJobsV4:WorkerEnabled");
            var pendingThreshold = Math.Max(
                1,
                _configuration.GetValue("OperationalHealth:AiJobPendingThreshold", 200));
            var maxPendingAgeMinutes = Math.Max(
                1,
                _configuration.GetValue("OperationalHealth:AiJobMaxPendingAgeMinutes", 15));
            var queue = _db.AiJobDispatches.AsNoTracking();
            var pending = await queue.CountAsync(
                dispatch => dispatch.CompletedAt == null,
                cancellationToken);
            var expiredLeases = await queue.CountAsync(
                dispatch => dispatch.CompletedAt == null &&
                    dispatch.LeaseExpiresAt != null &&
                    dispatch.LeaseExpiresAt <= now,
                cancellationToken);
            var failedLast24Hours = await _db.AiJobs.AsNoTracking().CountAsync(
                job => job.Status == AiJobStatuses.Failed && job.FinishedAt >= now.AddHours(-24),
                cancellationToken);
            var oldestPendingAt = await queue
                .Where(dispatch => dispatch.CompletedAt == null)
                .MinAsync(dispatch => (DateTimeOffset?)dispatch.AvailableAt, cancellationToken);
            var oldestPendingAgeMinutes = oldestPendingAt.HasValue
                ? Math.Max(0, (now - oldestPendingAt.Value).TotalMinutes)
                : 0;
            var data = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["workerEnabled"] = workerEnabled,
                ["pending"] = pending,
                ["expiredLeases"] = expiredLeases,
                ["failedLast24Hours"] = failedLast24Hours,
                ["oldestPendingAgeMinutes"] = Math.Round(oldestPendingAgeMinutes, 2)
            };

            if (!workerEnabled ||
                expiredLeases > 0 ||
                pending >= pendingThreshold ||
                oldestPendingAgeMinutes >= maxPendingAgeMinutes)
            {
                return HealthCheckResult.Degraded(
                    "AI job queue requires operator attention.",
                    data: data);
            }

            return HealthCheckResult.Healthy("AI job queue is within thresholds.", data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("AI job queue health query failed.", exception);
        }
    }
}
