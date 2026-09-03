using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed class PrivacyWorkQueueHealthCheck : IHealthCheck
{
    private readonly QalyDbContext _db;
    private readonly IConfiguration _configuration;

    public PrivacyWorkQueueHealthCheck(QalyDbContext db, IConfiguration configuration)
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
            var enabled = _configuration.GetValue<bool>("PrivacyV4:Enabled");
            if (!enabled)
            {
                return HealthCheckResult.Healthy(
                    "Privacy work processing is explicitly disabled.",
                    new Dictionary<string, object> { ["enabled"] = false });
            }

            var now = DateTimeOffset.UtcNow;
            var workerEnabled = _configuration.GetValue<bool>("PrivacyV4:WorkerEnabled");
            var pendingThreshold = Math.Max(
                1,
                _configuration.GetValue("OperationalHealth:PrivacyWorkPendingThreshold", 200));
            var maxPendingAgeMinutes = Math.Max(
                1,
                _configuration.GetValue("OperationalHealth:PrivacyWorkMaxPendingAgeMinutes", 15));
            var retention = _db.PrivacyRetentionActions.AsNoTracking();
            var dsar = _db.DataSubjectRequests.AsNoTracking();
            var pendingRetention = await retention.CountAsync(
                item => item.Status == PrivacyWorkerStatuses.Pending ||
                    item.Status == PrivacyWorkerStatuses.Running,
                cancellationToken);
            var pendingDsar = await dsar.CountAsync(
                item => item.Status == DataSubjectRequestStatuses.Accepted ||
                    item.Status == DataSubjectRequestStatuses.Collecting,
                cancellationToken);
            var failedRetention = await retention.CountAsync(
                item => item.Status == PrivacyWorkerStatuses.Failed,
                cancellationToken);
            var failedDsar = await dsar.CountAsync(
                item => item.Status == DataSubjectRequestStatuses.Failed,
                cancellationToken);
            var expiredRetentionLeases = await retention.CountAsync(
                item => item.Status == PrivacyWorkerStatuses.Running &&
                    item.LeaseExpiresAt != null &&
                    item.LeaseExpiresAt <= now,
                cancellationToken);
            var expiredDsarLeases = await dsar.CountAsync(
                item => item.Status == DataSubjectRequestStatuses.Collecting &&
                    item.LeaseExpiresAt != null &&
                    item.LeaseExpiresAt <= now,
                cancellationToken);
            var overdueDsar = await dsar.CountAsync(
                item => (item.Status == DataSubjectRequestStatuses.Accepted ||
                    item.Status == DataSubjectRequestStatuses.Collecting) &&
                    item.DeadlineAt != null &&
                    item.DeadlineAt <= now,
                cancellationToken);
            var oldestRetentionAt = await retention
                .Where(item => item.Status == PrivacyWorkerStatuses.Pending)
                .MinAsync(item => (DateTimeOffset?)item.AvailableAt, cancellationToken);
            var oldestDsarAt = await dsar
                .Where(item => item.Status == DataSubjectRequestStatuses.Accepted)
                .MinAsync(item => (DateTimeOffset?)item.AvailableAt, cancellationToken);
            var oldestPendingAt = new[] { oldestRetentionAt, oldestDsarAt }
                .Where(value => value.HasValue)
                .Min();
            var oldestPendingAgeMinutes = oldestPendingAt.HasValue
                ? Math.Max(0, (now - oldestPendingAt.Value).TotalMinutes)
                : 0;
            var pending = pendingRetention + pendingDsar;
            var failed = failedRetention + failedDsar;
            var expiredLeases = expiredRetentionLeases + expiredDsarLeases;
            var data = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["workerEnabled"] = workerEnabled,
                ["pendingRetention"] = pendingRetention,
                ["pendingDsar"] = pendingDsar,
                ["failed"] = failed,
                ["expiredLeases"] = expiredLeases,
                ["overdueDsar"] = overdueDsar,
                ["oldestPendingAgeMinutes"] = Math.Round(oldestPendingAgeMinutes, 2)
            };

            if (!workerEnabled ||
                failed > 0 ||
                expiredLeases > 0 ||
                overdueDsar > 0 ||
                pending >= pendingThreshold ||
                oldestPendingAgeMinutes >= maxPendingAgeMinutes)
            {
                return HealthCheckResult.Degraded(
                    "Privacy work queue requires operator attention.",
                    data: data);
            }

            return HealthCheckResult.Healthy("Privacy work queue is within thresholds.", data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Privacy work queue health query failed.", exception);
        }
    }
}
