namespace Qaly.Domain.Entities;

public class TaskAssignment : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid UserId { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
    public User User { get; set; } = null!;
}
