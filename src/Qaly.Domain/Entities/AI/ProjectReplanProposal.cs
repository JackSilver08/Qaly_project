namespace Qaly.Domain.Entities;

public sealed class ProjectReplanProposal : BaseEntity
{
    public Guid ProjectLaunchPlanArtifactId { get; set; }
    public Guid ProjectLaunchExecutionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public int Revision { get; set; } = 1;
    public string State { get; set; } = "pending_review";
    public string TriggerCodesJson { get; set; } = "[]";
    public string ProposalJson { get; set; } = "{}";
    public string SourceSnapshotJson { get; set; } = "[]";
    public string BaselineHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public long RowRevision { get; set; } = 1;

    public ProjectLaunchPlanArtifact ProjectLaunchPlanArtifact { get; set; } = null!;
    public ProjectLaunchExecution ProjectLaunchExecution { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public Project Project { get; set; } = null!;
}
