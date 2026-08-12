namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Ánh xạ một repository GitHub với một project Qaly. Một project có thể liên
/// kết nhiều repository (frontend/backend/infra tách riêng).
/// </summary>
public class GitHubRepositoryConnection : BaseEntity, ITenantScoped, ISoftDeleteEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>Project Qaly được ánh xạ.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Installation sở hữu repository (khóa nội bộ).</summary>
    public Guid GitHubInstallationId { get; set; }

    /// <summary>Repository ID do GitHub cấp.</summary>
    public long RepositoryExternalId { get; set; }

    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>owner/repository.</summary>
    public string FullName { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = "main";
    public bool IsPrivate { get; set; }

    /// <summary>Trạng thái đồng bộ (bật/tắt).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastSyncedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public GitHubInstallation Installation { get; set; } = null!;
    public ICollection<GitHubCommit> Commits { get; set; } = new List<GitHubCommit>();
    public ICollection<GitHubPullRequest> PullRequests { get; set; } = new List<GitHubPullRequest>();
    public ICollection<GitHubRelease> Releases { get; set; } = new List<GitHubRelease>();
    public ICollection<GitHubWorkflowRun> WorkflowRuns { get; set; } = new List<GitHubWorkflowRun>();
}
