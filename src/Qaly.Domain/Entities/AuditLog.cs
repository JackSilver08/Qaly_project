namespace Qaly.Domain.Entities;

/// <summary>
/// Audit Log - dùng BIGINT IDENTITY thay vì GUID (vì volume lớn).
/// ChangesJson thay thế JSONB của Postgres.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? ChangesJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    // Foreign keys
    public Guid? UserId { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
