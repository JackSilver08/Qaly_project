using System.Threading;
using System.Threading.Tasks;

namespace Qaly.Application.Services;

public interface IAiCostService
{
    /// <summary>
    /// Checks if the given project/tenant has enough budget for another AI request.
    /// Will throw or return false if HardStopEnabled and budget is exceeded.
    /// </summary>
    Task<bool> EnsureBudgetAvailableAsync(Guid? tenantId, Guid? projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the token usage and cost for an AI job into AiUsageLedger.
    /// </summary>
    Task RecordUsageAsync(
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
        CancellationToken cancellationToken = default);

    Task RecordJobUsageAsync(
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
        CancellationToken cancellationToken = default);
}
