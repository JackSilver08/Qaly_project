namespace Qaly.Domain.Entities;

public class TaskAssignment : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AssignedByUserId { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? AssignedByUser { get; set; }
}
