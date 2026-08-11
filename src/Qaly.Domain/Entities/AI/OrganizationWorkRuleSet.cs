namespace Qaly.Domain.Entities;

public sealed class OrganizationWorkRuleSet : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "draft";
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public string RulesJson { get; set; } = "[]";
    public Guid CreatedByUserId { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public long Revision { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<ProjectLaunchBrief> LaunchBriefs { get; set; } = new List<ProjectLaunchBrief>();
}
