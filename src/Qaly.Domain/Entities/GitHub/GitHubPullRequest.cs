namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Metadata pull request đã chuẩn hóa.
/// </summary>
public class GitHubPullRequest : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>Repository nguồn (khóa nội bộ).</summary>
    public Guid RepositoryConnectionId { get; set; }

    /// <summary>Số PR trên GitHub.</summary>
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Open, Closed, Merged.</summary>
    public string State { get; set; } = "Open";

    public string? AuthorLogin { get; set; }
    public string HeadBranch { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public bool IsDraft { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>Thời điểm cập nhật phía GitHub (khác <see cref="BaseEntity.UpdatedAt"/> là thời điểm cập nhật bản ghi nội bộ).</summary>
    public DateTimeOffset? GitHubUpdatedAt { get; set; }

    public DateTimeOffset? MergedAt { get; set; }
    public string? MergedByLogin { get; set; }

    public string Url { get; set; } = string.Empty;

    // Navigation
    public GitHubRepositoryConnection RepositoryConnection { get; set; } = null!;
    public ICollection<GitHubPullRequestReview> Reviews { get; set; } = new List<GitHubPullRequestReview>();
}
