namespace Qaly.Domain.Entities;

public class AiJobMigrationRecord : BaseEntity
{
    public Guid LegacyQueueItemId { get; set; }
    public Guid? CanonicalAiJobId { get; set; }
    public string Classification { get; set; } = "pending_review";
    public string Reason { get; set; } = string.Empty;
    public string? LegacySnapshotJson { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
}
