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
            .Where(p => p.ProjectId == projectId || (p.ProjectId == null && p.TenantId == tenantId))
            .OrderByDescending(p => p.ProjectId == projectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy == null) return true; // No limit set

        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var usageQuery = _context.AiUsageLedger.Where(u => u.CreatedAt >= startOfMonth);
        usageQuery = policy.ProjectId.HasValue
            ? usageQuery.Where(u => u.ProjectId == policy.ProjectId)
            : usageQuery.Where(u => u.TenantId == policy.TenantId);
        var usage = await usageQuery
            .Select(u => new { u.CreatedAt, Cost = u.ActualCostUsd ?? u.EstimatedCostUsd })
            .ToListAsync(cancellationToken);
        var usageToday = usage.Where(item => item.CreatedAt >= startOfDay).Sum(item => item.Cost);
        var usageMonth = usage.Sum(item => item.Cost);

        if (policy.HardStopEnabled &&
            (usageToday >= policy.DailyBudgetUsd || usageMonth >= policy.MonthlyBudgetUsd))
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
