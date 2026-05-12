namespace Qaly.Domain.Entities;

public class TaskAttachment : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Scope { get; set; } = "Task";

    // Foreign keys
    public Guid? TaskItemId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid UploadedById { get; set; }

    // Navigation properties
    public TaskItem? TaskItem { get; set; }
    public Project? Project { get; set; }
    public TaskComment? Comment { get; set; }
    public User UploadedBy { get; set; } = null!;
}
