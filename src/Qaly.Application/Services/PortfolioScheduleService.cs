using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class PortfolioScheduleService : IPortfolioScheduleService
{
    public const string SchemaId = "assignment_schedule_proposal.v1";
    public const string ScoringVersion = "portfolio-capacity-scheduler.v1";
    public const string DraftType = "AssignmentScheduleProposal";
    private const int MissingEstimateFallbackHours = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<Project> _projects;
    private readonly IRepository<ProjectMember> _projectMembers;
    private readonly IRepository<Organization> _organizations;
    private readonly IRepository<OrganizationMember> _organizationMembers;
    private readonly IRepository<OrganizationMemberCapacityProfile> _profiles;
    private readonly IRepository<MemberAvailabilityWindow> _availability;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<TaskAssignment> _assignments;
    private readonly IRepository<User> _users;
    private readonly IRepository<AiJob> _jobs;
    private readonly IRepository<AiGeneratedDraft> _drafts;
    private readonly IRepository<AiJobSource> _sources;
    private readonly IRepository<AiUsageLedger> _usage;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public PortfolioScheduleService(
        IRepository<Project> projects,
        IRepository<ProjectMember> projectMembers,
        IRepository<Organization> organizations,
        IRepository<OrganizationMember> organizationMembers,
        IRepository<OrganizationMemberCapacityProfile> profiles,
        IRepository<MemberAvailabilityWindow> availability,
        IRepository<TaskItem> tasks,
        IRepository<TaskAssignment> assignments,
        IRepository<User> users,
        IRepository<AiJob> jobs,
        IRepository<AiGeneratedDraft> drafts,
        IRepository<AiJobSource> sources,
        IRepository<AiUsageLedger> usage,
        ITaskAccessPolicy taskAccessPolicy,
        ICurrentUserService currentUser,
        IAuditLogService audit,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _projectMembers = projectMembers;
        _organizations = organizations;
        _organizationMembers = organizationMembers;
        _profiles = profiles;
        _availability = availability;
        _tasks = tasks;
        _assignments = assignments;
        _users = users;
        _jobs = jobs;
        _drafts = drafts;
        _sources = sources;
        _usage = usage;
        _taskAccessPolicy = taskAccessPolicy;
        _currentUser = currentUser;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PortfolioCapacityDto>> GetCapacityAsync(
        Guid projectId,
        DateTimeOffset? from,
        DateTimeOffset? windowEnd,
        CancellationToken ct = default)
    {
        var scope = await ResolveManagedPortfolioAsync(projectId, ct);
        if (!scope.IsSuccess)
        {
            return Result.Failure<PortfolioCapacityDto>(scope.Error!, scope.StatusCode, scope.ErrorCode);
        }

        var window = NormalizeWindow(from, windowEnd);
        if (!window.IsSuccess)
        {
            return Result.Failure<PortfolioCapacityDto>(window.Error!, window.StatusCode, window.ErrorCode);
        }

        var snapshot = await BuildCapacityAsync(scope.Data!, window.Data.Start, window.Data.End, ct);
        return Result.Success(new PortfolioCapacityDto(
            projectId,
            scope.Data!.OrganizationId,
            window.Data.Start,
            window.Data.End,
            ScoringVersion,
            snapshot.HasRestrictedLoad ? "partial_private_aggregate" : "authorized_organization_portfolio",
            true,
            true,
            snapshot.Members,
            DateTimeOffset.UtcNow));
    }

    public async Task<Result<PortfolioMemberCapacityDto>> UpdateCapacityProfileAsync(
        Guid organizationId,
        Guid userId,
        UpdateMemberCapacityProfileDto dto,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Forbidden<PortfolioMemberCapacityDto>();
        }

        var canManage = await CanManageOrganizationAsync(organizationId, currentUserId.Value, ct);
        if (!canManage && currentUserId.Value != userId)
        {
            return Result.NotFound<PortfolioMemberCapacityDto>();
        }

        if (!dto.Confirmed)
        {
            return Result.Failure<PortfolioMemberCapacityDto>("Explicit confirmation is required.", 400, AiErrorCodes.InvalidRequest);
        }

        if (dto.WeeklyCapacityHours is < 1m or > 168m ||
            string.IsNullOrWhiteSpace(dto.TimeZoneId) || dto.TimeZoneId.Trim().Length > 100 ||
            dto.AvailabilityWindows.Count > 100)
        {
            return Result.Failure<PortfolioMemberCapacityDto>("Capacity profile values are invalid.", 400, AiErrorCodes.InvalidRequest);
        }

        var organization = await _organizations.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization == null || !await IsOrganizationMemberAsync(organization, userId, ct))
        {
            return Result.NotFound<PortfolioMemberCapacityDto>();
        }

        var normalizedWindows = new List<MemberAvailabilityWindowDto>(dto.AvailabilityWindows.Count);
        foreach (var item in dto.AvailabilityWindows.OrderBy(item => item.StartsAt))
        {
            var kind = NormalizeAvailabilityKind(item.Kind);
            if (kind == null || item.EndsAt <= item.StartsAt || item.EndsAt - item.StartsAt > TimeSpan.FromDays(366) ||
                (kind == MemberAvailabilityWindow.ReducedCapacity && (!item.AvailableHours.HasValue || item.AvailableHours.Value < 0m)))
            {
                return Result.Failure<PortfolioMemberCapacityDto>("Availability window values are invalid.", 400, AiErrorCodes.InvalidRequest);
            }

            normalizedWindows.Add(item with { Kind = kind });
        }

        if (normalizedWindows.Zip(normalizedWindows.Skip(1)).Any(pair => pair.First.EndsAt > pair.Second.StartsAt))
        {
            return Result.Failure<PortfolioMemberCapacityDto>("Availability windows cannot overlap.", 400, AiErrorCodes.InvalidRequest);
        }

        var profile = await _profiles.GetQueryable()
            .Include(item => item.AvailabilityWindows)
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, ct);
        if (profile == null)
        {
            if (!string.IsNullOrWhiteSpace(dto.RowVersion))
            {
                return Result.Failure<PortfolioMemberCapacityDto>("Capacity profile no longer matches the submitted version.", 409, AiErrorCodes.DraftConcurrencyConflict);
            }

            profile = new OrganizationMemberCapacityProfile
            {
                OrganizationId = organizationId,
                UserId = userId
            };
            await _profiles.AddAsync(profile, ct);
        }
        else if (!MatchesRowVersion(profile.RowVersion, dto.RowVersion))
        {
            return Result.Failure<PortfolioMemberCapacityDto>("Capacity profile was modified by another request.", 409, AiErrorCodes.DraftConcurrencyConflict);
        }

        var previousWindows = profile.AvailabilityWindows.ToList();
        foreach (var existing in previousWindows)
        {
            await _availability.HardDeleteAsync(existing, ct);
        }

        profile.WeeklyCapacityHours = decimal.Round(dto.WeeklyCapacityHours, 2);
        profile.TimeZoneId = dto.TimeZoneId.Trim();
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var item in normalizedWindows)
        {
            await _availability.AddAsync(new MemberAvailabilityWindow
            {
                OrganizationMemberCapacityProfileId = profile.Id,
                StartsAt = item.StartsAt,
                EndsAt = item.EndsAt,
                Kind = item.Kind,
                AvailableHours = item.Kind == MemberAvailabilityWindow.ReducedCapacity
                    ? decimal.Round(item.AvailableHours!.Value, 2)
                    : null
            }, ct);
        }

        await _unitOfWork.SaveChangesWithAuditAsync(
            _audit,
            "UpdateMemberCapacityProfile",
            nameof(OrganizationMemberCapacityProfile),
            profile.Id.ToString(),
            new { organizationId, userId, profile.WeeklyCapacityHours, availabilityWindowCount = normalizedWindows.Count },
            ct);

        var scopeProject = await _projects.GetQueryable()
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var scope = new ManagedPortfolioScope(scopeProject?.Id ?? Guid.Empty, organizationId, organization.OwnerId);
        var snapshot = await BuildCapacityAsync(scope, DateTimeOffset.UtcNow.Date, DateTimeOffset.UtcNow.Date.AddDays(14), ct);
        return Result.Success(snapshot.Members.First(item => item.UserId == userId));
    }

    public async Task<Result<PortfolioScheduleProposalDto>> CreateProposalAsync(
        Guid projectId,
        CreatePortfolioScheduleProposalDto dto,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var scope = await ResolveManagedPortfolioAsync(projectId, ct);
        if (!scope.IsSuccess)
        {
            return Result.Failure<PortfolioScheduleProposalDto>(scope.Error!, scope.StatusCode, scope.ErrorCode);
        }

        var normalizedKey = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return Result.Failure<PortfolioScheduleProposalDto>("Idempotency-Key is required.", 400, AiErrorCodes.InvalidRequest);
        }

        var window = NormalizeWindow(dto.WindowStart, dto.WindowEnd);
        if (!window.IsSuccess)
        {
            return Result.Failure<PortfolioScheduleProposalDto>(window.Error!, window.StatusCode, window.ErrorCode);
        }

        var taskIds = dto.TaskIds.Where(item => item != Guid.Empty).Distinct().ToList();
        if (taskIds.Count is < 1 or > 50)
        {
            return Result.Failure<PortfolioScheduleProposalDto>("Select between 1 and 50 tasks.", 400, AiErrorCodes.InvalidRequest);
        }

        var currentUserId = _currentUser.UserId!.Value;
        var requestJson = JsonSerializer.Serialize(new
        {
            projectId,
            taskIds = taskIds.OrderBy(id => id).ToArray(),
            window.Data.Start,
            window.Data.End,
            requestedAssignee = dto.RequestedAssignee == null ? null : AiPromptLanguage.Normalize(dto.RequestedAssignee)
        }, JsonOptions);
        var requestHash = Hash(requestJson);
        var existing = await _jobs.GetQueryable()
            .Include(item => item.Drafts)
            .FirstOrDefaultAsync(item => item.RequestedById == currentUserId && item.IdempotencyKey == normalizedKey, ct);
        if (existing != null)
        {
            if (existing.ProjectId != projectId || existing.JobType != "portfolio_schedule_proposal" || existing.Drafts.Count == 0 ||
                existing.RequestHash != requestHash)
            {
                return Result.Failure<PortfolioScheduleProposalDto>("The idempotency key was used for another request.", 409, AiErrorCodes.IdempotencyConflict);
            }

            return await GetProposalAsync(projectId, existing.Drafts.First().Id, ct);
        }

        var selectedTasks = await _taskAccessPolicy.ApplyVisibilityFilter(_tasks.GetQueryable())
            .Include(item => item.Project)
            .Include(item => item.SkillRequirements)
                .ThenInclude(item => item.OrganizationSkill)
            .Include(item => item.PredecessorDependencies)
                .ThenInclude(item => item.Predecessor)
            .Where(item => item.ProjectId == projectId && taskIds.Contains(item.Id))
            .ToListAsync(ct);
        if (selectedTasks.Count != taskIds.Count || selectedTasks.Any(IsClosed))
        {
            return Result.Failure<PortfolioScheduleProposalDto>("One or more selected tasks are absent, closed, or outside the authorized scope.", 404, AiErrorCodes.PermissionDenied);
        }

        foreach (var selectedTask in selectedTasks)
        {
            if (!await _taskAccessPolicy.CanManageProjectAsync(selectedTask.ProjectId, selectedTask.Project.OwnerId, ct))
            {
                return Result.NotFound<PortfolioScheduleProposalDto>();
            }
        }

        var capacity = await BuildCapacityAsync(scope.Data!, window.Data.Start, window.Data.End, ct);
        var capacityByMember = capacity.Members.ToDictionary(item => item.UserId);
        var projectMembers = await _projectMembers.GetQueryable()
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(ct);
        var candidateUsers = projectMembers
            .Select(item => new CandidateUser(item.UserId, item.User.FullName))
            .Append(new CandidateUser(selectedTasks[0].Project.OwnerId,
                await _users.GetQueryable().Where(item => item.Id == selectedTasks[0].Project.OwnerId).Select(item => item.FullName).FirstAsync(ct)))
            .DistinctBy(item => item.UserId)
            .Where(item => capacityByMember.ContainsKey(item.UserId))
            .ToList();
        if (candidateUsers.Count == 0)
        {
            return Result.Failure<PortfolioScheduleProposalDto>("No eligible project member has an organization capacity scope.", 409, AiErrorCodes.InvalidRequest);
        }
        var requestedAssignee = AiTaskPrompt.ResolveAssignee(dto.RequestedAssignee,
            candidateUsers.Select(candidate => (candidate.UserId, candidate.FullName)));
        if (requestedAssignee.Error != null)
            return Result.Failure<PortfolioScheduleProposalDto>(requestedAssignee.Error, 422, AiErrorCodes.InvalidRequest);

        var organizationProjectIds = await _projects.GetQueryable()
            .AsNoTracking()
            .Where(item => item.OrganizationId == scope.Data!.OrganizationId && !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var evidenceTasks = await _tasks.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Project)
            .Include(item => item.SkillRequirements)
                .ThenInclude(item => item.OrganizationSkill)
            .Include(item => item.CompletionAttributions)
            .Where(item => organizationProjectIds.Contains(item.ProjectId) && item.Status == "Done")
            .ToListAsync(ct);
        var visibleEvidenceIds = await _taskAccessPolicy.ApplyVisibilityFilter(_tasks.GetQueryable())
            .AsNoTracking()
            .Where(item => organizationProjectIds.Contains(item.ProjectId) && item.Status == "Done")
            .Select(item => item.Id)
            .ToHashSetAsync(ct);

        var proposalSources = new Dictionary<string, PortfolioScheduleSourceDto>(StringComparer.Ordinal);
        var items = new List<PortfolioScheduleProposalItemDto>(selectedTasks.Count);
        foreach (var task in selectedTasks.OrderByDescending(item => PriorityRank(item.Priority)).ThenBy(item => item.DueDate).ThenBy(item => item.Id))
        {
            var requiredSkills = task.SkillRequirements
                .Where(item => item.OrganizationSkill.IsActive)
                .Select(item => item.OrganizationSkillId)
                .Distinct()
                .ToHashSet();
            var ranked = candidateUsers.Select(candidate =>
            {
                var memberEvidence = evidenceTasks.Where(evidence => evidence.CompletionAttributions.Any(attribution =>
                    attribution.ContributorUserId == candidate.UserId && attribution.Status == TaskCompletionAttribution.Confirmed)).ToList();
                var matchedEvidence = memberEvidence
                    .SelectMany(evidence => evidence.SkillRequirements.Select(requirement => new { Evidence = evidence, Requirement = requirement }))
                    .Where(item => requiredSkills.Contains(item.Requirement.OrganizationSkillId))
                    .ToList();
                var matchedSkills = matchedEvidence.Select(item => item.Requirement.OrganizationSkillId).Distinct().Count();
                var coverage = requiredSkills.Count == 0 ? 0 : (int)Math.Round(matchedSkills * 100m / requiredSkills.Count);
                var evidenceCount = matchedEvidence.Select(item => item.Evidence.Id).Distinct().Count();
                var maxLevel = matchedEvidence.Count == 0 ? 0 : matchedEvidence.Max(item => SkillLevelRank(item.Requirement.RequiredLevel));
                var confidence = matchedEvidence.Count == 0 ? 0m : MemberSkillEvidenceService.CalculateConfidence(evidenceCount, maxLevel);
                var memberCapacity = capacityByMember[candidate.UserId];
                return new RankedCandidate(
                    candidate,
                    memberCapacity,
                    coverage,
                    confidence,
                    matchedEvidence.Select(item => item.Evidence).DistinctBy(item => item.Id).ToList());
            })
            .OrderByDescending(item => requiredSkills.Count > 0 && item.Coverage > 0)
            .ThenByDescending(item => item.Coverage)
            .ThenByDescending(item => item.Confidence)
            .ThenByDescending(item => item.Capacity.RemainingHours)
            .ThenBy(item => item.Candidate.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

            var estimate = task.EstimatedHours ?? MissingEstimateFallbackHours;
            var predecessorDates = task.PredecessorDependencies
                .Where(item => !IsClosed(item.Predecessor))
                .Select(item => item.Predecessor.DueDate)
                .ToList();
            var start = predecessorDates.Where(item => item.HasValue).Select(item => item!.Value).Append(window.Data.Start).Max();
            if (start < window.Data.Start) start = window.Data.Start;
            var candidatePlans = ranked.Select((candidate, rank) =>
            {
                var dailyHours = Math.Max(1m, candidate.Capacity.WeeklyCapacityHours / 5m);
                var workDays = Math.Max(1, (int)Math.Ceiling(estimate / dailyHours));
                var candidateDue = AddBusinessDays(start, workDays - 1);
                var additionalHours = IsAssignedTo(task, candidate.Candidate.UserId) ? 0m : estimate;
                var blockers = new List<string>();
                if (candidate.Capacity.CapacityState == "missing_declared_capacity")
                    blockers.Add("Chưa có capacity được khai báo; lịch trống không được coi là năng lực");
                if (additionalHours > candidate.Capacity.RemainingHours)
                    blockers.Add($"Không đủ capacity: còn {candidate.Capacity.RemainingHours:0.#}h, cần thêm {additionalHours:0.#}h");
                if (candidate.Capacity.AvailabilityWindows.Any(availability =>
                        string.Equals(availability.Kind, MemberAvailabilityWindow.Unavailable, StringComparison.OrdinalIgnoreCase) &&
                        availability.EndsAt > start && availability.StartsAt < candidateDue))
                    blockers.Add("Có lịch không sẵn sàng trùng khoảng thực hiện đề xuất");
                if (requiredSkills.Count > 0 && candidate.Coverage == 0)
                    blockers.Add("Chưa có evidence đã xác nhận cho required skill của Task");
                if (candidateDue > window.Data.End)
                    blockers.Add("Không thể xếp trọn Task trong cửa sổ hiện tại");
                return new CandidateSchedulePlan(candidate, rank, candidateDue, additionalHours, blockers);
            })
            .OrderBy(item => requestedAssignee.Id.HasValue && item.Candidate.Candidate.UserId != requestedAssignee.Id.Value)
            .ThenBy(item => item.BlockingReasons.Count > 0)
            .ThenBy(item => item.Rank)
            .ToList();
            var winnerPlan = candidatePlans.First();
            var winner = winnerPlan.Candidate;
            var due = winnerPlan.Due;
            var dependencyConflicts = task.PredecessorDependencies
                .Where(item => !IsClosed(item.Predecessor) && !item.Predecessor.DueDate.HasValue)
                .Select(item => $"Task phụ thuộc {item.Predecessor.Title} chưa có hạn hoàn thành")
                .ToList();
            var risks = new List<string>();
            if (!task.EstimatedHours.HasValue) risks.Add($"Thiếu estimate; dùng fallback {MissingEstimateFallbackHours}h");
            if (winner.Capacity.CapacityState == "missing_declared_capacity") risks.Add("Chưa có capacity được khai báo; phương án đang bị chặn");
            if (winner.Capacity.AssignedHours + winnerPlan.AdditionalHours > winner.Capacity.WindowCapacityHours) risks.Add("Đề xuất làm vượt capacity trong cửa sổ");
            if (task.DueDate.HasValue && due > task.DueDate.Value) risks.Add("Lịch khả thi muộn hơn deadline hiện tại");
            if (due > window.Data.End) risks.Add("Không thể xếp trọn trong cửa sổ đã chọn");

            var sourceKeys = new List<string>();
            var taskSourceKey = $"task:{task.Id:D}";
            sourceKeys.Add(taskSourceKey);
            proposalSources[taskSourceKey] = new PortfolioScheduleSourceDto(
                taskSourceKey, "task", task.Id, task.Title, $"/projects/{task.ProjectId}/tasks/{task.Id}", false);
            foreach (var sourceTask in winner.MatchedEvidenceTasks.Take(8))
            {
                if (visibleEvidenceIds.Contains(sourceTask.Id))
                {
                    var key = $"task:{sourceTask.Id:D}";
                    sourceKeys.Add(key);
                    proposalSources[key] = new PortfolioScheduleSourceDto(
                        key, "task", sourceTask.Id, sourceTask.Title,
                        $"/projects/{sourceTask.ProjectId}/tasks/{sourceTask.Id}", false);
                }
                else
                {
                    var key = $"restricted-evidence:{winner.Candidate.UserId:D}";
                    sourceKeys.Add(key);
                    proposalSources[key] = new PortfolioScheduleSourceDto(
                        key, "skill_evidence_aggregate", null, "Bằng chứng kỹ năng hạn chế", null, true);
                }
            }
            var capacityKey = $"capacity:{winner.Candidate.UserId:D}";
            sourceKeys.Add(capacityKey);
            proposalSources[capacityKey] = new PortfolioScheduleSourceDto(
                capacityKey, "member_capacity", winner.Candidate.UserId,
                $"Capacity của {winner.Candidate.FullName}", null, winner.Capacity.HasRestrictedLoad);

            items.Add(new PortfolioScheduleProposalItemDto(
                Guid.NewGuid(),
                task.Id,
                task.Title,
                task.ProjectId,
                task.Project.Name,
                task.AssigneeId,
                task.AssigneeId.HasValue ? candidateUsers.FirstOrDefault(item => item.UserId == task.AssigneeId.Value)?.FullName : null,
                winner.Candidate.UserId,
                winner.Candidate.FullName,
                start,
                due,
                winner.Coverage,
                winner.Confidence,
                winner.Capacity.AssignedHours,
                winner.Capacity.AssignedHours + winnerPlan.AdditionalHours,
                winner.Capacity.WindowCapacityHours,
                dependencyConflicts,
                risks,
                candidatePlans.Skip(1).Take(3).Select(item => new PortfolioScheduleAlternativeDto(
                    item.Candidate.Candidate.UserId,
                    item.Candidate.Candidate.FullName,
                    item.Candidate.Coverage,
                    item.Candidate.Capacity.RemainingHours,
                    BuildTradeOff(item.Candidate, requiredSkills.Count),
                    item.Candidate.Confidence,
                    item.Candidate.Capacity.AssignedHours,
                    item.Candidate.Capacity.WindowCapacityHours,
                    item.BlockingReasons)).ToList(),
                sourceKeys.Distinct(StringComparer.Ordinal).ToList(),
                EncodeRowVersion(task.RowVersion),
                winnerPlan.BlockingReasons.Count == 0,
                winnerPlan.BlockingReasons));
            if (winnerPlan.BlockingReasons.Count == 0)
            {
                // Reserve proposed work within this batch so the next task cannot reuse
                // the same remaining hours as if the preceding assignment did not exist.
                capacityByMember[winner.Candidate.UserId] = winner.Capacity with
                {
                    AssignedHours = winner.Capacity.AssignedHours + winnerPlan.AdditionalHours,
                    RemainingHours = winner.Capacity.RemainingHours - winnerPlan.AdditionalHours
                };
            }
        }

        var warnings = new List<string>();
        if (requestedAssignee.Id.HasValue)
            warnings.Add("Đã giữ người được giao theo yêu cầu. Nếu người này không đủ điều kiện, card bị chặn và giữ các ứng viên thay thế để bạn chọn; Qaly không tự đổi người.");
        if (capacity.HasRestrictedLoad) warnings.Add("Một phần tải công việc riêng tư chỉ được dùng dưới dạng tổng hợp, không hiển thị nội dung task.");
        if (items.Any(item => item.DeadlineRisks.Count > 0)) warnings.Add("Có đề xuất cần người quản lý xử lý cảnh báo trước khi xác nhận.");
        if (items.Any(item => item.BlockingReasons?.Count > 0)) warnings.Add("Không có phương án tự giao an toàn cho một hoặc nhiều Task; hãy chọn phương án thay thế hoặc cập nhật capacity/availability.");
        var payload = new StoredProposalPayload(
            SchemaId,
            ScoringVersion,
            scope.Data!.OrganizationId,
            window.Data.Start,
            window.Data.End,
            items,
            proposalSources.Values.OrderBy(item => item.Key).ToList(),
            warnings,
            DateTimeOffset.UtcNow);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var resultHash = Hash(payloadJson);
        var now = DateTimeOffset.UtcNow;
        var job = new AiJob
        {
            TenantId = scope.Data.OrganizationId,
            ProjectId = projectId,
            JobType = "portfolio_schedule_proposal",
            SourceType = "task",
            SourceId = selectedTasks[0].Id.ToString("D"),
            SchemaId = SchemaId,
            SchemaVersion = "1.0",
            RequestJson = requestJson,
            RequestHash = requestHash,
            IdempotencyKey = normalizedKey,
            ProviderHint = "local",
            Sensitive = selectedTasks.Any(item => item.IsPrivate) || capacity.HasRestrictedLoad,
            CloudEligible = false,
            PolicyCheckedAt = now,
            PolicyDecisionJson = JsonSerializer.Serialize(new { deterministic = true, privateAggregateRedacted = capacity.HasRestrictedLoad }),
            Status = AiJobStatuses.Succeeded,
            ProgressPercent = 100,
            AvailableAt = now,
            StartedAt = now,
            FinishedAt = now,
            AttemptCount = 1,
            MaxAttempts = 1,
            ResultJson = payloadJson,
            ResultHash = resultHash,
            SelectedProvider = "LocalRules",
            SelectedModel = ScoringVersion,
            EstimatedCostUsd = 0m,
            ActualCostUsd = 0m,
            PricingVersion = "deterministic-v1",
            CacheKey = requestHash,
            CacheHit = false,
            RequestedById = currentUserId
        };
        await _jobs.AddAsync(job, ct);
        var sourceIndex = 0;
        foreach (var source in proposalSources.Values.Where(item => item.EntityId.HasValue))
        {
            await _sources.AddAsync(new AiJobSource
            {
                AiJobId = job.Id,
                SourceType = source.Type,
                SourceEntityId = source.EntityId,
                SourceVersion = source.Type == "task"
                    ? items.FirstOrDefault(item => item.TaskId == source.EntityId)?.TaskRowVersion
                    : null,
                SourceHash = Hash(source.Key),
                SortOrder = sourceIndex++
            }, ct);
        }
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = projectId,
            DraftType = DraftType,
            PayloadJson = payloadJson,
            OriginalPayloadJson = payloadJson,
            WorkingPayloadJson = payloadJson,
            Status = AiDraftStatuses.PendingReview,
            SchemaId = SchemaId,
            Confidence = items.Count == 0 ? 0m : decimal.Round(items.Average(item => item.EvidenceConfidence), 4),
            SourceHashAtGeneration = requestHash,
            ExpiresAt = now.AddDays(7)
        };
        await _drafts.AddAsync(draft, ct);
        await _usage.AddAsync(new AiUsageLedger
        {
            TenantId = scope.Data.OrganizationId,
            ProjectId = projectId,
            UserId = currentUserId,
            AiJobId = job.Id,
            JobType = job.JobType,
            ProviderName = "LocalRules",
            ModelName = ScoringVersion,
            InputTokens = 0,
            OutputTokens = 0,
            EstimatedCostUsd = 0m,
            ActualCostUsd = 0m,
            PricingVersion = "deterministic-v1",
            LatencyMs = 0,
            Status = "success",
            CacheHit = false,
            PromptHash = requestHash,
            ResponseHash = resultHash
        }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _audit,
            "CreatePortfolioScheduleProposal",
            nameof(AiGeneratedDraft),
            draft.Id.ToString(),
            new { projectId, taskCount = items.Count, schemaId = SchemaId },
            ct);
        return await GetProposalAsync(projectId, draft.Id, ct);
    }

    public async Task<Result<PortfolioScheduleProposalDto>> GetProposalAsync(Guid projectId, Guid draftId, CancellationToken ct = default)
    {
        var draft = await LoadProposalAsync(projectId, draftId, tracking: false, ct);
        if (draft == null) return Result.NotFound<PortfolioScheduleProposalDto>();
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<PortfolioScheduleProposalDto>> GetLatestProposalForTaskAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) return Result.NotFound<PortfolioScheduleProposalDto>();
        var draftId = await _drafts.GetQueryable()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId &&
                item.DraftType == DraftType &&
                item.AiJob.RequestedById == _currentUser.UserId.Value &&
                item.AiJob.SourceId == taskId.ToString("D"))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(ct);
        return draftId.HasValue
            ? await GetProposalAsync(projectId, draftId.Value, ct)
            : Result.NotFound<PortfolioScheduleProposalDto>();
    }

    public async Task<Result<PortfolioScheduleProposalDto>> UpdateProposalAsync(
        Guid projectId,
        Guid draftId,
        UpdatePortfolioScheduleProposalDto dto,
        CancellationToken ct = default)
    {
        var draft = await LoadProposalAsync(projectId, draftId, tracking: true, ct);
        if (draft == null) return Result.NotFound<PortfolioScheduleProposalDto>();
        if (!MatchesRowVersion(draft.RowVersion, dto.RowVersion))
            return Result.Failure<PortfolioScheduleProposalDto>("The proposal was modified by another request.", 409, AiErrorCodes.DraftConcurrencyConflict);
        if (draft.Status != AiDraftStatuses.PendingReview)
            return Result.Failure<PortfolioScheduleProposalDto>("The proposal is no longer editable.", 409, AiErrorCodes.DraftAlreadyConfirmed);

        var payload = DeserializePayload(draft.WorkingPayloadJson);
        var validation = await ValidateEditedItemsAsync(payload, dto.Items, ct);
        if (!validation.IsSuccess)
            return Result.Failure<PortfolioScheduleProposalDto>(validation.Error!, validation.StatusCode, validation.ErrorCode);

        payload = payload with { Items = dto.Items.ToList() };
        draft.WorkingPayloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        draft.PayloadJson = draft.WorkingPayloadJson;
        await _unitOfWork.SaveChangesWithAuditAsync(
            _audit,
            "UpdatePortfolioScheduleProposal",
            nameof(AiGeneratedDraft),
            draft.Id.ToString(),
            new { projectId, itemCount = dto.Items.Count },
            ct);
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<PortfolioScheduleProposalDto>> ConfirmProposalAsync(
        Guid projectId,
        Guid draftId,
        ConfirmPortfolioScheduleProposalDto dto,
        CancellationToken ct = default)
    {
        var draft = await LoadProposalAsync(projectId, draftId, tracking: true, ct);
        if (draft == null) return Result.NotFound<PortfolioScheduleProposalDto>();
        if (!dto.Confirmed || string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            return Result.Failure<PortfolioScheduleProposalDto>("Explicit confirmation and Idempotency-Key are required.", 400, AiErrorCodes.InvalidRequest);

        if (draft.Status == AiDraftStatuses.Confirmed &&
            draft.ConfirmationIdempotencyKey == dto.IdempotencyKey &&
            !string.IsNullOrWhiteSpace(draft.ConfirmationResultJson))
        {
            return Result.Success(ToDto(draft));
        }
        if (draft.Status != AiDraftStatuses.PendingReview)
            return Result.Failure<PortfolioScheduleProposalDto>("The proposal is no longer confirmable.", 409, AiErrorCodes.DraftAlreadyConfirmed);
        if (!MatchesRowVersion(draft.RowVersion, dto.RowVersion))
            return Result.Failure<PortfolioScheduleProposalDto>("The proposal was modified by another request.", 409, AiErrorCodes.DraftConcurrencyConflict);

        var payload = DeserializePayload(draft.WorkingPayloadJson);
        var selectedIds = dto.SelectedItemIds.Distinct().ToHashSet();
        var selected = payload.Items.Where(item => selectedIds.Contains(item.ItemId)).ToList();
        if (selected.Count == 0 || selected.Count != selectedIds.Count)
            return Result.Failure<PortfolioScheduleProposalDto>("Select at least one valid proposal item.", 400, AiErrorCodes.InvalidRequest);
        if (selected.Any(item => item.BlockingReasons?.Count > 0))
            return Result.Failure<PortfolioScheduleProposalDto>(
                "The reviewed proposal still has capacity, availability, skill-evidence, or planning-window blockers. Choose a safe alternative or regenerate it.",
                409, AiErrorCodes.SourceStale);

        var tasks = await _tasks.GetQueryable()
            .Include(item => item.Project)
            .Include(item => item.Assignees)
            .Include(item => item.SkillRequirements).ThenInclude(item => item.OrganizationSkill)
            .Where(item => selected.Select(selection => selection.TaskId).Contains(item.Id))
            .ToListAsync(ct);
        if (tasks.Count != selected.Count)
            return Result.NotFound<PortfolioScheduleProposalDto>();

        foreach (var item in selected)
        {
            var task = tasks.First(candidate => candidate.Id == item.TaskId);
            if (!await _taskAccessPolicy.CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
                return Result.NotFound<PortfolioScheduleProposalDto>();
            if (!MatchesRowVersion(task.RowVersion, item.TaskRowVersion))
                return Result.Failure<PortfolioScheduleProposalDto>("A source task changed after the proposal was generated.", 409, AiErrorCodes.SourceStale);
            if (item.ProposedDue < item.ProposedStart)
                return Result.Failure<PortfolioScheduleProposalDto>("A proposed due date is earlier than its start date.", 422, AiErrorCodes.SchemaInvalid);
            var eligible = task.Project.OwnerId == item.ProposedAssigneeId || await _projectMembers.GetQueryable().AnyAsync(member =>
                member.ProjectId == task.ProjectId && member.UserId == item.ProposedAssigneeId, ct);
            if (!eligible)
                return Result.Failure<PortfolioScheduleProposalDto>("A proposed assignee is no longer a project member.", 409, AiErrorCodes.SourceStale);
        }

        var currentConstraints = await ValidateCurrentExecutionConstraintsAsync(payload, selected, tasks, ct);
        if (!currentConstraints.IsSuccess)
            return Result.Failure<PortfolioScheduleProposalDto>(currentConstraints.Error!, currentConstraints.StatusCode, currentConstraints.ErrorCode);

        foreach (var item in selected)
        {
            var task = tasks.First(candidate => candidate.Id == item.TaskId);
            foreach (var existing in task.Assignees.ToList())
                await _assignments.HardDeleteAsync(existing, ct);
            task.AssigneeId = item.ProposedAssigneeId;
            task.StartDate = item.ProposedStart;
            task.DueDate = item.ProposedDue;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            await _assignments.AddAsync(new TaskAssignment
            {
                TaskItemId = task.Id,
                UserId = item.ProposedAssigneeId,
                AssignedAt = DateTimeOffset.UtcNow,
                AssignedByUserId = _currentUser.UserId
            }, ct);
        }

        var now = DateTimeOffset.UtcNow;
        var receipt = new PortfolioScheduleReceiptDto(
            draft.Id,
            $"schedule:{Guid.NewGuid():N}",
            selected.Count,
            selected.Select(item => item.TaskId).ToList(),
            selected.Select(item => $"/projects/{item.CurrentProjectId}/tasks/{item.TaskId}").ToList(),
            now,
            AiActionReceiptStatuses.VerificationPending,
            false,
            []);
        draft.Status = AiDraftStatuses.Confirmed;
        draft.ConfirmedById = _currentUser.UserId;
        draft.ConfirmedAt = now;
        draft.ConfirmAction = "apply_schedule_proposal";
        draft.ConfirmationIdempotencyKey = dto.IdempotencyKey.Trim();
        draft.ConfirmationResultJson = JsonSerializer.Serialize(receipt, JsonOptions);
        await _unitOfWork.SaveChangesAsync(ct);

        var readBack = await _tasks.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Assignees)
            .Where(item => receipt.AppliedTaskIds.Contains(item.Id))
            .ToListAsync(ct);
        var verificationErrors = new List<string>();
        foreach (var item in selected)
        {
            var task = readBack.FirstOrDefault(candidate => candidate.Id == item.TaskId);
            if (task == null)
            {
                verificationErrors.Add($"Task {item.TaskId:D} không còn tồn tại sau khi ghi.");
                continue;
            }
            if (task.AssigneeId != item.ProposedAssigneeId ||
                task.StartDate != item.ProposedStart ||
                task.DueDate != item.ProposedDue ||
                task.Assignees.Count != 1 ||
                task.Assignees.Single().UserId != item.ProposedAssigneeId)
                verificationErrors.Add($"Task {item.TaskId:D} không khớp người phụ trách/lịch đã xác nhận.");
        }
        receipt = receipt with
        {
            Status = verificationErrors.Count == 0
                ? AiActionReceiptStatuses.Succeeded
                : AiActionReceiptStatuses.VerificationFailed,
            ReadBackVerified = verificationErrors.Count == 0,
            VerificationErrors = verificationErrors
        };
        draft.ConfirmationResultJson = JsonSerializer.Serialize(receipt, JsonOptions);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _audit,
            "ConfirmPortfolioScheduleProposal",
            nameof(AiGeneratedDraft),
            draft.Id.ToString(),
            new { projectId, receipt.AppliedCount, receipt.AppliedTaskIds },
            ct);
        if (!receipt.ReadBackVerified)
            return Result.Failure<PortfolioScheduleProposalDto>("Canonical task read-back did not match the confirmed assignment.", 500, "canonical_readback_failed");
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<PortfolioScheduleProposalDto>> RejectProposalAsync(
        Guid projectId,
        Guid draftId,
        RejectPortfolioScheduleProposalDto dto,
        CancellationToken ct = default)
    {
        var draft = await LoadProposalAsync(projectId, draftId, tracking: true, ct);
        if (draft == null) return Result.NotFound<PortfolioScheduleProposalDto>();
        if (string.IsNullOrWhiteSpace(dto.Reason) || string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            return Result.Failure<PortfolioScheduleProposalDto>("Reason and Idempotency-Key are required.", 400, AiErrorCodes.InvalidRequest);
        if (!MatchesRowVersion(draft.RowVersion, dto.RowVersion))
            return Result.Failure<PortfolioScheduleProposalDto>("The proposal was modified by another request.", 409, AiErrorCodes.DraftConcurrencyConflict);
        if (draft.Status != AiDraftStatuses.PendingReview)
            return Result.Failure<PortfolioScheduleProposalDto>("The proposal is no longer rejectable.", 409, AiErrorCodes.DraftAlreadyConfirmed);

        draft.Status = AiDraftStatuses.Rejected;
        draft.RejectedById = _currentUser.UserId;
        draft.RejectedAt = DateTimeOffset.UtcNow;
        draft.RejectionReason = dto.Reason.Trim();
        draft.ConfirmationIdempotencyKey = dto.IdempotencyKey.Trim();
        await _unitOfWork.SaveChangesWithAuditAsync(
            _audit,
            "RejectPortfolioScheduleProposal",
            nameof(AiGeneratedDraft),
            draft.Id.ToString(),
            new { projectId, reason = dto.Reason.Trim() },
            ct);
        return Result.Success(ToDto(draft));
    }

    private async Task<Result<ManagedPortfolioScope>> ResolveManagedPortfolioAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _projects.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted, ct);
        if (project?.OrganizationId == null || project.Organization == null || !project.Organization.IsActive)
            return Result.NotFound<ManagedPortfolioScope>();
        if (!await _taskAccessPolicy.CanManageProjectAsync(project.Id, project.OwnerId, ct) ||
            !_currentUser.UserId.HasValue ||
            !await CanManageOrganizationAsync(project.OrganizationId.Value, _currentUser.UserId.Value, ct))
            return Result.NotFound<ManagedPortfolioScope>();
        return Result.Success(new ManagedPortfolioScope(project.Id, project.OrganizationId.Value, project.Organization.OwnerId));
    }

    private async Task<CapacitySnapshot> BuildCapacityAsync(
        ManagedPortfolioScope scope,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct)
    {
        var organization = await _organizations.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Owner)
            .FirstAsync(item => item.Id == scope.OrganizationId, ct);
        var memberships = await _organizationMembers.GetQueryable()
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.OrganizationId == scope.OrganizationId && item.User.IsActive)
            .ToListAsync(ct);
        var people = memberships.Select(item => new CandidateUser(item.UserId, item.User.FullName, item.User.AvatarUrl))
            .Concat(organization.Owner.IsActive
                ? [new CandidateUser(organization.OwnerId, organization.Owner.FullName, organization.Owner.AvatarUrl)]
                : [])
            .DistinctBy(item => item.UserId)
            .ToList();
        var profiles = await _profiles.GetQueryable()
            .AsNoTracking()
            .Include(item => item.AvailabilityWindows)
            .Where(item => item.OrganizationId == scope.OrganizationId)
            .ToListAsync(ct);
        var projects = await _projects.GetQueryable()
            .AsNoTracking()
            .Where(item => item.OrganizationId == scope.OrganizationId && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, ct);
        var projectIds = projects.Keys.ToList();
        var allTasks = await _tasks.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Assignees)
            .Where(item => projectIds.Contains(item.ProjectId) &&
                           item.Status != "Done" &&
                           item.Status != "Completed" &&
                           item.Status != "Cancelled" &&
                           item.Status != "Canceled")
            .ToListAsync(ct);
        var visibleIds = await _taskAccessPolicy.ApplyVisibilityFilter(_tasks.GetQueryable())
            .AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) &&
                           item.Status != "Done" &&
                           item.Status != "Completed" &&
                           item.Status != "Cancelled" &&
                           item.Status != "Canceled")
            .Select(item => item.Id)
            .ToHashSetAsync(ct);

        var result = new List<PortfolioMemberCapacityDto>(people.Count);
        foreach (var person in people.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            var profile = profiles.FirstOrDefault(item => item.UserId == person.UserId);
            // Missing declaration is unknown capacity, never implicit availability.
            var weekly = profile?.WeeklyCapacityHours ?? 0m;
            var baseline = decimal.Round(CountWeekdays(start, end) * (weekly / 5m), 2);
            var windows = profile?.AvailabilityWindows
                .Where(item => item.EndsAt > start && item.StartsAt < end)
                .OrderBy(item => item.StartsAt)
                .ToList() ?? [];
            var reduction = windows.Sum(item => CalculateAvailabilityReduction(item, weekly, start, end));
            var capacity = Math.Max(0m, decimal.Round(baseline - reduction, 2));
            var assigned = allTasks.Where(item => IsAssignedTo(item, person.UserId) && IntersectsPlanningWindow(item, start, end)).ToList();
            var assignedHours = assigned.Sum(item => (decimal)(item.EstimatedHours ?? MissingEstimateFallbackHours));
            var restricted = assigned.Any(item => !visibleIds.Contains(item.Id));
            var collisionCount = assigned.Where(item => item.DueDate.HasValue)
                .GroupBy(item => item.DueDate!.Value.Date)
                .Where(group => group.Count() > 1)
                .Sum(group => group.Count());
            var projectLoads = assigned.GroupBy(item => item.ProjectId).Select(group => new PortfolioProjectLoadDto(
                group.Key,
                projects.TryGetValue(group.Key, out var project) ? project.Name : "Project",
                group.Sum(item => (decimal)(item.EstimatedHours ?? MissingEstimateFallbackHours)),
                group.Count(),
                group.Any(item => !visibleIds.Contains(item.Id)))).OrderByDescending(item => item.AssignedHours).ToList();
            var remaining = decimal.Round(capacity - assignedHours, 2);
            result.Add(new PortfolioMemberCapacityDto(
                person.UserId,
                person.FullName,
                person.AvatarUrl,
                weekly,
                profile == null ? "missing_declared_capacity" : windows.Count > 0 ? "declared_with_availability" : "declared",
                capacity,
                assignedHours,
                remaining,
                capacity <= 0m ? (assignedHours > 0m ? 100 : 0) : (int)Math.Round(Math.Min(999m, assignedHours * 100m / capacity)),
                assigned.Count,
                assigned.Count(item => !item.EstimatedHours.HasValue),
                collisionCount,
                restricted,
                projectLoads,
                windows.Select(item => new MemberAvailabilityWindowDto(
                    item.Id, item.StartsAt, item.EndsAt, item.Kind, item.AvailableHours, EncodeRowVersion(item.RowVersion))).ToList(),
                profile == null ? null : EncodeRowVersion(profile.RowVersion)));
        }

        return new CapacitySnapshot(result, result.Any(item => item.HasRestrictedLoad));
    }

    private async Task<AiGeneratedDraft?> LoadProposalAsync(Guid projectId, Guid draftId, bool tracking, CancellationToken ct)
    {
        var query = _drafts.GetQueryable()
            .Include(item => item.Project)
                .ThenInclude(item => item.Organization)
            .Include(item => item.AiJob)
            .Where(item => item.Id == draftId && item.ProjectId == projectId && item.DraftType == DraftType);
        if (!tracking) query = query.AsNoTracking();
        var draft = await query.FirstOrDefaultAsync(ct);
        if (draft?.Project.OrganizationId == null || !_currentUser.UserId.HasValue ||
            !await CanManageOrganizationAsync(draft.Project.OrganizationId.Value, _currentUser.UserId.Value, ct))
            return null;
        return draft;
    }

    private async Task<Result> ValidateEditedItemsAsync(
        StoredProposalPayload original,
        IReadOnlyList<PortfolioScheduleProposalItemDto> edited,
        CancellationToken ct)
    {
        if (edited.Count != original.Items.Count || edited.Select(item => item.ItemId).Distinct().Count() != edited.Count ||
            !edited.Select(item => item.ItemId).ToHashSet().SetEquals(original.Items.Select(item => item.ItemId)))
            return Result.Failure("Proposal items cannot be added or removed during edit.", 422, AiErrorCodes.SchemaInvalid);
        foreach (var item in edited)
        {
            var source = original.Items.First(originalItem => originalItem.ItemId == item.ItemId);
            if (item.TaskId != source.TaskId || item.CurrentProjectId != source.CurrentProjectId || item.TaskRowVersion != source.TaskRowVersion ||
                item.ProposedDue < item.ProposedStart)
                return Result.Failure("Immutable task/source fields or dates are invalid.", 422, AiErrorCodes.SchemaInvalid);
            var reviewedCandidateIds = source.Alternatives.Select(candidate => candidate.UserId)
                .Append(source.ProposedAssigneeId)
                .ToHashSet();
            if (!reviewedCandidateIds.Contains(item.ProposedAssigneeId))
                return Result.Failure("Choose a reviewed candidate or generate a new proposal with current evidence.", 422, AiErrorCodes.SchemaInvalid);
            var eligible = await _projectMembers.GetQueryable().AnyAsync(member =>
                member.ProjectId == item.CurrentProjectId && member.UserId == item.ProposedAssigneeId, ct) ||
                await _projects.GetQueryable().AnyAsync(project => project.Id == item.CurrentProjectId && project.OwnerId == item.ProposedAssigneeId, ct);
            if (!eligible) return Result.Failure("Proposed assignee is not a project member.", 422, AiErrorCodes.SchemaInvalid);
            var reviewedBlockingReasons = item.ProposedAssigneeId == source.ProposedAssigneeId
                ? source.BlockingReasons ?? []
                : source.Alternatives.Single(candidate => candidate.UserId == item.ProposedAssigneeId).BlockingReasons ?? [];
            if (!(item.BlockingReasons ?? []).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(reviewedBlockingReasons.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
                return Result.Failure("Candidate blocking reasons are server-owned; regenerate the proposal after constraints change.", 422, AiErrorCodes.SchemaInvalid);
        }
        return Result.Success();
    }

    private async Task<Result> ValidateCurrentExecutionConstraintsAsync(
        StoredProposalPayload payload,
        List<PortfolioScheduleProposalItemDto> selected,
        IReadOnlyList<TaskItem> tasks,
        CancellationToken ct)
    {
        var snapshot = await BuildCapacityAsync(
            new ManagedPortfolioScope(selected[0].CurrentProjectId, payload.OrganizationId, Guid.Empty),
            payload.WindowStart,
            payload.WindowEnd,
            ct);
        var additionalByMember = new Dictionary<Guid, decimal>();
        foreach (var item in selected)
        {
            if (item.ProposedStart < payload.WindowStart || item.ProposedDue > payload.WindowEnd)
                return Result.Failure("Lịch đã sửa nằm ngoài cửa sổ kế hoạch; hãy lập lại phương án.", 409, AiErrorCodes.SourceStale);
            var member = snapshot.Members.FirstOrDefault(candidate => candidate.UserId == item.ProposedAssigneeId);
            if (member == null)
                return Result.Failure("Người được chọn không còn thuộc Organization.", 409, AiErrorCodes.SourceStale);
            if (member.CapacityState == "missing_declared_capacity")
                return Result.Failure($"{member.FullName} chưa khai báo capacity thật; lịch trống không được coi là năng lực.", 409, AiErrorCodes.SourceStale);
            if (member.AvailabilityWindows.Any(window =>
                    string.Equals(window.Kind, MemberAvailabilityWindow.Unavailable, StringComparison.OrdinalIgnoreCase) &&
                    window.EndsAt > item.ProposedStart && window.StartsAt < item.ProposedDue))
                return Result.Failure($"{member.FullName} có lịch không sẵn sàng trùng phương án.", 409, AiErrorCodes.SourceStale);

            var task = tasks.First(candidate => candidate.Id == item.TaskId);
            var requiredSkillIds = task.SkillRequirements
                .Where(requirement => requirement.OrganizationSkill.IsActive)
                .Select(requirement => requirement.OrganizationSkillId)
                .Distinct()
                .ToArray();
            if (requiredSkillIds.Length > 0)
            {
                var matchedSkillCount = await _tasks.GetQueryable()
                    .AsNoTracking()
                    .Where(evidence => evidence.Status == "Done" &&
                        evidence.Project.OrganizationId == payload.OrganizationId &&
                        evidence.CompletionAttributions.Any(attribution =>
                            attribution.ContributorUserId == item.ProposedAssigneeId &&
                            attribution.Status == TaskCompletionAttribution.Confirmed))
                    .SelectMany(evidence => evidence.SkillRequirements)
                    .Where(requirement => requiredSkillIds.Contains(requirement.OrganizationSkillId))
                    .Select(requirement => requirement.OrganizationSkillId)
                    .Distinct()
                    .CountAsync(ct);
                if (matchedSkillCount == 0)
                    return Result.Failure(
                        $"{member.FullName} chưa có evidence đã xác nhận cho required skill của Task.",
                        409, AiErrorCodes.SourceStale);
            }
            var extra = IsAssignedTo(task, item.ProposedAssigneeId)
                ? 0m
                : task.EstimatedHours ?? MissingEstimateFallbackHours;
            additionalByMember[item.ProposedAssigneeId] = additionalByMember.GetValueOrDefault(item.ProposedAssigneeId) + extra;
        }

        foreach (var (userId, extra) in additionalByMember)
        {
            var member = snapshot.Members.First(candidate => candidate.UserId == userId);
            if (extra > member.RemainingHours)
                return Result.Failure($"{member.FullName} không còn đủ capacity ({member.RemainingHours:0.#}h còn lại, cần thêm {extra:0.#}h).", 409, AiErrorCodes.SourceStale);
        }
        return Result.Success();
    }

    private static PortfolioScheduleProposalDto ToDto(AiGeneratedDraft draft)
    {
        var payload = DeserializePayload(draft.WorkingPayloadJson);
        var receipt = string.IsNullOrWhiteSpace(draft.ConfirmationResultJson)
            ? null
            : JsonSerializer.Deserialize<PortfolioScheduleReceiptDto>(draft.ConfirmationResultJson, JsonOptions);
        return new PortfolioScheduleProposalDto(
            draft.Id,
            draft.AiJobId,
            draft.ProjectId,
            payload.OrganizationId,
            draft.Status,
            payload.SchemaId,
            payload.ScoringVersion,
            payload.WindowStart,
            payload.WindowEnd,
            payload.Items,
            payload.Sources,
            payload.Warnings,
            EncodeRowVersion(draft.RowVersion),
            draft.AiJob.SelectedProvider ?? "LocalRules",
            draft.AiJob.SelectedModel ?? ScoringVersion,
            payload.GeneratedAt,
            receipt);
    }

    private static StoredProposalPayload DeserializePayload(string json)
        => JsonSerializer.Deserialize<StoredProposalPayload>(json, JsonOptions)
            ?? throw new JsonException("Portfolio schedule proposal payload is invalid.");

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, Guid userId, CancellationToken ct)
    {
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role)) return true;
        var organization = await _organizations.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId && item.IsActive, ct);
        if (organization?.OwnerId == userId) return true;
        var role = await _organizationMembers.GetQueryable().AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.UserId == userId)
            .Select(item => item.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private async Task<bool> IsOrganizationMemberAsync(Organization organization, Guid userId, CancellationToken ct)
        => organization.OwnerId == userId || await _organizationMembers.GetQueryable().AnyAsync(item =>
            item.OrganizationId == organization.Id && item.UserId == userId, ct);

    private static Result<(DateTimeOffset Start, DateTimeOffset End)> NormalizeWindow(DateTimeOffset? from, DateTimeOffset? to)
    {
        var start = from ?? DateTimeOffset.UtcNow.Date;
        var end = to ?? start.AddDays(14);
        if (end <= start || end - start > TimeSpan.FromDays(90))
            return Result.Failure<(DateTimeOffset, DateTimeOffset)>("Planning window must be between 1 and 90 days.", 400, AiErrorCodes.InvalidRequest);
        return Result.Success((start, end));
    }

    private static decimal CalculateAvailabilityReduction(MemberAvailabilityWindow item, decimal weekly, DateTimeOffset start, DateTimeOffset end)
    {
        var overlapStart = item.StartsAt > start ? item.StartsAt : start;
        var overlapEnd = item.EndsAt < end ? item.EndsAt : end;
        var baseline = CountWeekdays(overlapStart, overlapEnd) * (weekly / 5m);
        return item.Kind == MemberAvailabilityWindow.Unavailable
            ? baseline
            : Math.Max(0m, baseline - (item.AvailableHours ?? 0m));
    }

    private static int CountWeekdays(DateTimeOffset start, DateTimeOffset end)
    {
        var count = 0;
        for (var day = start.Date; day < end.Date; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) count++;
        return count;
    }

    private static DateTimeOffset AddBusinessDays(DateTimeOffset start, int businessDays)
    {
        var result = start;
        var remaining = businessDays;
        while (remaining > 0)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) remaining--;
        }
        return result;
    }

    private static bool IntersectsPlanningWindow(TaskItem task, DateTimeOffset start, DateTimeOffset end)
    {
        if (!task.StartDate.HasValue && !task.DueDate.HasValue) return true;
        var taskStart = task.StartDate ?? task.DueDate ?? start;
        var taskEnd = task.DueDate ?? task.StartDate ?? end;
        return taskEnd >= start && taskStart <= end;
    }

    private static bool IsAssignedTo(TaskItem task, Guid userId)
        => task.AssigneeId == userId || task.Assignees.Any(item => item.UserId == userId);

    private static bool IsClosed(TaskItem task) => IsClosedStatus(task.Status);

    private static bool IsClosedStatus(string status)
        => status.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("Canceled", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeAvailabilityKind(string? kind)
        => kind?.Trim().ToLowerInvariant() switch
        {
            "unavailable" => MemberAvailabilityWindow.Unavailable,
            "reducedcapacity" or "reduced_capacity" => MemberAvailabilityWindow.ReducedCapacity,
            _ => null
        };

    private static int SkillLevelRank(string? level)
        => level?.Trim().ToLowerInvariant() switch
        {
            "advanced" => 3,
            "working" or "intermediate" => 2,
            "familiar" or "beginner" => 1,
            _ => 0
        };

    private static int PriorityRank(string? priority)
        => priority?.Trim().ToLowerInvariant() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0
        };

    private static string BuildTradeOff(RankedCandidate candidate, int requiredSkillCount)
    {
        var skill = requiredSkillCount == 0
            ? "task chưa có skill đã xác nhận"
            : $"phủ {candidate.Coverage}% skill";
        var load = candidate.Capacity.RemainingHours >= 0
            ? $"còn {candidate.Capacity.RemainingHours:0.#}h"
            : $"quá tải {Math.Abs(candidate.Capacity.RemainingHours):0.#}h";
        return $"{skill}, {load} trong cửa sổ";
    }

    private static bool MatchesRowVersion(byte[] current, string? encoded)
    {
        if (current.Length == 0) return string.IsNullOrWhiteSpace(encoded);
        if (string.IsNullOrWhiteSpace(encoded)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(encoded)); }
        catch (FormatException) { return false; }
    }

    private static string EncodeRowVersion(byte[] rowVersion)
        => rowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(rowVersion);

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record ManagedPortfolioScope(Guid ProjectId, Guid OrganizationId, Guid OrganizationOwnerId);
    private sealed record CandidateUser(Guid UserId, string FullName, string? AvatarUrl = null);
    private sealed record CapacitySnapshot(IReadOnlyList<PortfolioMemberCapacityDto> Members, bool HasRestrictedLoad);
    private sealed record RankedCandidate(
        CandidateUser Candidate,
        PortfolioMemberCapacityDto Capacity,
        int Coverage,
        decimal Confidence,
        IReadOnlyList<TaskItem> MatchedEvidenceTasks);
    private sealed record CandidateSchedulePlan(
        RankedCandidate Candidate,
        int Rank,
        DateTimeOffset Due,
        decimal AdditionalHours,
        IReadOnlyList<string> BlockingReasons);
    private sealed record StoredProposalPayload(
        string SchemaId,
        string ScoringVersion,
        Guid OrganizationId,
        DateTimeOffset WindowStart,
        DateTimeOffset WindowEnd,
        List<PortfolioScheduleProposalItemDto> Items,
        List<PortfolioScheduleSourceDto> Sources,
        List<string> Warnings,
        DateTimeOffset GeneratedAt);
}
