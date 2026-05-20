namespace Qaly.Domain.Entities;

public class OrganizationMember : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
}
