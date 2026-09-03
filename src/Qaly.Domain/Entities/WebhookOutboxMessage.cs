namespace Qaly.Domain.Entities;

/// <summary>
/// Durable business-event handoff for outbound project webhooks. The row is written in the
/// same database transaction as the canonical mutation and is only completed after every
/// matching active subscription has a canonical delivery receipt.
/// </summary>
public class WebhookOutboxMessage : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public int RetryCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }
    public string? ErrorMessage { get; set; }
}
