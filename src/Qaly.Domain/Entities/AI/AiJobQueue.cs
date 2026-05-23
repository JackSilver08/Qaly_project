using System;

namespace Qaly.Domain.Entities;

public class AiJobItem : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? RequestedBy { get; set; }
    public string JobType { get; set; } = null!;
    public string SchemaId { get; set; } = null!;
    public string Status { get; set; } = "queued"; // queued|running|succeeded|failed|retrying|canceled
    public int Priority { get; set; } = 100;
    public bool Sensitive { get; set; }
    public long? ConsentId { get; set; }
    public string? ProviderHint { get; set; }
    public string? InputRefType { get; set; }
    public long? InputRefId { get; set; }
    public string? PayloadJson { get; set; }
    public string? ResultJson { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetry { get; set; } = 1;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public Guid? CanceledBy { get; set; }
}