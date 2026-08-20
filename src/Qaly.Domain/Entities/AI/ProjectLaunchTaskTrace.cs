namespace Qaly.Domain.Entities;

/// <summary>
/// Durable business trace from a canonical Task back to the reviewed Project Launch intent.
/// This stores identifiers only; user-facing labels remain in the versioned Launch Brief.
/// </summary>
public sealed class ProjectLaunchTaskTrace : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid ProjectLaunchBriefId { get; set; }
    public string SprintClientId { get; set; } = string.Empty;
    public string TaskClientId { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;
    public string ObjectiveMetricIdsJson { get; set; } = "[]";
    public string SourceRefsJson { get; set; } = "[]";

    public TaskItem TaskItem { get; set; } = null!;
    public ProjectLaunchBrief ProjectLaunchBrief { get; set; } = null!;
}
