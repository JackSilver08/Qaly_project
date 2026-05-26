namespace Qaly.Domain.Entities;

public class GroupPoll : BaseEntity
{
    public Guid WorkGroupId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool AllowMultiple { get; set; }
    public string Status { get; set; } = "Open";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public WorkGroup WorkGroup { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<GroupPollOption> Options { get; set; } = new List<GroupPollOption>();
    public ICollection<GroupPollVote> Votes { get; set; } = new List<GroupPollVote>();
}
