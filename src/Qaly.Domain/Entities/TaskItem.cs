namespace Qaly.Domain.Entities;

/// <summary>
/// Task entity - đặt tên TaskItem để tránh conflict với System.Threading.Tasks.Task
/// </summary>
public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Todo";
    public string Priority { get; set; } = "Medium";
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public int? EstimatedHours { get; set; }
    public int? ActualHours { get; set; }
    public bool IsPrivate { get; set; }
    public int SortOrder { get; set; }

    // Foreign keys
    public Guid ProjectId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid ReporterId { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public User? Assignee { get; set; }
    public User Reporter { get; set; } = null!;
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<TaskDependency> PredecessorDependencies { get; set; } = new List<TaskDependency>();
    public ICollection<TaskDependency> SuccessorDependencies { get; set; } = new List<TaskDependency>();
}
