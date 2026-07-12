using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
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

    public GitHubAccessGuard(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IRepository<OrganizationMember> orgMemberRepo,
        ICurrentUserService currentUserService)
    {
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _orgMemberRepo = orgMemberRepo;
        _currentUserService = currentUserService;
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
                OrgOwnerId = p.Organization != null ? (Guid?)p.Organization.OwnerId : null
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

        var organizationId = project.OrganizationId.Value;
        var isAdmin = ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);
        var isProjectOwner = project.OwnerId == userId;
        var isOrgOwner = project.OrgOwnerId == userId;

        var isProjectMember = await _projectMemberRepo.GetQueryable()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

        var orgRole = await _orgMemberRepo.GetQueryable()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync(ct);
        var isOrgMember = orgRole is not null;

        var canAccess = isAdmin || isProjectOwner || isOrgOwner || isProjectMember || isOrgMember;
        if (!canAccess)
        {
            return Result.Forbidden<GitHubProjectContext>();
        }

        if (requireManage)
        {
            var canManage = isAdmin
                || isProjectOwner
                || isOrgOwner
                || IsOrgManagerRole(orgRole);
            if (!canManage)
            {
                return Result.Forbidden<GitHubProjectContext>(
                    "Chỉ quản trị viên dự án/organization mới được thay đổi kết nối GitHub.");
            }
        }

        return Result.Success(new GitHubProjectContext(projectId, organizationId));
    }

    private static bool IsOrgManagerRole(string? role)
        => string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
}
