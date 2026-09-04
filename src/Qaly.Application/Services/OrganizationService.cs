using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text.RegularExpressions;

namespace Qaly.Application.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IRepository<Organization> _organizationRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<ModeratorAssignment> _moderatorAssignmentRepo;
    private readonly IRepository<ProfessionalProfileDefinition> _professionalProfileDefinitions;
    private readonly IRepository<ProjectMember>? _projectMemberRepo;
    private readonly IRepository<OrganizationMemberCapacityProfile>? _capacityProfileRepo;
    private readonly IRepository<TaskCompletionAttribution>? _skillAttributionRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public OrganizationService(
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<User> userRepo,
        IRepository<ModeratorAssignment> moderatorAssignmentRepo,
        IRepository<ProfessionalProfileDefinition> professionalProfileDefinitions,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IRepository<ProjectMember>? projectMemberRepo = null,
        IRepository<OrganizationMemberCapacityProfile>? capacityProfileRepo = null,
        IRepository<TaskCompletionAttribution>? skillAttributionRepo = null)
    {
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _userRepo = userRepo;
        _moderatorAssignmentRepo = moderatorAssignmentRepo;
        _professionalProfileDefinitions = professionalProfileDefinitions;
        _projectMemberRepo = projectMemberRepo;
        _capacityProfileRepo = capacityProfileRepo;
        _skillAttributionRepo = skillAttributionRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<OrganizationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await OrganizationDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (organization == null)
        {
            return Result.NotFound<OrganizationDto>();
        }

        if (!organization.IsActive)
        {
            return Result.Forbidden<OrganizationDto>();
        }

        if (!await CanAccessOrganizationAsync(organization.Id, organization.OwnerId, ct))
        {
            return Result.Forbidden<OrganizationDto>();
        }

        return Result.Success(organization.ToDto());
    }

    public async Task<Result<PagedResult<OrganizationDto>>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<OrganizationDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = OrganizationDetailsQuery();
        if (!IsSystemAdmin())
        {
            if (SystemRoleRules.IsModerator(_currentUserService.Role))
            {
                var now = DateTimeOffset.UtcNow;
                var assignedOrganizationIds = _moderatorAssignmentRepo.GetQueryable()
                    .Where(item => item.ModeratorUserId == currentUserId && item.IsActive && item.RevokedAt == null
                        && (item.ExpiresAt == null || item.ExpiresAt > now))
                    .Select(item => item.OrganizationId);
                query = query.Where(item => item.OwnerId == currentUserId
                    || item.Members.Any(member => member.UserId == currentUserId)
                    || assignedOrganizationIds.Contains(item.Id));
            }
            else
            {
                query = query.Where(item => item.OwnerId == currentUserId
                    || item.Members.Any(member => member.UserId == currentUserId));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(item =>
                item.Name.Contains(normalized) ||
                item.Code.Contains(normalized) ||
                (item.Description != null && item.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<OrganizationDto>
        {
            Items = items.Select(item => item.ToDto()).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<OrganizationDto>> CreateAsync(CreateOrganizationDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<OrganizationDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<OrganizationDto>("Organization name is required.");
        }

        var ownerId = currentUserId.Value;
        if (dto.OwnerId.HasValue && dto.OwnerId.Value != currentUserId.Value)
        {
            if (!IsSystemAdmin())
            {
                return Result.Forbidden<OrganizationDto>();
            }

            var ownerExists = await _userRepo.GetQueryable()
                .AnyAsync(user => user.Id == dto.OwnerId.Value && user.IsActive, ct);
            if (!ownerExists)
            {
                return Result.Failure<OrganizationDto>("Organization owner was not found.", 404);
            }

            ownerId = dto.OwnerId.Value;
        }

        var organization = new Organization
        {
            Name = dto.Name.Trim(),
            Code = await GenerateUniqueCodeAsync(dto.Code, dto.Name, ct),
            Description = NormalizeOptional(dto.Description),
            OwnerId = ownerId,
            IsActive = true
        };

        await _organizationRepo.AddAsync(organization, ct);
        await _professionalProfileDefinitions.AddRangeAsync(
            ProfessionalProfileCatalog.CreateBaseline(organization.Id),
            ct);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = ownerId,
            Role = OrganizationRoleRules.Owner
        }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Create",
            nameof(Organization),
            organization.Id.ToString(),
            new { organization.Name, organization.Code },
            ct);

        return await GetByIdAsync(organization.Id, ct);
    }

    public async Task<Result<OrganizationDto>> UpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(id, ct);
        if (organization == null)
        {
            return Result.NotFound<OrganizationDto>();
        }

        if (!await CanManageOrganizationAsync(organization.Id, organization.OwnerId, ct))
        {
            return Result.Forbidden<OrganizationDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<OrganizationDto>("Organization name is required.");
        }

        organization.Name = dto.Name.Trim();
        organization.Code = await GenerateUniqueCodeAsync(dto.Code, dto.Name, ct, organization.Id);
        organization.Description = NormalizeOptional(dto.Description);
        organization.IsActive = dto.IsActive;
        organization.AllowedEmailDomains = dto.AllowedEmailDomains;
        organization.WorkspaceIcon = dto.WorkspaceIcon;
        organization.WorkspaceCover = dto.WorkspaceCover;

        if (dto.OwnerId.HasValue && dto.OwnerId.Value != organization.OwnerId)
        {
            if (!IsSystemAdmin())
            {
                return Result.Forbidden<OrganizationDto>();
            }

            var newOwnerExists = await _userRepo.GetQueryable()
                .AnyAsync(user => user.Id == dto.OwnerId.Value && user.IsActive, ct);
            if (!newOwnerExists)
            {
                return Result.Failure<OrganizationDto>("Organization owner was not found.", 404);
            }

            var previousOwnerMembership = await _organizationMemberRepo.GetQueryable()
                .FirstOrDefaultAsync(item => item.OrganizationId == id && item.UserId == organization.OwnerId, ct);
            if (previousOwnerMembership != null)
            {
                previousOwnerMembership.Role = OrganizationRoleRules.OrganizationAdmin;
                await _organizationMemberRepo.UpdateAsync(previousOwnerMembership, ct);
            }

            var newOwnerMembership = await _organizationMemberRepo.GetQueryable()
                .FirstOrDefaultAsync(item => item.OrganizationId == id && item.UserId == dto.OwnerId.Value, ct);
            if (newOwnerMembership == null)
            {
                await _organizationMemberRepo.AddAsync(new OrganizationMember
                {
                    OrganizationId = id,
                    UserId = dto.OwnerId.Value,
                    Role = OrganizationRoleRules.Owner
                }, ct);
            }
            else
            {
                newOwnerMembership.Role = OrganizationRoleRules.Owner;
                await _organizationMemberRepo.UpdateAsync(newOwnerMembership, ct);
            }

            organization.OwnerId = dto.OwnerId.Value;
        }

        await _organizationRepo.UpdateAsync(organization, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Update",
            nameof(Organization),
            organization.Id.ToString(),
            dto,
            ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(id, ct);
        if (organization == null)
        {
            return Result.NotFound();
        }

        if (!await CanManageOrganizationAsync(organization.Id, organization.OwnerId, ct))
        {
            return Result.Forbidden();
        }

        organization.IsActive = false;
        await _organizationRepo.UpdateAsync(organization, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Deactivate",
            nameof(Organization),
            organization.Id.ToString(),
            new { organization.Name },
            ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<OrganizationMemberDto>>> GetMembersAsync(Guid organizationId, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(organizationId, ct);
        if (organization == null)
        {
            return Result.NotFound<IReadOnlyList<OrganizationMemberDto>>();
        }

        if (!organization.IsActive)
        {
            return Result.Forbidden<IReadOnlyList<OrganizationMemberDto>>();
        }

        if (!await CanAccessOrganizationAsync(organization.Id, organization.OwnerId, ct)
            && !await HasModeratorCapabilityAsync(organization.Id, ModeratorCapabilities.UsersView, ct))
        {
            return Result.Forbidden<IReadOnlyList<OrganizationMemberDto>>();
        }

        var members = await _organizationMemberRepo.GetQueryable()
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .Include(item => item.User)
            .OrderBy(item => item.User.FullName)
            .ToListAsync(ct);

        var memberIds = members.Select(item => item.UserId).ToHashSet();
        var canManage = await CanManageOrganizationAsync(organizationId, organization.OwnerId, ct);
        var currentUserId = _currentUserService.UserId;

        var projectMemberships = _projectMemberRepo == null
            ? []
            : await _projectMemberRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Project)
            .Where(item =>
                item.Project.OrganizationId == organizationId &&
                !item.Project.IsDeleted &&
                memberIds.Contains(item.UserId))
            .OrderBy(item => item.Project.Name)
            .ToListAsync(ct);

        var visibleProjectIds = canManage
            ? projectMemberships.Select(item => item.ProjectId).ToHashSet()
            : projectMemberships
                .Where(item => item.Project.OwnerId == currentUserId || item.UserId == currentUserId)
                .Select(item => item.ProjectId)
                .ToHashSet();

        var capacityProfiles = _capacityProfileRepo == null
            ? []
            : await _capacityProfileRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.AvailabilityWindows)
            .Where(item => item.OrganizationId == organizationId && memberIds.Contains(item.UserId))
            .ToListAsync(ct);
        var capacityByUserId = capacityProfiles.ToDictionary(item => item.UserId);

        var skillRows = _skillAttributionRepo == null
            ? []
            : await _skillAttributionRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.TaskItem)
                .ThenInclude(item => item.Project)
            .Include(item => item.TaskItem.SkillRequirements)
                .ThenInclude(item => item.OrganizationSkill)
            .Where(item =>
                memberIds.Contains(item.ContributorUserId) &&
                item.Status == TaskCompletionAttribution.Confirmed &&
                !item.TaskItem.IsDeleted &&
                item.TaskItem.Project.OrganizationId == organizationId)
            .ToListAsync(ct);
        var skillsByUserId = skillRows
            .SelectMany(attribution => attribution.TaskItem.SkillRequirements
                .Where(requirement => requirement.OrganizationSkill.OrganizationId == organizationId)
                .Select(requirement => new
                {
                    attribution.ContributorUserId,
                    requirement.OrganizationSkill.Name
                }))
            .GroupBy(item => item.ContributorUserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(item => item.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var result = members
            .Select(item =>
            {
                var canSeePeopleData = canManage || item.UserId == currentUserId;
                capacityByUserId.TryGetValue(item.UserId, out var capacity);
                skillsByUserId.TryGetValue(item.UserId, out var skills);
                var assignments = projectMemberships
                    .Where(projectMember =>
                        projectMember.UserId == item.UserId &&
                        visibleProjectIds.Contains(projectMember.ProjectId))
                    .Select(projectMember => new OrganizationProjectMembershipDto(
                        projectMember.ProjectId,
                        projectMember.Project.Name,
                        projectMember.Project.Code,
                        projectMember.Role))
                    .ToList();

                return new OrganizationMemberDto(
                    item.UserId,
                    item.User.FullName,
                    item.User.Email,
                    item.Role,
                    item.JoinedAt,
                    assignments,
                    canSeePeopleData
                        ? capacity?.WeeklyCapacityHours ?? OrganizationMemberCapacityProfile.DefaultWeeklyCapacityHours
                        : null,
                    canSeePeopleData ? capacity == null ? "assumed_default" : "declared" : null,
                    canSeePeopleData ? capacity?.TimeZoneId ?? "Asia/Ho_Chi_Minh" : null,
                    canSeePeopleData
                        ? capacity?.AvailabilityWindows
                            .OrderBy(window => window.StartsAt)
                            .Select(window => new OrganizationMemberAvailabilityDto(
                                window.Id,
                                window.StartsAt,
                                window.EndsAt,
                                window.Kind,
                                window.AvailableHours,
                                window.RowVersion.Length == 0 ? null : Convert.ToBase64String(window.RowVersion)))
                            .ToList() ?? []
                        : null,
                    canSeePeopleData && capacity?.RowVersion.Length > 0
                        ? Convert.ToBase64String(capacity.RowVersion)
                        : null,
                    canSeePeopleData ? skills ?? [] : null);
            })
            .ToList();

        return Result.Success<IReadOnlyList<OrganizationMemberDto>>(result);
    }

    public async Task<Result> AddMemberAsync(Guid organizationId, Guid userId, string role, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(organizationId, ct);
        if (organization == null)
        {
            return Result.Failure("Organization was not found.", 404);
        }

        var membershipExists = await _organizationMemberRepo.GetQueryable()
            .AnyAsync(item => item.OrganizationId == organizationId && item.UserId == userId, ct);
        var requiredCapability = membershipExists
            ? ModeratorCapabilities.UsersUpdateRole
            : ModeratorCapabilities.UsersInvite;
        if (!await CanManageOrganizationAsync(organization.Id, organization.OwnerId, ct)
            && !await HasModeratorCapabilityAsync(organization.Id, requiredCapability, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        if (organization.OwnerId == userId && !string.Equals(role, OrganizationRoleRules.Owner, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Organization owner role cannot be downgraded by this action.", 409);
        }

        var userExists = await _userRepo.GetQueryable()
            .AnyAsync(user => user.Id == userId && user.IsActive, ct);
        if (!userExists)
        {
            return Result.Failure("User was not found.", 404);
        }

        if (!OrganizationRoleRules.TryNormalizeAssignableRole(role, out var normalizedRole))
        {
            return Result.Failure("Organization role is invalid.", 400);
        }
        var existing = await _organizationMemberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, ct);

        if (existing != null)
        {
            return Result.Failure("User is already a member of this organization.", 409);
        }

        try
        {
            await _organizationMemberRepo.AddAsync(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = userId,
                Role = normalizedRole
            }, ct);
            await _unitOfWork.SaveChangesWithAuditAsync(
                _auditLogService,
                "AddMember",
                nameof(Organization),
                organizationId.ToString(),
                new { userId, role = normalizedRole },
                ct);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("Organization was modified by another request.", 409);
        }
    }

    public async Task<Result> AddMemberByEmailAsync(Guid organizationId, string email, string role, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(organizationId, ct);
        if (organization == null)
        {
            return Result.Failure("Organization was not found.", 404);
        }

        if (!await CanManageOrganizationAsync(organization.Id, organization.OwnerId, ct)
            && !await HasModeratorCapabilityAsync(organization.Id, ModeratorCapabilities.UsersInvite, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Result.Failure("Email is required.", 400);
        }

        var userId = await _userRepo.GetQueryable()
            .Where(user => user.IsActive && user.Email == normalizedEmail)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == null)
        {
            return Result.Failure("No active account matches this email.", 404);
        }

        return await AddMemberAsync(organizationId, userId.Value, role, ct);
    }

    public async Task<Result> UpdateMemberRoleAsync(Guid organizationId, Guid userId, string role, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(organizationId, ct);
        if (organization == null)
        {
            return Result.Failure("Organization was not found.", 404);
        }

        if (!await CanManageOrganizationAsync(organization.Id, organization.OwnerId, ct)
            && !await HasModeratorCapabilityAsync(organization.Id, ModeratorCapabilities.UsersUpdateRole, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        if (!OrganizationRoleRules.TryNormalizeAssignableRole(role, out var normalizedRole))
        {
            return Result.Failure("Organization role is invalid.", 400);
        }

        var membership = await _organizationMemberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, ct);

        if (membership == null)
        {
            return Result.Failure("Organization member was not found.", 404);
        }

        if (organization.OwnerId == userId && !string.Equals(normalizedRole, OrganizationRoleRules.Owner, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Ownership transfer is required before changing the owner role.", 409);
        }

        if (string.Equals(membership.Role, normalizedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        try
        {
            var oldRole = membership.Role;
            membership.Role = normalizedRole;
            await _organizationMemberRepo.UpdateAsync(membership, ct);
            await _unitOfWork.SaveChangesWithAuditAsync(
                _auditLogService,
                "UpdateMemberRole",
                nameof(Organization),
                organizationId.ToString(),
                new
                {
                    userId,
                    OldRole = oldRole,
                    NewRole = normalizedRole
                },
                ct);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("Organization was modified by another request.", 409);
        }
    }

    public async Task<Result> RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(organizationId, ct);
        if (organization == null)
        {
            return Result.Failure("Organization was not found.", 404);
        }

        if (!await CanManageOrganizationAsync(organization.Id, organization.OwnerId, ct)
            && !await HasModeratorCapabilityAsync(organization.Id, ModeratorCapabilities.UsersRemove, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        if (organization.OwnerId == userId)
        {
            return Result.Failure("Organization owner cannot be removed.", 409);
        }

        var membership = await _organizationMemberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, ct);
        if (membership == null)
        {
            return Result.Failure("Organization member was not found.", 404);
        }

        try
        {
            await _organizationMemberRepo.DeleteAsync(membership, ct);
            await _unitOfWork.SaveChangesWithAuditAsync(
                _auditLogService,
                "RemoveMember",
                nameof(Organization),
                organizationId.ToString(),
                new { userId },
                ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("Organization was modified by another request.", 409);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> GetCurrentModeratorCapabilitiesAsync(Guid organizationId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null || !SystemRoleRules.IsModerator(_currentUserService.Role))
        {
            return Result.Success<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var now = DateTimeOffset.UtcNow;
        var capabilities = await _moderatorAssignmentRepo.GetQueryable().AsNoTracking()
            .Where(item => item.ModeratorUserId == currentUserId.Value && item.OrganizationId == organizationId
                && item.IsActive && item.RevokedAt == null && (item.ExpiresAt == null || item.ExpiresAt > now))
            .Select(item => item.Capability)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<string>>(capabilities);
    }

    private IQueryable<Organization> OrganizationDetailsQuery()
        => _organizationRepo.GetQueryable()
            .Include(item => item.Owner)
            .Include(item => item.Members)
            .Include(item => item.Projects);

    private async Task<bool> CanAccessOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return false;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin())
        {
            return true;
        }

        var organization = await _organizationRepo.GetQueryable()
            .AsNoTracking()
            .Where(item => item.Id == organizationId)
            .Select(item => new { item.IsActive })
            .FirstOrDefaultAsync(ct);
        if (organization == null || !organization.IsActive)
        {
            return false;
        }

        if (ownerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(item => item.OrganizationId == organizationId && item.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return false;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsSystemAdmin())
        {
            return true;
        }

        var organization = await _organizationRepo.GetQueryable()
            .AsNoTracking()
            .Where(item => item.Id == organizationId)
            .Select(item => new { item.IsActive })
            .FirstOrDefaultAsync(ct);
        if (organization == null || !organization.IsActive)
        {
            return false;
        }

        if (ownerId == currentUserId)
        {
            return true;
        }

        var role = await _organizationMemberRepo.GetQueryable()
            .Where(item => item.OrganizationId == organizationId && item.UserId == currentUserId)
            .Select(item => item.Role)
            .FirstOrDefaultAsync(ct);

        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private bool IsSystemAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

    private async Task<bool> HasActiveCurrentUserAsync(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        return await _userRepo.GetQueryable()
            .AnyAsync(user => user.Id == currentUserId.Value && user.IsActive, ct);
    }

    private async Task<bool> HasModeratorCapabilityAsync(Guid organizationId, string capability, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null || !SystemRoleRules.IsModerator(_currentUserService.Role))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        return await _moderatorAssignmentRepo.GetQueryable().AsNoTracking().AnyAsync(item =>
            item.ModeratorUserId == currentUserId.Value
            && item.OrganizationId == organizationId
            && item.Capability == capability
            && item.IsActive
            && item.RevokedAt == null
            && (item.ExpiresAt == null || item.ExpiresAt > now), ct);
    }

    private async Task<string> GenerateUniqueCodeAsync(string? requestedCode, string name, CancellationToken ct, Guid? currentOrganizationId = null)
    {
        var baseCode = Slugify(string.IsNullOrWhiteSpace(requestedCode) ? name : requestedCode);
        if (string.IsNullOrWhiteSpace(baseCode))
        {
            baseCode = "organization";
        }

        var candidate = baseCode;
        var suffix = 2;
        while (await _organizationRepo.GetQueryable().AnyAsync(item => item.Code == candidate && item.Id != currentOrganizationId, ct))
        {
            candidate = $"{baseCode}-{suffix++}";
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", string.Empty, RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"[\s-]+", "-", RegexOptions.CultureInvariant).Trim('-');
        return normalized.Length > 80 ? normalized[..80].Trim('-') : normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
