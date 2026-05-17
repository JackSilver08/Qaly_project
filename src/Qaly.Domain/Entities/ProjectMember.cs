namespace Qaly.Domain.Entities;

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool CanViewProjectTimeline { get; set; }
    public bool CanViewTaskRisk { get; set; }
    public bool CanNudgeAssignee { get; set; }
    public bool CanViewUnseenTaskSignal { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
}
