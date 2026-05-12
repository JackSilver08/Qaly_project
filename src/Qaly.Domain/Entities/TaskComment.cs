namespace Qaly.Domain.Entities;

public class TaskComment : BaseEntity
{
    public string Content { get; set; } = string.Empty;
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }

    // Foreign keys
    public Guid TaskItemId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ParentCommentId { get; set; }

    // Navigation properties
    public TaskItem TaskItem { get; set; } = null!;
    public User Author { get; set; } = null!;
    public TaskComment? ParentComment { get; set; }
    public ICollection<TaskComment> Replies { get; set; } = new List<TaskComment>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
}
