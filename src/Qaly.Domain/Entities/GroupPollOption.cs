namespace Qaly.Domain.Entities;

public class GroupPollOption : BaseEntity
{
    public Guid PollId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public GroupPoll Poll { get; set; } = null!;
    public ICollection<GroupPollVote> Votes { get; set; } = new List<GroupPollVote>();
}
