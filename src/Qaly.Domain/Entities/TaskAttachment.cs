namespace Qaly.Domain.Entities;

public class TaskAttachment : BaseEntity, ISoftDeleteEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Scope { get; set; } = "Task";
    public bool IsEvidence { get; set; }
    public string EvidenceApprovalStatus { get; set; } = "None";
    public Guid? EvidenceReviewedById { get; set; }
    public DateTimeOffset? EvidenceReviewedAt { get; set; }
    public string? EvidenceReviewNote { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

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
    public User? EvidenceReviewedBy { get; set; }
}
