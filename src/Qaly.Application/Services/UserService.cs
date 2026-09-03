using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.User;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepo;
    private readonly ICurrentUserService _currentUser;

    public UserService(IRepository<User> userRepo, ICurrentUserService currentUser)
    {
        _userRepo = userRepo;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserDirectoryDto>>> GetActiveAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid currentUserId)
            return Result.Forbidden<IReadOnlyList<UserDirectoryDto>>();

        var isSystemAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role);
        var query = _userRepo.GetQueryable()
            .Where(user => user.IsActive)
            .AsNoTracking();
        if (!isSystemAdmin)
            query = ApplyCollaboratorBoundary(query, currentUserId);

        var users = await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<UserDirectoryDto>>(
            users.Select(user => ToDirectoryDto(user, isSystemAdmin)).ToList());
    }

    public async Task<Result<UserDirectoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid currentUserId)
            return Result.Forbidden<UserDirectoryDto>();

        var isSystemAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role);
        var query = _userRepo.GetQueryable().AsNoTracking().Where(user => user.IsActive);
        if (!isSystemAdmin)
            query = ApplyCollaboratorBoundary(query, currentUserId);

        var user = await query.SingleOrDefaultAsync(item => item.Id == id, ct);
        // Use 404 for out-of-bound identities so callers cannot enumerate accounts.
        return user == null
            ? Result.NotFound<UserDirectoryDto>()
            : Result.Success(ToDirectoryDto(user, isSystemAdmin));
    }

    private static IQueryable<User> ApplyCollaboratorBoundary(IQueryable<User> users, Guid currentUserId)
        => users.Where(user =>
            user.Id == currentUserId ||
            user.OrganizationMemberships.Any(targetMembership =>
                targetMembership.Organization.IsActive &&
                (targetMembership.Organization.OwnerId == currentUserId ||
                 targetMembership.Organization.Members.Any(member => member.UserId == currentUserId))) ||
            user.OwnedOrganizations.Any(organization =>
                organization.IsActive &&
                (organization.OwnerId == currentUserId ||
                 organization.Members.Any(member => member.UserId == currentUserId))) ||
            user.ProjectMemberships.Any(targetMembership =>
                targetMembership.Project.OwnerId == currentUserId ||
                targetMembership.Project.Members.Any(member => member.UserId == currentUserId)) ||
            user.OwnedProjects.Any(project =>
                project.OwnerId == currentUserId ||
                project.Members.Any(member => member.UserId == currentUserId)) ||
            user.WorkGroupMemberships.Any(targetMembership =>
                !targetMembership.WorkGroup.IsDeleted &&
                (targetMembership.WorkGroup.OwnerId == currentUserId ||
                 targetMembership.WorkGroup.Members.Any(member => member.UserId == currentUserId))) ||
            user.OwnedWorkGroups.Any(group =>
                !group.IsDeleted &&
                (group.OwnerId == currentUserId ||
                 group.Members.Any(member => member.UserId == currentUserId))));

    private static UserDirectoryDto ToDirectoryDto(User user, bool exposeSystemRole)
        => new(
            user.Id,
            user.FullName,
            user.Email,
            user.IsActive,
            user.AvatarUrl,
            exposeSystemRole ? user.Role : null);
}
