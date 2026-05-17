namespace Qaly.Domain.Entities;

/// <summary>
/// Tracks a CSV/XLSX import operation for rollback support.
/// Each imported TaskItem links back via ImportSessionId.
/// </summary>
public class ImportSession : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public bool IsUndone { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<TaskItem> ImportedTasks { get; set; } = new List<TaskItem>();
}
