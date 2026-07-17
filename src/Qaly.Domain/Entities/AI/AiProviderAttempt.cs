namespace Qaly.Domain.Entities;

public class AiProviderAttempt : BaseEntity
{
    public Guid AiJobId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = AiAttemptStatuses.Running;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ProviderRequestId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public int? LatencyMs { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public decimal? ActualCostUsd { get; set; }
    public bool CacheHit { get; set; }
    public bool IsMock { get; set; }
    public string? MockReason { get; set; }
    public string? RequestHash { get; set; }
    public string? ResponseHash { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Retryable { get; set; }

    public AiJob AiJob { get; set; } = null!;
    public ICollection<AiUsageLedger> UsageEntries { get; set; } = new List<AiUsageLedger>();
}
