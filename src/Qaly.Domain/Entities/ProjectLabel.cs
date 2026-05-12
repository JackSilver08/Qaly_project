namespace Qaly.Domain.Entities;

public class ProjectLabel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748B";

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<TaskLabel> Tasks { get; set; } = new List<TaskLabel>();
}
