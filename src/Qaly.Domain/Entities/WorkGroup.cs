namespace Qaly.Domain.Entities;

public class WorkGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = "Active";

    public Guid OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }

    public User Owner { get; set; } = null!;
    public Organization? Organization { get; set; }
    public ICollection<WorkGroupMember> Members { get; set; } = new List<WorkGroupMember>();
    public ICollection<GroupInvitation> Invitations { get; set; } = new List<GroupInvitation>();
    public ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    public ICollection<GroupPoll> Polls { get; set; } = new List<GroupPoll>();
    public ICollection<GroupMeetingSession> MeetingSessions { get; set; } = new List<GroupMeetingSession>();
    public ICollection<Project> CreatedProjects { get; set; } = new List<Project>();
}
