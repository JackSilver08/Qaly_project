using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services.GitHub;

/// <summary>
/// Enforce cách ly tenant ở tầng data-access. Nền tảng chưa có "current
/// organization" ở tầng DbContext nên tenant được xác định qua project và kiểm
/// tra membership của người dùng hiện tại.
/// </summary>
public class GitHubAccessGuard : IGitHubAccessGuard
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _projectMemberRepo;
    private readonly IRepository<OrganizationMember> _orgMemberRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public GitHubAccessGuard(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IRepository<OrganizationMember> orgMemberRepo,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _orgMemberRepo = orgMemberRepo;
        _currentUserService = currentUserService;
        _taskAccessPolicy = taskAccessPolicy;
    }

    public async Task<Result<GitHubProjectContext>> AuthorizeProjectAsync(
        Guid projectId,
        bool requireManage,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return Result.Forbidden<GitHubProjectContext>();
        }

        var project = await _projectRepo.GetQueryable()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.Id,
                p.OwnerId,
                p.OrganizationId,
                OrganizationIsActive = p.Organization != null && p.Organization.IsActive
            })
            .FirstOrDefaultAsync(ct);

        if (project is null)
        {
            return Result.NotFound<GitHubProjectContext>("Không tìm thấy dự án.");
        }

        if (project.OrganizationId is null)
        {
            return Result.Failure<GitHubProjectContext>(
                "Tích hợp GitHub yêu cầu dự án thuộc một organization.", 400);
        }

        if (!project.OrganizationIsActive)
        {
            return Result.Forbidden<GitHubProjectContext>();
        }

        var organizationId = project.OrganizationId.Value;
        if (!await _taskAccessPolicy.CanAccessProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<GitHubProjectContext>();
        }

        if (requireManage && !await _taskAccessPolicy.CanManageProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<GitHubProjectContext>(
                "Chỉ quản trị viên dự án/organization mới được thay đổi kết nối GitHub.");
        }

        return Result.Success(new GitHubProjectContext(projectId, organizationId));
    }
}
