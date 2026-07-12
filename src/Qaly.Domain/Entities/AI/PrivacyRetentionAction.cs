namespace Qaly.Domain.Entities;

public class PrivacyRetentionAction : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid RetentionPolicyId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ActionType { get; set; } = PrivacyExpiryActions.Redact;
    public string Status { get; set; } = PrivacyWorkerStatuses.Pending;
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? EvidenceJson { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public RetentionPolicy RetentionPolicy { get; set; } = null!;
}
