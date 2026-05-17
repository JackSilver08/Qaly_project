namespace Qaly.Domain.Entities;

public class TaskViewEvent : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;
    public int ViewCount { get; set; } = 1;

    public TaskItem TaskItem { get; set; } = null!;
    public User User { get; set; } = null!;
}
