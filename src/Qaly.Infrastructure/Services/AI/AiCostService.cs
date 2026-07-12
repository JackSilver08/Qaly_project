using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public class AiCostService : IAiCostService
{
    private readonly QalyDbContext _context;

    public AiCostService(QalyDbContext context)
    {
        _context = context;
    }

    public async Task<bool> EnsureBudgetAvailableAsync(Guid? tenantId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var policy = await _context.AiBudgetPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProjectId == projectId, cancellationToken);

        if (policy == null) return true; // No limit set

        // Calculate usage for today
        var startOfDay = DateTimeOffset.UtcNow.Date;
        var usageToday = await _context.AiUsageLedger
            .Where(u => u.TenantId == tenantId && u.ProjectId == projectId && u.CreatedAt >= startOfDay)
            .SumAsync(u => u.EstimatedCostUsd, cancellationToken);

        if (policy.HardStopEnabled && usageToday >= policy.DailyBudgetUsd)
        {
            return false;
        }

        return true;
    }

    public async Task RecordUsageAsync(
        Guid? tenantId, 
        Guid? projectId, 
        Guid? userId, 
        string jobType, 
        string providerName, 
        string modelName, 
        int inputTokens, 
        int outputTokens, 
        decimal estimatedCostUsd, 
        int? latencyMs, 
        string status, 
        bool cacheHit,
        CancellationToken cancellationToken = default)
        => await RecordJobUsageAsync(
            tenantId,
            projectId,
            userId,
            jobType,
            providerName,
            modelName,
            inputTokens,
            outputTokens,
            estimatedCostUsd,
            latencyMs,
            status,
            cacheHit,
            cancellationToken: cancellationToken);

    public async Task RecordJobUsageAsync(
        Guid? tenantId,
        Guid? projectId,
        Guid? userId,
        string jobType,
        string providerName,
        string modelName,
        int inputTokens,
        int outputTokens,
        decimal estimatedCostUsd,
        int? latencyMs,
        string status,
        bool cacheHit,
        Guid? aiJobId = null,
        Guid? providerAttemptId = null,
        string? errorCode = null,
        CancellationToken cancellationToken = default)
    {
        if (aiJobId.HasValue && providerAttemptId.HasValue && await _context.AiUsageLedger.AnyAsync(
                entry => entry.AiJobId == aiJobId && entry.ProviderAttemptId == providerAttemptId,
                cancellationToken))
        {
            return;
        }

        var ledgerEntry = new AiUsageLedger
        {
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            AiJobId = aiJobId,
            ProviderAttemptId = providerAttemptId,
            JobType = jobType,
            ProviderName = providerName,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostUsd = estimatedCostUsd,
            LatencyMs = latencyMs,
            Status = status,
            CacheHit = cacheHit,
            ErrorCode = errorCode
        };

        _context.AiUsageLedger.Add(ledgerEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
