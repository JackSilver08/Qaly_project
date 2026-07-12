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
