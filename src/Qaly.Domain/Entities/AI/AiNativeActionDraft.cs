namespace Qaly.Domain.Entities;

/// <summary>
/// Generic review boundary for capability-specific native actions. The model
/// can only author PayloadJson; the capability, target and source version are
/// server-owned and revalidated immediately before confirmation.
/// </summary>
public sealed class AiNativeActionDraft : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string CapabilityId { get; set; } = string.Empty;
    public string SchemaId { get; set; } = string.Empty;
    public string RendererId { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string SourceVersion { get; set; } = string.Empty;
    public string Status { get; set; } = "pending_review";
    public int Revision { get; set; } = 1;
    public string? ConfirmationIdempotencyKey { get; set; }
    public string? ReceiptJson { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;
    public Project? Project { get; set; }
    public Organization? Organization { get; set; }
}
