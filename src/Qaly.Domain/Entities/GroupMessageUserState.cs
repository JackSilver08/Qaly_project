namespace Qaly.Domain.Entities;

public class GroupMessageUserState : BaseEntity
{
    public Guid GroupMessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset? HiddenAt { get; set; }

    public GroupMessage GroupMessage { get; set; } = null!;
    public User User { get; set; } = null!;
}
