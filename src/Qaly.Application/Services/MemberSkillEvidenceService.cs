using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

/// <summary>
/// CAND-016 evidence ledger. Skill bands are calculated deterministically from
/// explicit contribution records; no model can invent, promote, or silently
/// attribute a skill to a person.
/// </summary>
public sealed class MemberSkillEvidenceService : IMemberSkillEvidenceService
{
    public const string EvidenceMethodVersion = "member-skill-evidence.v1";
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<TaskCompletionAttribution> _attributions;
    private readonly IRepository<Organization> _organizations;
    private readonly IRepository<OrganizationMember> _organizationMembers;
    private readonly IRepository<User> _users;
    private readonly ITaskAccessPolicy _taskAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _audit;

    public MemberSkillEvidenceService(
        IRepository<TaskItem> tasks,
        IRepository<TaskCompletionAttribution> attributions,
        IRepository<Organization> organizations,
        IRepository<OrganizationMember> organizationMembers,
        IRepository<User> users,
        ITaskAccessPolicy taskAccess,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IAuditLogService audit)
    {
        _tasks = tasks;
        _attributions = attributions;
        _organizations = organizations;
        _organizationMembers = organizationMembers;
        _users = users;
        _taskAccess = taskAccess;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<Result<TaskCompletionAttributionsDto>> GetTaskCompletionAttributionsAsync(
        Guid taskId,
        CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskId, tracking: false, ct);
        if (task == null || !await _taskAccess.CanAccessTaskAsync(task, ct))
        {
            return Result.NotFound<TaskCompletionAttributionsDto>();
        }

        var canManage = await _taskAccess.CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct);
        return Result.Success(ToTaskAttributionsDto(task, canManage, _currentUser.UserId));
    }

    public async Task<Result<TaskCompletionAttributionsDto>> ReplaceTaskCompletionAttributionsAsync(
        Guid taskId,
        ReplaceTaskCompletionAttributionsDto dto,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.NotFound<TaskCompletionAttributionsDto>();
        }

        var task = await LoadTaskAsync(taskId, tracking: true, ct);
        if (task == null || !await _taskAccess.CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
        {
            return Result.NotFound<TaskCompletionAttributionsDto>();
        }

        if (!IsDone(task.Status))
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "Completion contributors can only be confirmed after the task is Done.",
                409,
                AiErrorCodes.InvalidRequest);
        }
        if (task.SkillRequirements.Count == 0)
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "Confirm at least one task skill before creating skill evidence.",
                422,
                AiErrorCodes.SkillSemanticInvalid);
        }
        if (!dto.Confirmed)
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "Explicit confirmation is required before changing completion contributors.",
                400,
                AiErrorCodes.InvalidRequest);
        }
        if (!MatchesRowVersion(task.RowVersion, dto.TaskRowVersion))
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "The task changed in another session. Reload contributors before confirming.",
                409,
                AiErrorCodes.TaskSkillConcurrencyConflict);
        }

        var selected = (dto.ContributorUserIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();
        if (selected.Count > 20)
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "A task can contain at most 20 confirmed contributors.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var eligible = EligibleContributorIds(task);
        if (!selected.IsSubsetOf(eligible))
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "Every contributor must be an assignee of this completed task.",
                422,
                AiErrorCodes.SkillSemanticInvalid);
        }

        var before = task.CompletionAttributions
            .Select(item => new { item.ContributorUserId, item.Status, item.CompletedAt })
            .OrderBy(item => item.ContributorUserId)
            .ToList();
        var now = DateTimeOffset.UtcNow;
        var completedAt = task.UpdatedAt ?? task.CreatedAt;
        foreach (var attribution in task.CompletionAttributions)
        {
            if (selected.Contains(attribution.ContributorUserId))
            {
                attribution.Status = TaskCompletionAttribution.Confirmed;
                attribution.ConfirmedByUserId = currentUserId.Value;
                attribution.ConfirmedAt = now;
                attribution.CompletedAt = completedAt;
                attribution.CorrectionReason = null;
                attribution.CorrectionRequestedAt = null;
            }
            else if (!string.Equals(attribution.Status, TaskCompletionAttribution.Revoked, StringComparison.Ordinal))
            {
                attribution.Status = TaskCompletionAttribution.Revoked;
                attribution.CorrectionReason = null;
                attribution.CorrectionRequestedAt = null;
            }
        }

        foreach (var contributorId in selected.Where(id => task.CompletionAttributions.All(item => item.ContributorUserId != id)))
        {
            await _attributions.AddAsync(new TaskCompletionAttribution
            {
                TaskItemId = task.Id,
                ContributorUserId = contributorId,
                ConfirmedByUserId = currentUserId.Value,
                CompletedAt = completedAt,
                ConfirmedAt = now,
                Status = TaskCompletionAttribution.Confirmed,
                AttributionPolicyVersion = "completion-contributor.v1"
            }, ct);
        }

        task.UpdatedAt = now;
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<TaskCompletionAttributionsDto>(
                "The task changed in another session. Reload contributors before confirming.",
                409,
                AiErrorCodes.TaskSkillConcurrencyConflict);
        }

        await _audit.LogAsync(
            "ConfirmTaskCompletionContributors",
            nameof(TaskCompletionAttribution),
            task.Id.ToString(),
            new
            {
                task.ProjectId,
                TaskRowVersion = EncodeRowVersion(task.RowVersion),
                ConfirmationNote = Trim(dto.ConfirmationNote, 500),
                Before = before,
                ContributorUserIds = selected.OrderBy(id => id).ToArray(),
                EvidenceMethodVersion
            },
            ct);

        var reloaded = await LoadTaskAsync(taskId, tracking: false, ct);
        return Result.Success(ToTaskAttributionsDto(reloaded!, canManage: true, currentUserId));
    }

    public async Task<Result<TaskCompletionAttributionDto>> RequestCorrectionAsync(
        Guid taskId,
        Guid attributionId,
        RequestCompletionAttributionCorrectionDto dto,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.NotFound<TaskCompletionAttributionDto>();
        }

        var task = await LoadTaskAsync(taskId, tracking: true, ct);
        var attribution = task?.CompletionAttributions.FirstOrDefault(item => item.Id == attributionId);
        if (task == null || attribution == null || !await _taskAccess.CanAccessTaskAsync(task, ct))
        {
            return Result.NotFound<TaskCompletionAttributionDto>();
        }
        var canManage = await _taskAccess.CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct);
        if (!canManage && attribution.ContributorUserId != currentUserId.Value)
        {
            return Result.NotFound<TaskCompletionAttributionDto>();
        }
        if (!string.Equals(attribution.Status, TaskCompletionAttribution.Confirmed, StringComparison.Ordinal))
        {
            return Result.Failure<TaskCompletionAttributionDto>(
                "Only a confirmed attribution can be corrected.",
                409,
                AiErrorCodes.InvalidRequest);
        }
        if (!MatchesRowVersion(attribution.RowVersion, dto.RowVersion))
        {
            return Result.Failure<TaskCompletionAttributionDto>(
                "This attribution changed in another session. Reload before requesting a correction.",
                409,
                AiErrorCodes.TaskSkillConcurrencyConflict);
        }
        var reason = Trim(dto.Reason, 500);
        if (reason == null || reason.Length < 3)
        {
            return Result.Failure<TaskCompletionAttributionDto>("A correction reason of at least 3 characters is required.", 400);
        }

        attribution.Status = TaskCompletionAttribution.CorrectionRequested;
        attribution.CorrectionReason = reason;
        attribution.CorrectionRequestedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        await _audit.LogAsync(
            "RequestTaskCompletionAttributionCorrection",
            nameof(TaskCompletionAttribution),
            attribution.Id.ToString(),
            new { task.ProjectId, task.Id, attribution.ContributorUserId, reason },
            ct);

        return Result.Success(ToAttributionDto(attribution, canRequestCorrection: true));
    }

    public async Task<Result<MemberSkillProfileDto>> GetMemberSkillProfileAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId;
        if (!currentUserId.HasValue || !await CanAccessOrganizationAsync(organizationId, ct))
        {
            return Result.NotFound<MemberSkillProfileDto>();
        }
        var isMember = await IsOrganizationMemberAsync(organizationId, memberId, ct);
        if (!isMember)
        {
            return Result.NotFound<MemberSkillProfileDto>();
        }
        var canManage = await CanManageOrganizationAsync(organizationId, ct);
        if (memberId != currentUserId.Value && !canManage)
        {
            return Result.NotFound<MemberSkillProfileDto>();
        }

        var target = await _users.GetQueryable().AsNoTracking().FirstOrDefaultAsync(user => user.Id == memberId, ct);
        if (target == null)
        {
            return Result.NotFound<MemberSkillProfileDto>();
        }

        var rows = await _attributions.GetQueryable()
            .AsNoTracking()
            .Include(item => item.TaskItem)
                .ThenInclude(task => task.Project)
            .Include(item => item.TaskItem.Assignees)
            .Include(item => item.TaskItem.SkillRequirements)
                .ThenInclude(requirement => requirement.OrganizationSkill)
            .Where(item =>
                item.ContributorUserId == memberId &&
                item.Status == TaskCompletionAttribution.Confirmed &&
                !item.TaskItem.IsDeleted &&
                item.TaskItem.Project.OrganizationId == organizationId)
            .OrderByDescending(item => item.CompletedAt)
            .Take(500)
            .ToListAsync(ct);

        var sourceTaskIds = rows.Select(item => item.TaskItemId).Distinct().ToHashSet();
        var visibleSourceTaskIds = sourceTaskIds.Count == 0
            ? []
            : await _taskAccess.ApplyVisibilityFilter(_tasks.GetQueryable())
                .AsNoTracking()
                .Where(task => sourceTaskIds.Contains(task.Id))
                .Select(task => task.Id)
                .ToHashSetAsync(ct);

        var skillRows = rows
            .SelectMany(attribution => attribution.TaskItem.SkillRequirements.Select(requirement => new { attribution, requirement }))
            .Where(item => item.requirement.OrganizationSkill.OrganizationId == organizationId)
            .GroupBy(item => new { item.requirement.OrganizationSkillId, item.requirement.OrganizationSkill.Name });

        var skills = skillRows.Select(group =>
        {
            var evidence = group.ToList();
            var count = evidence.Select(item => item.attribution.TaskItemId).Distinct().Count();
            var maxLevel = evidence.Max(item => LevelRank(item.requirement.RequiredLevel));
            var latest = evidence.Max(item => item.attribution.CompletedAt);
            var restricted = evidence.Count(item => !visibleSourceTaskIds.Contains(item.attribution.TaskItemId));
            var sources = evidence
                .GroupBy(item => item.attribution.Id)
                .OrderByDescending(item => item.First().attribution.CompletedAt)
                .Select(item =>
                {
                    var first = item.First();
                    var allowed = visibleSourceTaskIds.Contains(first.attribution.TaskItemId);
                    return new MemberSkillEvidenceSourceDto(
                        first.attribution.Id,
                        first.attribution.TaskItemId,
                        allowed ? first.attribution.TaskItem.Title : null,
                        allowed ? $"/projects/{first.attribution.TaskItem.ProjectId}/tasks/{first.attribution.TaskItemId}" : null,
                        !allowed,
                        first.attribution.CompletedAt,
                        first.requirement.RequiredLevel);
                })
                .Take(12)
                .ToList();
            return new MemberSkillEvidenceDto(
                group.Key.OrganizationSkillId,
                group.Key.Name,
                CalculateEvidenceBand(count, maxLevel),
                CalculateConfidence(count, maxLevel),
                count,
                restricted,
                latest,
                latest < DateTimeOffset.UtcNow.AddDays(-180),
                sources);
        })
        .OrderByDescending(item => BandRank(item.EvidenceBand))
        .ThenByDescending(item => item.MostRecentCompletedAt)
        .ThenBy(item => item.SkillName, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var pending = await _attributions.GetQueryable()
            .CountAsync(item =>
                item.ContributorUserId == memberId &&
                item.Status == TaskCompletionAttribution.CorrectionRequested &&
                !item.TaskItem.IsDeleted &&
                item.TaskItem.Project.OrganizationId == organizationId,
                ct);
        return Result.Success(new MemberSkillProfileDto(
            organizationId,
            memberId,
            target.FullName,
            memberId == currentUserId.Value,
            canManage,
            EvidenceMethodVersion,
            skills,
            pending,
            skills.Count == 0
                ? "Chưa đủ bằng chứng hoàn thành đã được xác nhận. Đây không phải đánh giá năng lực thấp."
                : string.Empty));
    }

    private async Task<TaskItem?> LoadTaskAsync(Guid taskId, bool tracking, CancellationToken ct)
    {
        var query = _tasks.GetQueryable()
            .Include(task => task.Project)
            .Include(task => task.Assignee)
            .Include(task => task.Assignees).ThenInclude(item => item.User)
            .Include(task => task.SkillRequirements).ThenInclude(item => item.OrganizationSkill)
            .Include(task => task.CompletionAttributions).ThenInclude(item => item.ContributorUser)
            .Where(task => task.Id == taskId && !task.IsDeleted);
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct);
    }

    private static TaskCompletionAttributionsDto ToTaskAttributionsDto(
        TaskItem task,
        bool canManage,
        Guid? currentUserId)
    {
        var eligible = task.Assignees
            .Select(item => item.User)
            .Concat(task.Assignee == null ? [] : [task.Assignee])
            .Where(item => item != null)
            .DistinctBy(item => item!.Id)
            .OrderBy(item => item!.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new CompletionContributorCandidateDto(item!.Id, item.FullName, item.Email))
            .ToList();
        var eligibleForAttribution = IsDone(task.Status) && task.SkillRequirements.Count > 0 && eligible.Count > 0;
        var notice = !IsDone(task.Status)
            ? "Chỉ xác nhận đóng góp sau khi task đã hoàn thành."
            : task.SkillRequirements.Count == 0
                ? "Task chưa có kỹ năng đã xác nhận nên chưa thể tạo bằng chứng kỹ năng."
                : eligible.Count == 0
                    ? "Task chưa có người được giao để xác nhận đóng góp."
                    : null;
        return new TaskCompletionAttributionsDto(
            task.Id,
            task.Status,
            canManage,
            eligibleForAttribution,
            EncodeRowVersion(task.RowVersion),
            eligible,
            task.CompletionAttributions
                .OrderBy(item => item.ContributorUser.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(item => ToAttributionDto(
                    item,
                    canManage || (currentUserId.HasValue && item.ContributorUserId == currentUserId.Value)))
                .ToList(),
            notice);
    }

    private static TaskCompletionAttributionDto ToAttributionDto(
        TaskCompletionAttribution item,
        bool canRequestCorrection)
        => new(
            item.Id,
            item.ContributorUserId,
            item.ContributorUser.FullName,
            item.Status,
            item.CompletedAt,
            item.ConfirmedAt,
            item.ConfirmedByUserId,
            item.CorrectionReason,
            item.CorrectionRequestedAt,
            EncodeRowVersion(item.RowVersion),
            canRequestCorrection);

    private static HashSet<Guid> EligibleContributorIds(TaskItem task)
        => task.Assignees.Select(item => item.UserId)
            .Concat(task.AssigneeId.HasValue ? [task.AssigneeId.Value] : [])
            .ToHashSet();

    private async Task<bool> CanAccessOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return false;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role))
        {
            return await _organizations.GetQueryable().AnyAsync(item => item.Id == organizationId && item.IsActive, ct);
        }
        return await _organizations.GetQueryable().AnyAsync(item =>
            item.Id == organizationId && item.IsActive &&
            (item.OwnerId == userId.Value || item.Members.Any(member => member.UserId == userId.Value)), ct);
    }

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return false;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role)) return true;
        var organization = await _organizations.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization == null) return false;
        if (organization.OwnerId == userId.Value) return true;
        var role = await _organizationMembers.GetQueryable().AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.UserId == userId.Value)
            .Select(item => item.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private async Task<bool> IsOrganizationMemberAsync(Guid organizationId, Guid userId, CancellationToken ct)
        => await _organizations.GetQueryable().AnyAsync(item =>
            item.Id == organizationId && (item.OwnerId == userId || item.Members.Any(member => member.UserId == userId)), ct);

    private static int LevelRank(string value)
        => value switch
        {
            TaskSkillService.LevelExpert => 3,
            TaskSkillService.LevelProficient => 2,
            _ => 1
        };

    public static string CalculateEvidenceBand(int count, int maxLevel)
        => count >= 4 || (count >= 2 && maxLevel >= 3) ? "experienced"
            : count >= 2 || maxLevel >= 2 ? "practiced"
            : "emerging";

    private static int BandRank(string band)
        => band == "experienced" ? 3 : band == "practiced" ? 2 : 1;

    public static decimal CalculateConfidence(int count, int maxLevel)
        => Math.Min(0.95m, 0.25m + (0.15m * count) + (0.10m * maxLevel));

    private static bool IsDone(string? status)
        => string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesRowVersion(byte[] current, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(candidate)); }
        catch (FormatException) { return false; }
    }

    private static string EncodeRowVersion(byte[] rowVersion)
        => rowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(rowVersion);

    private static string? Trim(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is { Length: > 0 } ? normalized[..Math.Min(normalized.Length, max)] : null;
    }
}
