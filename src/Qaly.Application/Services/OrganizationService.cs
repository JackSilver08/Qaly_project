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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public OrganizationService(
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<User> userRepo,
        IRepository<ModeratorAssignment> moderatorAssignmentRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _userRepo = userRepo;
        _moderatorAssignmentRepo = moderatorAssignmentRepo;
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

        var organization = new Organization
        {
            Name = dto.Name.Trim(),
            Code = await GenerateUniqueCodeAsync(dto.Code, dto.Name, ct),
            Description = NormalizeOptional(dto.Description),
            OwnerId = currentUserId.Value,
            IsActive = true
        };

        await _organizationRepo.AddAsync(organization, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = currentUserId.Value,
            Role = OrganizationRoleRules.Owner
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(Organization), organization.Id.ToString(), new { organization.Name, organization.Code }, ct);

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

        await _organizationRepo.UpdateAsync(organization, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(Organization), organization.Id.ToString(), dto, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Deactivate", nameof(Organization), organization.Id.ToString(), new { organization.Name }, ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<OrganizationMemberDto>>> GetMembersAsync(Guid organizationId, CancellationToken ct = default)
    {
        var organization = await _organizationRepo.GetByIdAsync(organizationId, ct);
        if (organization == null)
        {
            return Result.NotFound<IReadOnlyList<OrganizationMemberDto>>();
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

        var result = members
            .Select(item => new OrganizationMemberDto(
                item.UserId,
                item.User.FullName,
                item.User.Email,
                item.Role,
                item.JoinedAt))
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
            return Result.Failure("Organization owner role cannot be downgraded by this action.", 400);
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
            existing.Role = normalizedRole;
            await _organizationMemberRepo.UpdateAsync(existing, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _auditLogService.LogAsync("UpdateMemberRole", nameof(Organization), organizationId.ToString(), new { userId, role = normalizedRole }, ct);
            return Result.Success();
        }

        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = normalizedRole
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AddMember", nameof(Organization), organizationId.ToString(), new { userId, role = normalizedRole }, ct);

        return Result.Success();
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
            return Result.Failure("Organization owner cannot be removed.", 400);
        }

        var membership = await _organizationMemberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, ct);
        if (membership == null)
        {
            return Result.Failure("Organization member was not found.", 404);
        }

        await _organizationMemberRepo.DeleteAsync(membership, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RemoveMember", nameof(Organization), organizationId.ToString(), new { userId }, ct);
        return Result.Success();
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
            .AnyAsync(item => item.OrganizationId == organizationId && item.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
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

        var role = await _organizationMemberRepo.GetQueryable()
            .Where(item => item.OrganizationId == organizationId && item.UserId == currentUserId)
            .Select(item => item.Role)
            .FirstOrDefaultAsync(ct);

        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private bool IsSystemAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

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
