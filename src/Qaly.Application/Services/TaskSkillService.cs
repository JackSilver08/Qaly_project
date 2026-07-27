using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class TaskSkillService : ITaskSkillService
{
    public const string LevelFamiliar = "Familiar";
    public const string LevelProficient = "Proficient";
    public const string LevelExpert = "Expert";
    public const string ProvenanceManual = "MANUAL";
    public const string ProvenanceAiConfirmed = "AI_CONFIRMED";

    private readonly IRepository<Organization> _organizationRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<OrganizationSkill> _skillRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskSkillRequirement> _requirementRepo;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public TaskSkillService(
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<OrganizationSkill> skillRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskSkillRequirement> requirementRepo,
        ITaskAccessPolicy taskAccessPolicy,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService)
    {
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _skillRepo = skillRepo;
        _taskRepo = taskRepo;
        _requirementRepo = requirementRepo;
        _taskAccessPolicy = taskAccessPolicy;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<Result<IReadOnlyList<OrganizationSkillDto>>> GetOrganizationSkillsAsync(
        Guid organizationId,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (!await CanAccessOrganizationAsync(organizationId, ct))
        {
            return Result.NotFound<IReadOnlyList<OrganizationSkillDto>>();
        }

        var query = _skillRepo.GetQueryable()
            .AsNoTracking()
            .Where(skill => skill.OrganizationId == organizationId);
        if (!includeInactive)
        {
            query = query.Where(skill => skill.IsActive);
        }

        var normalizedSearch = NormalizeOptional(search);
        if (normalizedSearch != null)
        {
            query = query.Where(skill =>
                skill.Name.Contains(normalizedSearch) ||
                (skill.Description != null && skill.Description.Contains(normalizedSearch)));
        }

        var skills = await query
            .OrderByDescending(skill => skill.IsActive)
            .ThenBy(skill => skill.Name)
            .Take(200)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<OrganizationSkillDto>>(skills.Select(ToDto).ToList());
    }

    public async Task<Result<OrganizationSkillDto>> CreateOrganizationSkillAsync(
        Guid organizationId,
        CreateOrganizationSkillDto dto,
        CancellationToken ct = default)
    {
        if (!await CanManageOrganizationAsync(organizationId, ct))
        {
            return Result.NotFound<OrganizationSkillDto>();
        }

        var validation = ValidateName(dto.Name);
        if (!validation.IsSuccess)
        {
            return Result.Failure<OrganizationSkillDto>(validation.Error!, validation.StatusCode, validation.ErrorCode);
        }

        var name = CanonicalizeName(dto.Name);
        var normalizedName = NormalizeName(name);
        if (await _skillRepo.GetQueryable().AnyAsync(
                skill => skill.OrganizationId == organizationId && skill.NormalizedName == normalizedName,
                ct))
        {
            return Result.Failure<OrganizationSkillDto>(
                "A skill with this normalized name already exists.",
                409,
                AiErrorCodes.SkillCatalogConflict);
        }

        var skill = new OrganizationSkill
        {
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName,
            Description = NormalizeDescription(dto.Description),
            IsActive = true
        };

        await _skillRepo.AddAsync(skill, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<OrganizationSkillDto>(
                "A skill with this normalized name already exists.",
                409,
                AiErrorCodes.SkillCatalogConflict);
        }

        await _auditLogService.LogAsync(
            "CreateOrganizationSkill",
            nameof(OrganizationSkill),
            skill.Id.ToString(),
            new { skill.OrganizationId, skill.Name, skill.NormalizedName },
            ct);

        return Result.Created(ToDto(skill));
    }

    public async Task<Result<OrganizationSkillDto>> UpdateOrganizationSkillAsync(
        Guid organizationId,
        Guid skillId,
        UpdateOrganizationSkillDto dto,
        CancellationToken ct = default)
    {
        if (!await CanManageOrganizationAsync(organizationId, ct))
        {
            return Result.NotFound<OrganizationSkillDto>();
        }

        var skill = await _skillRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == skillId && item.OrganizationId == organizationId, ct);
        if (skill == null)
        {
            return Result.NotFound<OrganizationSkillDto>();
        }

        if (!MatchesRowVersion(skill.RowVersion, dto.RowVersion))
        {
            return Result.Failure<OrganizationSkillDto>(
                "The skill was modified by another request.",
                409,
                AiErrorCodes.SkillConcurrencyConflict);
        }

        var validation = ValidateName(dto.Name);
        if (!validation.IsSuccess)
        {
            return Result.Failure<OrganizationSkillDto>(validation.Error!, validation.StatusCode, validation.ErrorCode);
        }

        var name = CanonicalizeName(dto.Name);
        var normalizedName = NormalizeName(name);
        var duplicate = await _skillRepo.GetQueryable().AnyAsync(
            item => item.OrganizationId == organizationId &&
                    item.Id != skill.Id &&
                    item.NormalizedName == normalizedName,
            ct);
        if (duplicate)
        {
            return Result.Failure<OrganizationSkillDto>(
                "A skill with this normalized name already exists.",
                409,
                AiErrorCodes.SkillCatalogConflict);
        }

        var before = new { skill.Name, skill.Description, skill.IsActive };
        skill.Name = name;
        skill.NormalizedName = normalizedName;
        skill.Description = NormalizeDescription(dto.Description);
        skill.IsActive = dto.IsActive;
        skill.UpdatedAt = DateTimeOffset.UtcNow;
        await _skillRepo.UpdateAsync(skill, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<OrganizationSkillDto>(
                "The skill was modified by another request.",
                409,
                AiErrorCodes.SkillConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<OrganizationSkillDto>(
                "A skill with this normalized name already exists.",
                409,
                AiErrorCodes.SkillCatalogConflict);
        }

        await _auditLogService.LogAsync(
            "UpdateOrganizationSkill",
            nameof(OrganizationSkill),
            skill.Id.ToString(),
            new { before, after = new { skill.Name, skill.Description, skill.IsActive } },
            ct);

        return Result.Success(ToDto(skill));
    }

    public async Task<Result<TaskSkillsDto>> GetTaskSkillsAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskId, tracking: false, ct);
        if (task == null || !await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
        {
            return Result.NotFound<TaskSkillsDto>();
        }

        var canManage = await _taskAccessPolicy.CanManageTaskAsync(task, ct);
        var canManageCatalog = task.Project.OrganizationId.HasValue &&
            await CanManageOrganizationAsync(task.Project.OrganizationId.Value, ct);
        var availability = task.Project.OrganizationId.HasValue
            ? await _skillRepo.GetQueryable().AnyAsync(
                skill => skill.OrganizationId == task.Project.OrganizationId.Value && skill.IsActive,
                ct)
                ? "ready"
                : "catalog_empty"
            : "organization_required";

        return Result.Success(ToTaskSkillsDto(task, availability, canManage, canManageCatalog));
    }

    public async Task<Result<TaskSkillsDto>> ReplaceTaskSkillsAsync(
        Guid taskId,
        ReplaceTaskSkillsDto dto,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.NotFound<TaskSkillsDto>();
        }

        var task = await LoadTaskAsync(taskId, tracking: true, ct);
        if (task == null || !await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.NotFound<TaskSkillsDto>();
        }

        if (!task.Project.OrganizationId.HasValue)
        {
            return Result.Failure<TaskSkillsDto>(
                "This project must belong to an organization before task skills can be managed.",
                422,
                AiErrorCodes.SkillOrganizationRequired);
        }

        if (dto.TaskRowVersion == null ||
            !MatchesRowVersion(task.RowVersion, dto.TaskRowVersion))
        {
            return Result.Failure<TaskSkillsDto>(
                "The task was modified by another request.",
                409,
                AiErrorCodes.TaskSkillConcurrencyConflict);
        }

        var selections = dto.Skills ?? [];
        if (selections.Count > 20 || selections.Any(item => item.SkillId == Guid.Empty))
        {
            return Result.Failure<TaskSkillsDto>("A task can contain at most 20 valid skills.", 400);
        }
        if (selections.Select(item => item.SkillId).Distinct().Count() != selections.Count)
        {
            return Result.Failure<TaskSkillsDto>("Duplicate skills are not allowed.", 400);
        }

        var normalizedSelections = new List<(Guid SkillId, string Level)>(selections.Count);
        foreach (var selection in selections)
        {
            if (!TryNormalizeLevel(selection.RequiredLevel, out var level))
            {
                return Result.Failure<TaskSkillsDto>(
                    "requiredLevel must be Familiar, Proficient, or Expert.",
                    400);
            }
            normalizedSelections.Add((selection.SkillId, level));
        }

        var skillIds = normalizedSelections.Select(item => item.SkillId).ToList();
        var validSkills = await _skillRepo.GetQueryable()
            .Where(skill =>
                skill.OrganizationId == task.Project.OrganizationId.Value &&
                skill.IsActive &&
                skillIds.Contains(skill.Id))
            .ToListAsync(ct);
        if (validSkills.Count != skillIds.Count)
        {
            return Result.Failure<TaskSkillsDto>(
                "One or more skills are inactive, absent, or outside this organization.",
                422,
                AiErrorCodes.SkillSemanticInvalid);
        }

        var before = task.SkillRequirements.Select(requirement => new
        {
            requirement.OrganizationSkillId,
            requirement.RequiredLevel,
            requirement.Provenance
        }).ToList();
        foreach (var requirement in task.SkillRequirements.ToList())
        {
            await _requirementRepo.HardDeleteAsync(requirement, ct);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var selection in normalizedSelections)
        {
            await _requirementRepo.AddAsync(new TaskSkillRequirement
            {
                TaskItemId = task.Id,
                OrganizationSkillId = selection.SkillId,
                RequiredLevel = selection.Level,
                Provenance = ProvenanceManual,
                ConfirmedByUserId = currentUserId.Value,
                ConfirmedAt = now
            }, ct);
        }

        task.UpdatedAt = now;
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<TaskSkillsDto>(
                "The task skills were modified by another request.",
                409,
                AiErrorCodes.TaskSkillConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<TaskSkillsDto>(
                "The task skill selection conflicts with the current catalog.",
                409,
                AiErrorCodes.SkillCatalogConflict);
        }

        await _auditLogService.LogAsync(
            "ReplaceTaskSkills",
            nameof(TaskItem),
            task.Id.ToString(),
            new
            {
                task.ProjectId,
                before,
                after = normalizedSelections.Select(item => new { item.SkillId, RequiredLevel = item.Level })
            },
            ct);

        var reloaded = await LoadTaskAsync(taskId, tracking: false, ct);
        var canManageCatalog = await CanManageOrganizationAsync(task.Project.OrganizationId.Value, ct);
        return Result.Success(ToTaskSkillsDto(reloaded!, "ready", canManage: true, canManageCatalog));
    }

    public static bool TryNormalizeLevel(string? value, out string normalized)
    {
        var trimmed = value?.Trim();
        if (string.Equals(trimmed, LevelFamiliar, StringComparison.OrdinalIgnoreCase))
        {
            normalized = LevelFamiliar;
            return true;
        }
        if (string.Equals(trimmed, LevelProficient, StringComparison.OrdinalIgnoreCase))
        {
            normalized = LevelProficient;
            return true;
        }
        if (string.Equals(trimmed, LevelExpert, StringComparison.OrdinalIgnoreCase))
        {
            normalized = LevelExpert;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private async Task<TaskItem?> LoadTaskAsync(Guid taskId, bool tracking, CancellationToken ct)
    {
        var query = _taskRepo.GetQueryable()
            .Include(task => task.Project)
                .ThenInclude(project => project.Organization)
            .Include(task => task.Assignees)
            .Include(task => task.SkillRequirements)
                .ThenInclude(requirement => requirement.OrganizationSkill)
            .Where(task => task.Id == taskId && !task.IsDeleted);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<bool> CanAccessOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return false;
        }
        if (ProjectRoleRules.IsSystemAdmin(_currentUserService.Role))
        {
            return await _organizationRepo.GetQueryable().AnyAsync(
                organization => organization.Id == organizationId && organization.IsActive,
                ct);
        }
        return await _organizationRepo.GetQueryable().AnyAsync(
            organization =>
                organization.Id == organizationId &&
                organization.IsActive &&
                (organization.OwnerId == currentUserId.Value ||
                 organization.Members.Any(member => member.UserId == currentUserId.Value)),
            ct);
    }

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return false;
        }
        if (ProjectRoleRules.IsSystemAdmin(_currentUserService.Role))
        {
            return await _organizationRepo.GetQueryable().AnyAsync(
                organization => organization.Id == organizationId && organization.IsActive,
                ct);
        }

        var organization = await _organizationRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization == null)
        {
            return false;
        }
        if (organization.OwnerId == currentUserId.Value)
        {
            return true;
        }

        var role = await _organizationMemberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member =>
                member.OrganizationId == organizationId &&
                member.UserId == currentUserId.Value)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private static Result ValidateName(string? value)
    {
        var name = CanonicalizeName(value ?? string.Empty);
        if (name.Length is < 2 or > 100)
        {
            return Result.Failure("Skill name must contain between 2 and 100 characters.", 400);
        }
        return Result.Success();
    }

    private static string CanonicalizeName(string value)
        => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ");

    private static string NormalizeName(string value)
        => CanonicalizeName(value).ToLowerInvariant();

    private static string? NormalizeDescription(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized is { Length: > 500 } ? normalized[..500] : normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool MatchesRowVersion(byte[] current, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return current.Length == 0 && candidate != null;
        }
        try
        {
            return current.SequenceEqual(Convert.FromBase64String(candidate));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeRowVersion(byte[] rowVersion)
        => rowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(rowVersion);

    private static OrganizationSkillDto ToDto(OrganizationSkill skill)
        => new(
            skill.Id,
            skill.OrganizationId,
            skill.Name,
            skill.NormalizedName,
            skill.Description,
            skill.IsActive,
            EncodeRowVersion(skill.RowVersion));

    private static TaskSkillsDto ToTaskSkillsDto(
        TaskItem task,
        string availability,
        bool canManage,
        bool canManageCatalog)
        => new(
            task.Id,
            task.ProjectId,
            task.Project.OrganizationId,
            availability,
            canManage,
            canManageCatalog,
            EncodeRowVersion(task.RowVersion),
            task.SkillRequirements
                .OrderBy(requirement => requirement.OrganizationSkill.Name)
                .Select(requirement => new TaskSkillRequirementDto(
                    requirement.Id,
                    requirement.OrganizationSkillId,
                    requirement.OrganizationSkill.Name,
                    requirement.OrganizationSkill.Description,
                    requirement.RequiredLevel,
                    requirement.Provenance,
                    requirement.ConfirmedByUserId,
                    requirement.ConfirmedAt,
                    EncodeRowVersion(requirement.RowVersion)))
                .ToList());
}
