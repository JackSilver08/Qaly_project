namespace Qaly.Domain.Entities;

public sealed class OrganizationWorkRuleDecision : BaseEntity
{
    public Guid ProjectLaunchBriefId { get; set; }
    public Guid? RuleSetId { get; set; }
    public int? RuleSetVersion { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public string Result { get; set; } = "unknown";
    public string Severity { get; set; } = "warning";
    public string Explanation { get; set; } = string.Empty;
    public string DeterministicFactsJson { get; set; } = "{}";
    public bool ExceptionEligible { get; set; }
    public string SourceFreshness { get; set; } = string.Empty;

    public ProjectLaunchBrief ProjectLaunchBrief { get; set; } = null!;
}
