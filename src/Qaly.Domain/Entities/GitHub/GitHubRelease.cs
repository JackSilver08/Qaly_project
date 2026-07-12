namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Release đã phát hành trên repository.
/// </summary>
public class GitHubRelease : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>Repository nguồn (khóa nội bộ).</summary>
    public Guid RepositoryConnectionId { get; set; }

    /// <summary>Release ID do GitHub cấp.</summary>
    public long ReleaseExternalId { get; set; }

    public string TagName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool IsDraft { get; set; }
    public bool IsPrerelease { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string Url { get; set; } = string.Empty;

    // Navigation
    public GitHubRepositoryConnection RepositoryConnection { get; set; } = null!;
}
