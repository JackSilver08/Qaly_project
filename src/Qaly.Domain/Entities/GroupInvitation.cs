namespace Qaly.Domain.Entities;

public class GroupInvitation : BaseEntity
{
    public Guid WorkGroupId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Guid InvitedByUserId { get; set; }
    public Guid? InvitedUserId { get; set; }

    public WorkGroup WorkGroup { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public User? InvitedUser { get; set; }
}
