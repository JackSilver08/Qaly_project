namespace Qaly.Domain.Entities;

public sealed class ProjectLaunchExecution : BaseEntity
{
    public Guid ProjectLaunchPlanArtifactId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = "executed";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string ReceiptJson { get; set; } = "{}";
    public Guid ExecutedByUserId { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public bool MonitoringEnabled { get; set; } = true;
    public DateTimeOffset? NextMonitorAt { get; set; }
    public DateTimeOffset? LastMonitoredAt { get; set; }
    public DateTimeOffset? RolledBackAt { get; set; }
    public string? RollbackReason { get; set; }
    public string? RollbackIdempotencyKey { get; set; }
    public string? RollbackPayloadHash { get; set; }
    public long RowRevision { get; set; } = 1;

    public ProjectLaunchPlanArtifact ProjectLaunchPlanArtifact { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ICollection<ProjectReplanProposal> ReplanProposals { get; set; } = new List<ProjectReplanProposal>();
}
