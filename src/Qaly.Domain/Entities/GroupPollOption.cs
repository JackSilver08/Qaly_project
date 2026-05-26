namespace Qaly.Domain.Entities;

public class GroupPollOption : BaseEntity
{
    public Guid GroupPollId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public GroupPoll GroupPoll { get; set; } = null!;
    public ICollection<GroupPollVote> Votes { get; set; } = new List<GroupPollVote>();
}
