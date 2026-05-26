namespace Qaly.Domain.Entities;

public class GroupPollVote : BaseEntity
{
    public Guid GroupPollId { get; set; }
    public Guid GroupPollOptionId { get; set; }
    public Guid UserId { get; set; }

    public GroupPoll GroupPoll { get; set; } = null!;
    public GroupPollOption GroupPollOption { get; set; } = null!;
    public User User { get; set; } = null!;
}
