namespace Qaly.Domain.Entities;

public class PrivacyLegalHold : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Status { get; set; } = PrivacyLegalHoldStatuses.Active;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset HeldAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid HeldById { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public Guid? ReleasedById { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
