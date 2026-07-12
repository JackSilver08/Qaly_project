using Qaly.Application.Common.Models;

namespace Qaly.Application.Services.GitHub;

/// <summary>Ngữ cảnh tenant đã xác thực cho một project khi thao tác GitHub.</summary>
public record GitHubProjectContext(Guid ProjectId, Guid OrganizationId);

/// <summary>
/// Rào chắn cách ly tenant cho mọi thao tác GitHub. Bảo đảm người dùng hiện tại
/// thực sự có quyền trên project và trả về OrganizationId để scope dữ liệu.
/// </summary>
public interface IGitHubAccessGuard
{
    /// <param name="requireManage">true nếu là thao tác thay đổi (map/gỡ repo).</param>
    Task<Result<GitHubProjectContext>> AuthorizeProjectAsync(
        Guid projectId,
        bool requireManage,
        CancellationToken ct = default);
}
