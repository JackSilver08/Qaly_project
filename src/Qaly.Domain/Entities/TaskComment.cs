namespace Qaly.Domain.Entities;

public class TaskComment : BaseEntity
{
    public string Content { get; set; } = string.Empty;

    // Foreign keys
    public Guid TaskItemId { get; set; }
    public Guid AuthorId { get; set; }

    // Navigation properties
    public TaskItem TaskItem { get; set; } = null!;
    public User Author { get; set; } = null!;
}
