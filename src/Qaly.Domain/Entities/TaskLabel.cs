namespace Qaly.Domain.Entities;

public class TaskLabel : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid ProjectLabelId { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
    public ProjectLabel ProjectLabel { get; set; } = null!;
}
