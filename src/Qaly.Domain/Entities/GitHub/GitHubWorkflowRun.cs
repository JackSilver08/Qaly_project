namespace Qaly.Domain.Entities.GitHub;

public class GitHubWorkflowRun : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }
    public Guid RepositoryConnectionId { get; set; }
    public long RunExternalId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public string? DisplayTitle { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Conclusion { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Url { get; set; } = string.Empty;
    public GitHubRepositoryConnection RepositoryConnection { get; set; } = null!;
}
