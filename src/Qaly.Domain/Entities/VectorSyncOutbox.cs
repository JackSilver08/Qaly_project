namespace Qaly.Domain.Entities;

public class VectorSyncOutbox : BaseEntity
{
    public string EventType { get; set; } = string.Empty; // e.g., TaskCreated, TaskDeleted
    public string Payload { get; set; } = string.Empty; // JSON { "Id": "..." }
    public int RetryCount { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
