using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Organization;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class ProfessionalProfileService : IProfessionalProfileService
{
    public const string AuthorizationNotice = "Hồ sơ nghề nghiệp hỗ trợ tìm kiếm và xếp hạng staffing; không cấp quyền truy cập hệ thống, tổ chức hoặc Project.";

    private readonly IRepository<Organization> _organizations;
    private readonly IRepository<OrganizationMember> _members;
    private readonly IRepository<User> _users;
    private readonly IRepository<ProfessionalProfileDefinition> _definitions;
    private readonly IRepository<OrganizationMemberProfessionalProfile> _profiles;
    private readonly IRepository<ModeratorAssignment> _moderatorAssignments;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _audit;

    public ProfessionalProfileService(
        IRepository<Organization> organizations,
        IRepository<OrganizationMember> members,
        IRepository<User> users,
        IRepository<ProfessionalProfileDefinition> definitions,
        IRepository<OrganizationMemberProfessionalProfile> profiles,
        IRepository<ModeratorAssignment> moderatorAssignments,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IAuditLogService audit)
    {
        _organizations = organizations;
        _members = members;
        _users = users;
        _definitions = definitions;
        _profiles = profiles;
        _moderatorAssignments = moderatorAssignments;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ProfessionalProfileDefinitionDto>>> GetDefinitionsAsync(
        Guid organizationId,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (!await CanAccessAsync(organizationId, ct))
        {
            return Result.NotFound<IReadOnlyList<ProfessionalProfileDefinitionDto>>();
        }

        var query = _definitions.GetQueryable().AsNoTracking()
            .Where(item => item.OrganizationId == organizationId);
        if (!includeInactive) query = query.Where(item => item.IsActive);
        var result = await query.OrderBy(item => item.Category).ThenBy(item => item.Name)
            .Select(item => ToDefinitionDto(item))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ProfessionalProfileDefinitionDto>>(result);
    }

    public async Task<Result<ProfessionalProfileDefinitionDto>> CreateDefinitionAsync(
        Guid organizationId,
        CreateProfessionalProfileDefinitionDto dto,
        CancellationToken ct = default)
    {
        if (!await CanManageAsync(organizationId, ct)) return Result.NotFound<ProfessionalProfileDefinitionDto>();
        var validation = ValidateDefinition(dto.Name, dto.Category, dto.Description);
        if (validation != null) return Result.Failure<ProfessionalProfileDefinitionDto>(validation, 400);

        var key = ProfessionalProfileCatalog.ToKey(string.IsNullOrWhiteSpace(dto.Key) ? dto.Name : dto.Key);
        if (key.Length is < 2 or > 80) return Result.Failure<ProfessionalProfileDefinitionDto>("Professional profile key must contain 2-80 URL-safe characters.", 400);
        if (await _definitions.GetQueryable().AnyAsync(item => item.OrganizationId == organizationId && item.Key == key, ct))
        {
            return Result.Failure<ProfessionalProfileDefinitionDto>("Professional profile key already exists in this organization.", 409);
        }

        var entity = new ProfessionalProfileDefinition
        {
            OrganizationId = organizationId,
            Key = key,
            Name = dto.Name.Trim(),
            Category = dto.Category.Trim(),
            Description = Trim(dto.Description, 600),
            IsActive = true
        };
        await _definitions.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _audit,
            "CreateProfessionalProfileDefinition",
            nameof(ProfessionalProfileDefinition),
            entity.Id.ToString(),
            new { organizationId, entity.Key, entity.Name },
            ct);
        return Result.Created(ToDefinitionDto(entity));
    }

    public async Task<Result<ProfessionalProfileDefinitionDto>> UpdateDefinitionAsync(
        Guid organizationId,
        Guid definitionId,
        UpdateProfessionalProfileDefinitionDto dto,
        CancellationToken ct = default)
    {
        if (!await CanManageAsync(organizationId, ct)) return Result.NotFound<ProfessionalProfileDefinitionDto>();
        var entity = await _definitions.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == definitionId && item.OrganizationId == organizationId, ct);
        if (entity == null) return Result.NotFound<ProfessionalProfileDefinitionDto>();
        if (!MatchesRowVersion(entity.RowVersion, dto.RowVersion))
        {
            return Result.Failure<ProfessionalProfileDefinitionDto>("Professional profile changed in another session. Reload before saving.", 409);
        }
        var validation = ValidateDefinition(dto.Name, dto.Category, dto.Description);
        if (validation != null) return Result.Failure<ProfessionalProfileDefinitionDto>(validation, 400);

        entity.Name = dto.Name.Trim();
        entity.Category = dto.Category.Trim();
        entity.Description = Trim(dto.Description, 600);
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await _unitOfWork.SaveChangesWithAuditAsync(
                _audit,
                "UpdateProfessionalProfileDefinition",
                nameof(ProfessionalProfileDefinition),
                entity.Id.ToString(),
                new { organizationId, entity.Name, entity.IsActive },
                ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ProfessionalProfileDefinitionDto>("Professional profile changed in another session. Reload before saving.", 409);
        }
        return Result.Success(ToDefinitionDto(entity));
    }

    public async Task<Result<MemberProfessionalProfileSetDto>> GetMemberProfilesAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken ct = default)
    {
        var context = await LoadContextAsync(organizationId, userId, ct);
        if (context == null || !await CanAccessAsync(organizationId, ct))
        {
            return Result.NotFound<MemberProfessionalProfileSetDto>();
        }
        return Result.Success(await ToProfileSetAsync(context.Value.Organization, context.Value.Member, ct));
    }

    public async Task<Result<MemberProfessionalProfileSetDto>> ReplaceMemberProfilesAsync(
        Guid organizationId,
        Guid userId,
        ReplaceMemberProfessionalProfilesDto dto,
        CancellationToken ct = default)
    {
        var actorId = _currentUser.UserId;
        var context = await LoadContextAsync(organizationId, userId, ct);
        if (!actorId.HasValue || context == null || !dto.Confirmed)
        {
            return context == null
                ? Result.NotFound<MemberProfessionalProfileSetDto>()
                : Result.Failure<MemberProfessionalProfileSetDto>("Explicit confirmation is required before changing professional profiles.", 400);
        }
        var canManage = await CanManageAsync(organizationId, ct);
        var isSelf = actorId.Value == userId;
        if (!canManage && !isSelf) return Result.NotFound<MemberProfessionalProfileSetDto>();

        var selections = dto.Profiles ?? [];
        if (selections.Count > 30 || selections.Select(item => item.DefinitionId).Distinct().Count() != selections.Count)
        {
            return Result.Failure<MemberProfessionalProfileSetDto>("Select no more than 30 unique professional profiles.", 400);
        }
        var definitionIds = selections.Select(item => item.DefinitionId).ToHashSet();
        var definitions = await _definitions.GetQueryable().AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.IsActive && definitionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, ct);
        if (definitions.Count != definitionIds.Count)
        {
            return Result.Failure<MemberProfessionalProfileSetDto>("Every selected professional profile must be active and belong to this organization.", 422);
        }

        var existing = await _profiles.GetQueryable()
            .Where(item => item.OrganizationId == organizationId && item.UserId == userId)
            .ToListAsync(ct);
        var mutableExisting = canManage
            ? existing
            : existing.Where(item => item.VerificationStatus == OrganizationMemberProfessionalProfile.Declared).ToList();
        var knownRows = (dto.KnownRows ?? []).GroupBy(item => item.AssignmentId).ToDictionary(group => group.Key, group => group.Last().RowVersion);
        if (mutableExisting.Any(item => !knownRows.TryGetValue(item.Id, out var rowVersion) || !MatchesRowVersion(item.RowVersion, rowVersion)))
        {
            return Result.Failure<MemberProfessionalProfileSetDto>("Professional profiles changed in another session. Reload before saving.", 409);
        }

        var normalized = new List<(MemberProfessionalProfileSelectionDto Selection, string Proficiency, string Status, string Source)>();
        foreach (var selection in selections)
        {
            if (!ProfessionalProfileCatalog.TryNormalizeProficiency(selection.Proficiency, out var proficiency)
                || !ProfessionalProfileCatalog.TryNormalizeStatus(selection.VerificationStatus, out var status))
            {
                return Result.Failure<MemberProfessionalProfileSetDto>("Professional profile proficiency or verification status is invalid.", 400);
            }
            if (!canManage && status != OrganizationMemberProfessionalProfile.Declared)
            {
                return Result.Failure<MemberProfessionalProfileSetDto>("Members may declare their own profiles, but only an organization manager can verify or reject them.", 403);
            }
            var source = canManage && ProfessionalProfileCatalog.TryNormalizeSource(selection.Source, out var requestedSource)
                ? requestedSource
                : canManage ? ProfessionalProfileCatalog.ManagerConfirmed : ProfessionalProfileCatalog.MemberDeclared;
            if (status == OrganizationMemberProfessionalProfile.Verified) source = ProfessionalProfileCatalog.ManagerConfirmed;
            if (selection.EffectiveFrom.HasValue && selection.EffectiveTo.HasValue && selection.EffectiveTo <= selection.EffectiveFrom)
            {
                return Result.Failure<MemberProfessionalProfileSetDto>("Professional profile end date must be after its start date.", 400);
            }
            normalized.Add((selection, proficiency, status, source));
        }

        var before = existing.Select(item => new { item.ProfessionalProfileDefinitionId, item.Proficiency, item.VerificationStatus, item.Source }).ToList();
        var now = DateTimeOffset.UtcNow;
        foreach (var entity in mutableExisting.Where(item => normalized.All(next => next.Selection.DefinitionId != item.ProfessionalProfileDefinitionId)))
        {
            await _profiles.DeleteAsync(entity, ct);
        }
        foreach (var item in normalized)
        {
            var entity = existing.FirstOrDefault(current => current.ProfessionalProfileDefinitionId == item.Selection.DefinitionId);
            if (entity != null && !canManage && entity.VerificationStatus != OrganizationMemberProfessionalProfile.Declared)
            {
                continue;
            }
            entity ??= new OrganizationMemberProfessionalProfile
            {
                OrganizationId = organizationId,
                UserId = userId,
                ProfessionalProfileDefinitionId = item.Selection.DefinitionId
            };
            entity.Proficiency = item.Proficiency;
            entity.VerificationStatus = item.Status;
            entity.Source = item.Source;
            entity.EffectiveFrom = item.Selection.EffectiveFrom ?? entity.EffectiveFrom;
            entity.EffectiveTo = item.Selection.EffectiveTo;
            entity.Note = Trim(item.Selection.Note, 500);
            entity.UpdatedAt = now;
            if (item.Status == OrganizationMemberProfessionalProfile.Verified)
            {
                entity.VerifiedByUserId = actorId.Value;
                entity.VerifiedAt = now;
            }
            else
            {
                entity.VerifiedByUserId = null;
                entity.VerifiedAt = null;
            }
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            if (!existing.Contains(entity)) await _profiles.AddAsync(entity, ct);
        }

        try
        {
            await _unitOfWork.SaveChangesWithAuditAsync(
                _audit,
                "ReplaceMemberProfessionalProfiles",
                nameof(OrganizationMemberProfessionalProfile),
                userId.ToString(),
                new
                {
                    organizationId,
                    TargetUserId = userId,
                    ActorUserId = actorId,
                    Before = before,
                    After = normalized.Select(item => new { item.Selection.DefinitionId, item.Proficiency, item.Status, item.Source }),
                    AuthorizationNotice
                },
                ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<MemberProfessionalProfileSetDto>("Professional profiles changed in another session. Reload before saving.", 409);
        }
        var reloaded = await LoadContextAsync(organizationId, userId, ct);
        return Result.Success(await ToProfileSetAsync(reloaded!.Value.Organization, reloaded.Value.Member, ct));
    }

    private async Task<(Organization Organization, OrganizationMember Member)?> LoadContextAsync(Guid organizationId, Guid userId, CancellationToken ct)
    {
        var organization = await _organizations.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization == null) return null;
        var member = await _members.GetQueryable().AsNoTracking().Include(item => item.User)
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId && item.User.IsActive, ct);
        return member == null ? null : (organization, member);
    }

    private async Task<MemberProfessionalProfileSetDto> ToProfileSetAsync(Organization organization, OrganizationMember member, CancellationToken ct)
    {
        var canManage = await CanManageAsync(organization.Id, ct);
        var isSelf = _currentUser.UserId == member.UserId;
        var profiles = await _profiles.GetQueryable().AsNoTracking()
            .Include(item => item.ProfessionalProfileDefinition)
            .Include(item => item.VerifiedByUser)
            .Where(item => item.OrganizationId == organization.Id && item.UserId == member.UserId)
            .OrderBy(item => item.ProfessionalProfileDefinition.Category)
            .ThenBy(item => item.ProfessionalProfileDefinition.Name)
            .ToListAsync(ct);
        return new MemberProfessionalProfileSetDto(
            organization.Id,
            member.UserId,
            member.User.FullName,
            OrganizationRoleRules.TryNormalizeKnownRole(member.Role, out var accessRole) ? accessRole : "Restricted",
            isSelf,
            canManage,
            AuthorizationNotice,
            profiles.Select(item => new MemberProfessionalProfileDto(
                item.Id,
                item.ProfessionalProfileDefinitionId,
                item.ProfessionalProfileDefinition.Key,
                item.ProfessionalProfileDefinition.Name,
                item.ProfessionalProfileDefinition.Category,
                item.ProfessionalProfileDefinition.Description,
                item.Proficiency,
                item.VerificationStatus,
                item.Source,
                item.EffectiveFrom,
                item.EffectiveTo,
                item.VerifiedByUserId,
                item.VerifiedByUser?.FullName,
                item.VerifiedAt,
                item.Note,
                EncodeRowVersion(item.RowVersion),
                canManage || (isSelf && item.VerificationStatus == OrganizationMemberProfessionalProfile.Declared))).ToList());
    }

    private async Task<bool> CanAccessAsync(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return false;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role))
        {
            return await _organizations.GetQueryable().AnyAsync(item => item.Id == organizationId && item.IsActive, ct);
        }
        if (await _organizations.GetQueryable().AnyAsync(item => item.Id == organizationId && item.IsActive
            && (item.OwnerId == userId || item.Members.Any(member => member.UserId == userId)), ct))
        {
            return true;
        }
        return await HasModeratorCapabilityAsync(organizationId, ModeratorCapabilities.ProfessionalProfilesView, ct)
            || await HasModeratorCapabilityAsync(organizationId, ModeratorCapabilities.ProfessionalProfilesManage, ct);
    }

    private async Task<bool> CanManageAsync(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return false;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role)) return true;
        var organization = await _organizations.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization == null) return false;
        if (organization.OwnerId == userId.Value) return true;
        var role = await _members.GetQueryable().AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.UserId == userId.Value)
            .Select(item => item.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageOrganization(role)
            || await HasModeratorCapabilityAsync(organizationId, ModeratorCapabilities.ProfessionalProfilesManage, ct);
    }

    private async Task<bool> HasModeratorCapabilityAsync(Guid organizationId, string capability, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || !SystemRoleRules.IsModerator(_currentUser.Role)) return false;
        var now = DateTimeOffset.UtcNow;
        return await _moderatorAssignments.GetQueryable().AsNoTracking().AnyAsync(item =>
            item.ModeratorUserId == userId.Value
            && item.OrganizationId == organizationId
            && item.Capability == capability
            && item.IsActive
            && item.RevokedAt == null
            && (item.ExpiresAt == null || item.ExpiresAt > now), ct);
    }

    private static string? ValidateDefinition(string? name, string? category, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 120) return "Professional profile name must contain 2-120 characters.";
        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length is < 2 or > 80) return "Professional profile category must contain 2-80 characters.";
        if (description?.Trim().Length > 600) return "Professional profile description cannot exceed 600 characters.";
        return null;
    }

    private static ProfessionalProfileDefinitionDto ToDefinitionDto(ProfessionalProfileDefinition item)
        => new(item.Id, item.OrganizationId, item.Key, item.Name, item.Description, item.Category, item.IsSystemSeed, item.IsActive, EncodeRowVersion(item.RowVersion));

    private static string EncodeRowVersion(byte[] value) => value.Length == 0 ? string.Empty : Convert.ToBase64String(value);
    private static bool MatchesRowVersion(byte[] current, string? candidate)
    {
        if (current.Length == 0) return string.IsNullOrEmpty(candidate);
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(candidate)); }
        catch (FormatException) { return false; }
    }
    private static string? Trim(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is { Length: > 0 } ? normalized[..Math.Min(normalized.Length, max)] : null;
    }
}
