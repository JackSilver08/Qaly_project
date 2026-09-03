namespace Qaly.Domain.Entities;

public class VectorSyncOutbox : BaseEntity
{
    /// <summary>Database-generated total order used to serialize one aggregate's events.</summary>
    public long SequenceNumber { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string Payload { get; set; } = string.Empty; // JSON evidence retained for diagnostics.

    /// <summary>Number of processing attempts, including a reclaimed crashed attempt.</summary>
    public int RetryCount { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class VectorSyncAggregateTypes
{
    public const string Project = "Project";
    public const string Task = "Task";
    public const string Comment = "Comment";
    public const string Attachment = "Attachment";
}

public static class VectorSyncEventTypes
{
    public const string ProjectCreated = "ProjectCreated";
    public const string ProjectUpdated = "ProjectUpdated";
    public const string ProjectDeleted = "ProjectDeleted";
    public const string TaskCreated = "TaskCreated";
    public const string TaskUpdated = "TaskUpdated";
    public const string TaskDeleted = "TaskDeleted";
    public const string CommentAdded = "CommentAdded";
    public const string CommentUpdated = "CommentUpdated";
    public const string CommentDeleted = "CommentDeleted";
    public const string TaskAttachmentCreated = "TaskAttachmentCreated";
    public const string TaskAttachmentUpdated = "TaskAttachmentUpdated";
    public const string TaskAttachmentDeleted = "TaskAttachmentDeleted";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        ProjectCreated,
        ProjectUpdated,
        ProjectDeleted,
        TaskCreated,
        TaskUpdated,
        TaskDeleted,
        CommentAdded,
        CommentUpdated,
        CommentDeleted,
        TaskAttachmentCreated,
        TaskAttachmentUpdated,
        TaskAttachmentDeleted
    };
}
