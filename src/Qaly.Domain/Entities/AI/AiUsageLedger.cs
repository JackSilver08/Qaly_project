using System;

namespace Qaly.Domain.Entities;

public class AiUsageLedger : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public long? JobId { get; set; }
    public string JobType { get; set; } = null!;
    public string ProviderName { get; set; } = null!;
    public string ModelName { get; set; } = null!;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public int? LatencyMs { get; set; }
    public string Status { get; set; } = null!;
    public bool CacheHit { get; set; }
    public string? PromptHash { get; set; }
    public string? ResponseHash { get; set; }
    public string? ErrorCode { get; set; }
}