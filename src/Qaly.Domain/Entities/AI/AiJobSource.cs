namespace Qaly.Domain.Entities;

public class AiJobSource : BaseEntity
{
    public Guid AiJobId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceEntityId { get; set; }
    public string? LegacySourceKey { get; set; }
    public string? SourceVersion { get; set; }
    public string? SourceHash { get; set; }
    public DateTimeOffset? SourceTimestamp { get; set; }
    public int SortOrder { get; set; }

    public AiJob AiJob { get; set; } = null!;
}
