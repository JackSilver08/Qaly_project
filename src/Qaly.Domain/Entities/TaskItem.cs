using System.ComponentModel.DataAnnotations.Schema;

namespace Qaly.Domain.Entities;

/// <summary>
/// Task entity - đặt tên TaskItem để tránh conflict với System.Threading.Tasks.Task
/// </summary>
public class TaskItem : BaseEntity, ISoftDeleteEntity
{
    /// <summary>
    /// Số thứ tự task tăng dần theo từng project, bất biến sau khi tạo.
    /// Được cấp tập trung trong <c>QalyDbContext.SaveChanges</c>. Kết hợp với
    /// <see cref="Project.Code"/> để tạo Task Key dạng "QALY-142".
    /// </summary>
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Todo";
    public string Priority { get; set; } = "Medium";
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public int? EstimatedHours { get; set; }
    public int? ActualHours { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsPinned { get; set; }
    public bool ContributesToProgress { get; set; } = true;
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Foreign keys
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid ReporterId { get; set; }
    public Guid? ImportSessionId { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public Sprint? Sprint { get; set; }
    public User? Assignee { get; set; }
    public User Reporter { get; set; } = null!;
    public ImportSession? ImportSession { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<TaskAssignment> Assignees { get; set; } = new List<TaskAssignment>();
    public ICollection<TaskLabel> Labels { get; set; } = new List<TaskLabel>();
    public ICollection<TaskDependency> PredecessorDependencies { get; set; } = new List<TaskDependency>();
    public ICollection<TaskDependency> SuccessorDependencies { get; set; } = new List<TaskDependency>();
    public ICollection<TaskViewEvent> ViewEvents { get; set; } = new List<TaskViewEvent>();

    /// <summary>
    /// Task Key hiển thị dạng "QALY-142". Chỉ khả dụng khi navigation
    /// <see cref="Project"/> đã được nạp; ngược lại trả về null để nơi gọi tự
    /// dựng key từ Project.Code khi cần.
    /// </summary>
    [NotMapped]
    public string? Key =>
        Project is not null && !string.IsNullOrEmpty(Project.Code) && Number > 0
            ? $"{Project.Code}-{Number}"
            : null;
}
