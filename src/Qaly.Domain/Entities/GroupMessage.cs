namespace Qaly.Domain.Entities;

public class GroupMessage : BaseEntity, ISoftDeleteEntity
{
    public Guid WorkGroupId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "Text";
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }

    public WorkGroup WorkGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
