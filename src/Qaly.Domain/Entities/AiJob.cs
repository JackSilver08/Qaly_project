namespace Qaly.Domain.Entities;

public class AiJob : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceId { get; set; }
    public string SchemaId { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "4.0";
    public string RequestJson { get; set; } = "{}";
    public string RequestHash { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ProviderHint { get; set; } = "auto";
    public bool Sensitive { get; set; }
    public Guid? ConsentId { get; set; }
    public Guid? RetentionPolicyId { get; set; }
    public Guid? BudgetPolicyId { get; set; }
    public bool CloudEligible { get; set; }
    public string? PolicyDecisionJson { get; set; }
    public DateTimeOffset? PolicyCheckedAt { get; set; }
    public string Status { get; set; } = AiJobStatuses.Queued;
    public string? LegacyStatus { get; set; }
    public int ProgressPercent { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public Guid? CanceledById { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTimeOffset? NextRetryAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public bool LastErrorRetryable { get; set; }
    public string? ResultJson { get; set; }
    public string? ResultHash { get; set; }
    public string? SelectedProvider { get; set; }
    public string? SelectedModel { get; set; }
    public string? ProviderRequestId { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public decimal? ActualCostUsd { get; set; }
    public decimal? MaximumCostUsd { get; set; }
    public string? PricingVersion { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public bool CacheHit { get; set; }
    public bool IsMock { get; set; }
    public string? MockReason { get; set; }
    public Guid? LegacyQueueItemId { get; set; }
    public Guid RequestedById { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Project? Project { get; set; }
    public User RequestedBy { get; set; } = null!;
    public AiJobDispatch? Dispatch { get; set; }
    public ICollection<AiGeneratedDraft> Drafts { get; set; } = new List<AiGeneratedDraft>();
    public ICollection<AiJobSource> Sources { get; set; } = new List<AiJobSource>();
    public ICollection<AiProviderAttempt> ProviderAttempts { get; set; } = new List<AiProviderAttempt>();
    public ICollection<AiJobActivityEvent> ActivityEvents { get; set; } = new List<AiJobActivityEvent>();
    public ICollection<AiUsageLedger> UsageEntries { get; set; } = new List<AiUsageLedger>();
}
