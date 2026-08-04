namespace Qaly.Application.DTOs.GitHub;

/// <summary>Kết nối repository GitHub đã ánh xạ với một project.</summary>
public record GitHubRepositoryConnectionDto(
    Guid Id,
    Guid ProjectId,
    Guid OrganizationId,
    Guid GitHubInstallationId,
    long RepositoryExternalId,
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    bool IsPrivate,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Yêu cầu ánh xạ một repository vào project. Repository phải thuộc một
/// installation đã kết nối của cùng organization (kiểm tra ở tầng service).
/// </summary>
public record CreateGitHubRepositoryConnectionDto(
    Guid GitHubInstallationId,
    long RepositoryExternalId,
    string Owner,
    string Name,
    string? FullName = null,
    string DefaultBranch = "main",
    bool IsPrivate = true);

public record TaskDevelopmentCommitDto(string Sha, string Message, string? AuthorLogin,
    DateTimeOffset CommittedAt, string? BranchName, string Url, string Repository);

public record TaskDevelopmentPullRequestDto(int Number, string Title, string State, bool IsDraft,
    string? AuthorLogin, string HeadBranch, string BaseBranch, int ReviewCount, int ApprovalCount,
    DateTimeOffset? MergedAt, string Url, string Repository);

public record TaskDevelopmentReleaseDto(string TagName, string? Name, DateTimeOffset? PublishedAt,
    string Url, string Repository);

public record TaskDevelopmentWorkflowRunDto(long RunId, string WorkflowName, string? DisplayTitle,
    string Branch, string Status, string? Conclusion, DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt, string Url, string Repository);

public record TaskDevelopmentLinkDto(string EntityType, string ExternalEntityId, string LinkSource,
    DateTimeOffset CreatedAt);

public record TaskDevelopmentDto(Guid TaskId, string TaskKey,
    IReadOnlyList<TaskDevelopmentCommitDto> Commits,
    IReadOnlyList<TaskDevelopmentPullRequestDto> PullRequests,
    IReadOnlyList<TaskDevelopmentWorkflowRunDto> WorkflowRuns,
    IReadOnlyList<TaskDevelopmentReleaseDto> Releases,
    IReadOnlyList<TaskDevelopmentLinkDto> Links);
