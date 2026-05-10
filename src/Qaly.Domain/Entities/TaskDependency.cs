namespace Qaly.Domain.Entities;

public class TaskDependency : BaseEntity
{
    public Guid PredecessorId { get; set; }
    public Guid SuccessorId { get; set; }
    
    /// <summary>
    /// Type of dependency, e.g., "FinishToStart", "StartToStart", etc.
    /// Default is "FinishToStart"
    /// </summary>
    public string DependencyType { get; set; } = "FinishToStart";

    public TaskItem Predecessor { get; set; } = null!;
    public TaskItem Successor { get; set; } = null!;
}
