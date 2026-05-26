namespace Qaly.Domain.Entities;

public class WorkGroupMember : BaseEntity
{
    public Guid WorkGroupId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public WorkGroup WorkGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
