using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Infrastructure.Services;

public class OutboxHealthCheck : IHealthCheck
{
    private readonly IRepository<VectorSyncOutbox> _outboxRepo;

    public OutboxHealthCheck(IRepository<VectorSyncOutbox> outboxRepo)
    {
        _outboxRepo = outboxRepo;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = await _outboxRepo.GetQueryable()
                .CountAsync(m => m.ProcessedAt == null, cancellationToken);

            if (pending > 200)
            {
                return HealthCheckResult.Unhealthy($"High pending outbox messages: {pending}");
            }

            return HealthCheckResult.Healthy($"Pending outbox messages: {pending}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Outbox health check failed", ex);
        }
    }
}
