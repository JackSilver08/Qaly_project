using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.GitHub;

namespace Qaly.Application.Services.GitHub;

/// <summary>
/// Quản lý ánh xạ repository GitHub ↔ project Qaly. Mọi thao tác đều được scope
/// và kiểm tra quyền qua <see cref="IGitHubAccessGuard"/>.
/// </summary>
public interface IGitHubRepositoryConnectionService
{
    Task<Result<IReadOnlyList<GitHubRepositoryConnectionDto>>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default);

    Task<Result<GitHubRepositoryConnectionDto>> CreateAsync(
        Guid projectId, CreateGitHubRepositoryConnectionDto dto, CancellationToken ct = default);

    Task<Result> RemoveAsync(
        Guid projectId, Guid connectionId, CancellationToken ct = default);
}
