namespace Qaly.Domain.Entities;

public sealed class ProjectLaunchPlanArtifact : BaseEntity
{
    public Guid ProjectLaunchBriefId { get; set; }
    public Guid AssistantSessionId { get; set; }
    public Guid AssistantTurnId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? RuleSetId { get; set; }
    public int Revision { get; set; } = 1;
    public string State { get; set; } = "pending_review";
    public string StaffingScenariosJson { get; set; } = "[]";
    public string DeliveryPlanJson { get; set; } = "{}";
    public string BlockingReasonsJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public string SourceSnapshotJson { get; set; } = "[]";
    public string SourceVersionHash { get; set; } = string.Empty;
    public string? SelectedScenarioId { get; set; }
    public string ScoringVersion { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string ActualProvider { get; set; } = string.Empty;
    public string ActualModel { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public long RowRevision { get; set; } = 1;

    public ProjectLaunchBrief ProjectLaunchBrief { get; set; } = null!;
    public AssistantSession AssistantSession { get; set; } = null!;
    public AssistantTurn AssistantTurn { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public OrganizationWorkRuleSet? RuleSet { get; set; }
    public ICollection<ProjectLaunchExecution> Executions { get; set; } = new List<ProjectLaunchExecution>();
    public ICollection<ProjectReplanProposal> ReplanProposals { get; set; } = new List<ProjectReplanProposal>();
}
