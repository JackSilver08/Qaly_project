namespace Qaly.Domain.Entities;

public sealed class ProjectLaunchBrief : BaseEntity
{
    public Guid AssistantSessionId { get; set; }
    public Guid AssistantTurnId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? RuleSetId { get; set; }
    public int Revision { get; set; } = 1;
    public string State { get; set; } = "BRIEF_READY";
    public string BriefJson { get; set; } = "{}";
    public string SourceSnapshotJson { get; set; } = "[]";
    public string PromptVersion { get; set; } = string.Empty;
    public string ActualProvider { get; set; } = string.Empty;
    public string ActualModel { get; set; } = string.Empty;
    public long RowRevision { get; set; }

    public AssistantSession AssistantSession { get; set; } = null!;
    public AssistantTurn AssistantTurn { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public OrganizationWorkRuleSet? RuleSet { get; set; }
    public ICollection<OrganizationWorkRuleDecision> RuleDecisions { get; set; } = new List<OrganizationWorkRuleDecision>();
}
