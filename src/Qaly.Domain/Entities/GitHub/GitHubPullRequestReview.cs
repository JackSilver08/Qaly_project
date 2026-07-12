namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Review của một pull request.
/// </summary>
public class GitHubPullRequestReview : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>PR liên quan (khóa nội bộ).</summary>
    public Guid PullRequestId { get; set; }

    /// <summary>Review ID do GitHub cấp.</summary>
    public long ReviewExternalId { get; set; }

    public string? ReviewerLogin { get; set; }

    /// <summary>Approved, ChangesRequested, Commented, Dismissed.</summary>
    public string State { get; set; } = "Commented";

    public DateTimeOffset SubmittedAt { get; set; }
    public string Url { get; set; } = string.Empty;

    // Navigation
    public GitHubPullRequest PullRequest { get; set; } = null!;
}
