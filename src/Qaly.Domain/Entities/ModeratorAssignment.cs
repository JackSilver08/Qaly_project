namespace Qaly.Domain.Entities;

public class ModeratorAssignment : BaseEntity
{
    public Guid ModeratorUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Capability { get; set; } = string.Empty;
    public Guid GrantedByUserId { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? RevokedAt { get; set; }

    public User ModeratorUser { get; set; } = null!;
    public User GrantedByUser { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
