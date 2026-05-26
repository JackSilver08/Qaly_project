namespace Qaly.Domain.Entities;

public class GroupMeetingSession : BaseEntity
{
    public Guid WorkGroupId { get; set; }
    public Guid StartedByUserId { get; set; }
    public string Provider { get; set; } = "External";
    public string RoomId { get; set; } = string.Empty;
    public string? JoinUrl { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public string? TranscriptSourceId { get; set; }
    public string? Summary { get; set; }

    public WorkGroup WorkGroup { get; set; } = null!;
    public User StartedByUser { get; set; } = null!;
}
