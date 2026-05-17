namespace Qaly.Domain.Entities;

public class TaskAttentionSignal : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid UserId { get; set; }
    public string SignalType { get; set; } = string.Empty;
    public DateTimeOffset FirstDetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSentAt { get; set; }
    public int CooldownHours { get; set; } = 24;
    public DateTimeOffset? ResolvedAt { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
    public User User { get; set; } = null!;
}
