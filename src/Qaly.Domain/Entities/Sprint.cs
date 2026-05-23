namespace Qaly.Domain.Entities;

using Qaly.Domain.Enums;

public class Sprint : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string Status { get; set; } = "Planning";
    public string? Goal { get; set; }

    // Foreign keys
    public Guid ProjectId { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
