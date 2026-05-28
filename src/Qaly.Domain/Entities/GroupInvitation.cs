using Qaly.Domain.Enums;

namespace Qaly.Domain.Entities;

public class GroupInvitation : BaseEntity
{
    public Guid GroupId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public GroupInvitationStatus Status { get; set; } = GroupInvitationStatus.Pending;
    public DateTimeOffset ExpiredAt { get; set; }

    public WorkGroup Group { get; set; } = null!;
}
