namespace Qaly.Domain.Entities;

public class AiJob : BaseEntity
{
    public string JobType { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceId { get; set; }
    public string ProviderHint { get; set; } = "auto";
    public bool Sensitive { get; set; }
    public string Status { get; set; } = "Queued";
    public decimal EstimatedCostUsd { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }

    public Project Project { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public ICollection<AiGeneratedDraft> Drafts { get; set; } = new List<AiGeneratedDraft>();
}
