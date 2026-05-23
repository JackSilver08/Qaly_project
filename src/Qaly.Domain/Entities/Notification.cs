namespace Qaly.Domain.Entities;

public class Notification : BaseEntity
{
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
    public string Tone { get; set; } = "info";
    public bool IsRead { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    // Foreign keys
    public Guid UserId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
