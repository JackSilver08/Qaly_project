using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services.Groups;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class GroupsService : IGroupsService
{
    private readonly IRepository<WorkGroup> _groupRepo;
    private readonly IRepository<WorkGroupMember> _memberRepo;
    private readonly IRepository<GroupMessage> _messageRepo;
    private readonly IRepository<Organization> _organizationRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IProjectService _projectService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GroupsService(
        IRepository<WorkGroup> groupRepo,
        IRepository<WorkGroupMember> memberRepo,
        IRepository<GroupMessage> messageRepo,
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<User> userRepo,
        IProjectService projectService,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _groupRepo = groupRepo;
        _memberRepo = memberRepo;
        _messageRepo = messageRepo;
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _userRepo = userRepo;
        _projectService = projectService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<GroupDto>>> GetMineAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<GroupDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = GroupDetailsQuery();
        if (!IsSystemAdmin())
        {
            query = query.Where(group => group.OwnerId == currentUserId || group.Members.Any(member => member.UserId == currentUserId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(group =>
                group.Name.Contains(normalized) ||
                (group.Description != null && group.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var groups = await query
            .OrderByDescending(group => group.UpdatedAt ?? group.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<GroupDto>
        {
            Items = groups.Select(group => ToDto(group, currentUserId.Value)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<GroupDto>> GetByIdAsync(Guid groupId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupDto>();
        }

        var group = await GroupDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupDto>();
        }

        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupDto>();
        }

        return Result.Success(ToDto(group, currentUserId.Value));
    }

    public async Task<Result<GroupDto>> CreateAsync(CreateGroupRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupDto>();
        }

        var validation = ValidateGroup(request.Name, request.Color);
        if (!validation.IsSuccess)
        {
            return Result.Failure<GroupDto>(validation.Error!, validation.StatusCode);
        }

        if (request.OrganizationId.HasValue)
        {
            var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId.Value, ct);
            if (organization == null)
            {
                return Result.Failure<GroupDto>("Organization was not found.", 404);
            }

            if (!organization.IsActive)
            {
                return Result.Failure<GroupDto>("Organization is inactive.", 400);
            }

            if (!await CanAccessOrganizationAsync(request.OrganizationId.Value, organization.OwnerId, ct))
            {
                return Result.Forbidden<GroupDto>();
            }
        }

        var group = new WorkGroup
        {
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            AvatarUrl = NormalizeOptional(request.AvatarUrl),
            Color = NormalizeOptional(request.Color),
            OrganizationId = request.OrganizationId,
            OwnerId = currentUserId.Value
        };

        await _groupRepo.AddAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _memberRepo.AddAsync(new WorkGroupMember
        {
            WorkGroupId = group.Id,
            UserId = currentUserId.Value,
            Role = GroupRoleRules.Owner
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(WorkGroup), group.Id.ToString(), new { group.Name }, ct);

        return await GetByIdAsync(group.Id, ct);
    }

    public async Task<Result<GroupDto>> UpdateAsync(Guid groupId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound<GroupDto>();
        }

        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupDto>();
        }

        var validation = ValidateGroup(request.Name, request.Color);
        if (!validation.IsSuccess)
        {
            return Result.Failure<GroupDto>(validation.Error!, validation.StatusCode);
        }

        group.Name = request.Name.Trim();
        group.Description = NormalizeOptional(request.Description);
        group.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        group.Color = NormalizeOptional(request.Color);
        group.Status = NormalizeGroupStatus(request.Status);

        await _groupRepo.UpdateAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(WorkGroup), groupId.ToString(), request, ct);

        return await GetByIdAsync(groupId, ct);
    }

    public async Task<Result> DeleteAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden();
        }

        await _groupRepo.DeleteAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(WorkGroup), groupId.ToString(), new { group.Name }, ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupMemberDto>>> GetMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<IReadOnlyList<GroupMemberDto>>();
        }

        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.WorkGroupId == groupId)
            .OrderBy(member => member.Role == GroupRoleRules.Owner ? 0 : member.Role == GroupRoleRules.Admin ? 1 : 2)
            .ThenBy(member => member.User.FullName)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<GroupMemberDto>>(members.Select(ToMemberDto).ToList());
    }

    public async Task<Result> AddExistingMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken ct = default)
    {
        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden();
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        var user = await _userRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsActive, ct);
        if (user == null)
        {
            return Result.Failure("User was not found.", 404);
        }

        var role = GroupRoleRules.Normalize(request.Role);
        var existing = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(member => member.WorkGroupId == groupId && member.UserId == request.UserId, ct);

        if (existing != null)
        {
            if (group.OwnerId == request.UserId && role != GroupRoleRules.Owner)
            {
                return Result.Failure("Group owner role cannot be changed.", 400);
            }

            existing.Role = role;
            await _memberRepo.UpdateAsync(existing, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _auditLogService.LogAsync("UpdateMemberRole", nameof(WorkGroup), groupId.ToString(), new { request.UserId, role }, ct);
            return Result.Success();
        }

        await _memberRepo.AddAsync(new WorkGroupMember
        {
            WorkGroupId = groupId,
            UserId = request.UserId,
            Role = role
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AddMember", nameof(WorkGroup), groupId.ToString(), new { request.UserId, role }, ct);
        await _notificationService.CreateAsync(
            request.UserId,
            $"You were added to group \"{group.Name}\".",
            "GroupInvite",
            "success",
            group.Id,
            nameof(WorkGroup),
            $"group:{group.Id}:member:{request.UserId}",
            ct);

        return Result.Success();
    }

    public async Task<Result> UpdateMemberRoleAsync(Guid groupId, Guid userId, UpdateGroupMemberRoleRequest request, CancellationToken ct = default)
    {
        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden();
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        if (group.OwnerId == userId)
        {
            return Result.Failure("Group owner role cannot be changed.", 400);
        }

        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.WorkGroupId == groupId && item.UserId == userId, ct);
        if (member == null)
        {
            return Result.Failure("Group member was not found.", 404);
        }

        member.Role = GroupRoleRules.Normalize(request.Role);
        await _memberRepo.UpdateAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("UpdateMemberRole", nameof(WorkGroup), groupId.ToString(), new { userId, member.Role }, ct);

        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden();
        }

        var isSelfLeave = currentUserId.Value == userId;
        if (!isSelfLeave && !await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden();
        }

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            return Result.NotFound();
        }

        if (group.OwnerId == userId)
        {
            return Result.Failure("Group owner cannot be removed from the group.", 400);
        }

        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.WorkGroupId == groupId && item.UserId == userId, ct);
        if (member == null)
        {
            return Result.Failure("Group member was not found.", 404);
        }

        await _memberRepo.DeleteAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RemoveMember", nameof(WorkGroup), groupId.ToString(), new { userId }, ct);

        return Result.Success();
    }

    public async Task<Result<PagedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<PagedResult<GroupMessageDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _messageRepo.GetQueryable()
            .AsNoTracking()
            .Include(message => message.User)
            .Where(message => message.WorkGroupId == groupId);

        var totalCount = await query.CountAsync(ct);
        var messages = await query
            .OrderByDescending(message => message.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        messages.Reverse();

        return Result.Success(new PagedResult<GroupMessageDto>
        {
            Items = messages.Select(ToMessageDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<GroupMessageDto>> CreateMessageAsync(Guid groupId, SendGroupMessageRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<GroupMessageDto>();
        }

        if (!await CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupMessageDto>();
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure<GroupMessageDto>("Message content is required.");
        }

        if (request.Content.Trim().Length > 4000)
        {
            return Result.Failure<GroupMessageDto>("Message content must be 4000 characters or fewer.");
        }

        var message = new GroupMessage
        {
            WorkGroupId = groupId,
            UserId = currentUserId.Value,
            Content = request.Content.Trim(),
            MessageType = NormalizeMessageType(request.MessageType)
        };

        await _messageRepo.AddAsync(message, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _messageRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.User)
            .FirstAsync(item => item.Id == message.Id, ct);

        return Result.Created(ToMessageDto(saved));
    }

    public async Task<Result<CreateProjectFromGroupResult>> CreateProjectFromGroupAsync(Guid groupId, CreateProjectFromGroupRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<CreateProjectFromGroupResult>();
        }

        if (!await CanManageGroupAsync(groupId, ct))
        {
            return Result.Forbidden<CreateProjectFromGroupResult>();
        }

        var group = await GroupDetailsQuery()
            .Include(item => item.Members)
                .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(item => item.Id == groupId, ct);
        if (group == null)
        {
            return Result.NotFound<CreateProjectFromGroupResult>();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CreateProjectFromGroupResult>("Project name is required.");
        }

        var projectResult = await _projectService.CreateAsync(new CreateProjectDto(
            request.Name.Trim(),
            request.Code,
            request.Description,
            null,
            request.StartDate,
            request.EndDate,
            group.OrganizationId,
            group.Id), ct);

        if (!projectResult.IsSuccess || projectResult.Data == null)
        {
            return Result.Failure<CreateProjectFromGroupResult>(projectResult.Error ?? "Project could not be created.", projectResult.StatusCode);
        }

        var addedUserIds = new List<Guid>();
        var warnings = new List<string>();

        foreach (var member in group.Members.Where(item => item.User.IsActive && item.UserId != currentUserId.Value))
        {
            var projectRole = GroupRoleRules.ToProjectRole(member.Role);
            var addResult = await _projectService.AddMemberAsync(projectResult.Data.Id, member.UserId, projectRole, ct);
            if (addResult.IsSuccess)
            {
                addedUserIds.Add(member.UserId);
            }
            else
            {
                warnings.Add($"{member.User.Email}: {addResult.Error}");
            }
        }

        var refreshedProject = await _projectService.GetByIdAsync(projectResult.Data.Id, ct);
        var project = refreshedProject.IsSuccess && refreshedProject.Data != null
            ? refreshedProject.Data
            : projectResult.Data;

        return Result.Created(new CreateProjectFromGroupResult(project, addedUserIds.Count, addedUserIds, warnings));
    }

    public async Task<bool> CanAccessGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin())
        {
            return true;
        }

        return await _groupRepo.GetQueryable()
            .AnyAsync(group =>
                group.Id == groupId &&
                (group.OwnerId == currentUserId ||
                 group.Members.Any(member => member.UserId == currentUserId)), ct);
    }

    public async Task<bool> CanManageGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin())
        {
            return true;
        }

        var membership = await _memberRepo.GetQueryable()
            .Where(member => member.WorkGroupId == groupId && member.UserId == currentUserId)
            .Select(member => new { member.Role })
            .FirstOrDefaultAsync(ct);

        if (membership == null)
        {
            return false;
        }

        return GroupRoleRules.CanManage(membership.Role);
    }

    private IQueryable<WorkGroup> GroupDetailsQuery()
        => _groupRepo.GetQueryable()
            .Include(group => group.Owner)
            .Include(group => group.Organization)
            .Include(group => group.Members)
                .ThenInclude(member => member.User)
            .Include(group => group.Messages)
            .Include(group => group.Polls);

    private GroupDto ToDto(WorkGroup group, Guid currentUserId)
    {
        var currentRole = group.OwnerId == currentUserId
            ? GroupRoleRules.Owner
            : group.Members.FirstOrDefault(member => member.UserId == currentUserId)?.Role;

        if (currentRole == null && IsSystemAdmin())
        {
            currentRole = GroupRoleRules.Admin;
        }

        return new GroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.AvatarUrl,
            group.Color,
            group.Status,
            group.OwnerId,
            group.Owner?.FullName ?? string.Empty,
            group.OrganizationId,
            group.Organization?.Name,
            currentRole ?? GroupRoleRules.Member,
            group.Members?.Count ?? 0,
            group.Messages?.Count(message => !message.IsDeleted) ?? 0,
            group.Polls?.Count(poll => string.Equals(poll.Status, "Open", StringComparison.OrdinalIgnoreCase)) ?? 0,
            group.CreatedAt,
            group.UpdatedAt);
    }

    private static GroupMemberDto ToMemberDto(WorkGroupMember member)
        => new(
            member.UserId,
            member.User.FullName,
            member.User.Email,
            member.Role,
            member.JoinedAt);

    private static GroupMessageDto ToMessageDto(GroupMessage message)
        => new(
            message.Id,
            message.WorkGroupId,
            message.UserId,
            message.User.FullName,
            message.User.AvatarUrl,
            message.IsDeleted ? string.Empty : message.Content,
            message.MessageType,
            message.IsDeleted,
            message.CreatedAt,
            message.EditedAt);

    private async Task<bool> CanAccessOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin() || ownerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == organizationId && member.UserId == currentUserId, ct);
    }

    private bool IsSystemAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

    private static Result ValidateGroup(string name, string? color)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Group name is required.");
        }

        if (name.Trim().Length > 160)
        {
            return Result.Failure("Group name must be 160 characters or fewer.");
        }

        if (!string.IsNullOrWhiteSpace(color) && color.Trim().Length > 20)
        {
            return Result.Failure("Group color must be 20 characters or fewer.");
        }

        return Result.Success();
    }

    private static string NormalizeGroupStatus(string? status)
    {
        if (string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return "Archived";
        }

        return "Active";
    }

    private static string NormalizeMessageType(string? messageType)
    {
        if (string.Equals(messageType, "System", StringComparison.OrdinalIgnoreCase))
        {
            return "System";
        }

        if (string.Equals(messageType, "Meeting", StringComparison.OrdinalIgnoreCase))
        {
            return "Meeting";
        }

        if (string.Equals(messageType, "Poll", StringComparison.OrdinalIgnoreCase))
        {
            return "Poll";
        }

        return "Text";
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
