namespace Qaly.Domain.Entities;

public class GroupPollVote : BaseEntity
{
    public Guid PollId { get; set; }
    public Guid OptionId { get; set; }
    public Guid UserId { get; set; }

    public GroupPoll Poll { get; set; } = null!;
    public GroupPollOption Option { get; set; } = null!;
    public User User { get; set; } = null!;
}
