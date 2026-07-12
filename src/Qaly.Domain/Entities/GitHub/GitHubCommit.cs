namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Metadata commit đã chuẩn hóa. Không lưu source code hay full diff.
/// </summary>
public class GitHubCommit : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>Repository nguồn (khóa nội bộ).</summary>
    public Guid RepositoryConnectionId { get; set; }

    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>GitHub login nếu ánh xạ được.</summary>
    public string? AuthorLogin { get; set; }

    /// <summary>Hash email tác giả — không lưu email thô.</summary>
    public string? AuthorEmailHash { get; set; }

    public DateTimeOffset CommittedAt { get; set; }

    /// <summary>Branch nhận từ event.</summary>
    public string? BranchName { get; set; }

    public string Url { get; set; } = string.Empty;

    // Navigation
    public GitHubRepositoryConnection RepositoryConnection { get; set; } = null!;
}
