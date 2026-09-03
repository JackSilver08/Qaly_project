using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class ProjectLaunchOrchestratorService
{
    public async Task<Result<ProjectLaunchPlanDto>> UpdatePlanAsync(
        Guid planId,
        UpdateProjectLaunchPlanRequestDto request,
        CancellationToken ct = default)
    {
        var entity = await _db.ProjectLaunchPlanArtifacts
            .SingleOrDefaultAsync(item => item.Id == planId, ct);
        if (entity == null) return Result.NotFound<ProjectLaunchPlanDto>();
        var access = await AuthorizeOrganizationAsync(entity.OrganizationId, manage: true, ct);
        if (!access.IsSuccess)
            return Result.Failure<ProjectLaunchPlanDto>(access.Error!, access.StatusCode, access.ErrorCode);
        if (entity.RowRevision != request.ExpectedRevision)
            return Result.Failure<ProjectLaunchPlanDto>(
                "Phương án đã thay đổi ở một phiên khác. Hãy tải bản mới nhất trước khi lưu.", 409, "project_launch_plan_stale");
        if (await _db.ProjectLaunchExecutions.AnyAsync(item => item.ProjectLaunchPlanArtifactId == planId, ct))
            return Result.Failure<ProjectLaunchPlanDto>(
                "Project đã được tạo; không thể sửa bản kế hoạch đã thực thi.", 409, "project_launch_already_executed");

        var currentHash = await ComputeOrganizationSourceHashAsync(entity.OrganizationId, ct);
        if (!string.Equals(currentHash, entity.SourceVersionHash, StringComparison.Ordinal))
            return Result.Failure<ProjectLaunchPlanDto>(
                "Dữ liệu nhân sự hoặc lịch đã thay đổi. Hãy lập lại phương án trước khi lưu.", 409, "project_launch_source_stale");

        var scenarios = JsonSerializer.Deserialize<ProjectStaffingScenarioDto[]>(entity.StaffingScenariosJson, JsonOptions) ?? [];
        var delivery = JsonSerializer.Deserialize<ProjectLaunchDeliveryPlanDto>(entity.DeliveryPlanJson, JsonOptions);
        if (delivery == null)
            return Result.Failure<ProjectLaunchPlanDto>("Bản kế hoạch đã lưu không hợp lệ.", 422, "project_launch_plan_invalid");
        var assignmentMode = request.AssignmentMode ?? delivery.AssignmentMode;
        var scheduleMode = request.ScheduleMode ?? delivery.ScheduleMode;
        if (!ProjectLaunchAssignmentModes.IsValid(assignmentMode) || !ProjectLaunchScheduleModes.IsValid(scheduleMode))
            return Result.Failure<ProjectLaunchPlanDto>(
                "Chế độ phân công hoặc cấu trúc lịch không được hỗ trợ.", 422, "project_launch_review_mode_invalid");
        var explicitlyPinnedTaskIds = string.Equals(
                assignmentMode,
                ProjectLaunchAssignmentModes.AutoBalance,
                StringComparison.Ordinal)
            ? FindExplicitlyPinnedTaskIds(delivery.Sprints, request.Sprints)
            : new HashSet<string>(StringComparer.Ordinal);
        delivery = delivery with { AssignmentMode = assignmentMode, ScheduleMode = scheduleMode };
        var sprintError = ValidateSprints(delivery, request.Sprints);
        if (sprintError != null)
            return Result.Failure<ProjectLaunchPlanDto>(sprintError, 422, "project_launch_sprint_invalid");

        var baseScenario = scenarios.SingleOrDefault(item => item.ScenarioId == request.SelectedScenarioId);
        if (baseScenario == null)
            return Result.Failure<ProjectLaunchPlanDto>(
                "Phương án nhân sự đã chọn không tồn tại trong bản kế hoạch hiện tại.",
                422,
                "project_launch_scenario_invalid");

        var normalizedOverrides = new List<ProjectStaffingOverrideDto>();
        var overrideIds = new HashSet<Guid>();
        foreach (var item in request.Staffing.Where(item => item.Included))
        {
            if (!overrideIds.Add(item.UserId))
                return Result.Failure<ProjectLaunchPlanDto>(
                    "Mỗi thành viên chỉ được xuất hiện một lần trong đội hình.", 422, "project_launch_staffing_duplicate");
            var requestedRole = item.Manager ? ProjectRoleRules.Manager : item.ProposedRole;
            if (!ProjectRoleRules.TryNormalizeAssignableRole(requestedRole, out var normalizedRole) ||
                string.Equals(normalizedRole, ProjectRoleRules.Owner, StringComparison.Ordinal) ||
                (!item.Manager && string.Equals(normalizedRole, ProjectRoleRules.Manager, StringComparison.Ordinal)))
                return Result.Failure<ProjectLaunchPlanDto>(
                    "Vai trò trong đội hình không hợp lệ. Quyền Owner không được gán qua AI và Manager phải là người quản lý đã chọn.",
                    422,
                    "project_launch_role_invalid");
            normalizedOverrides.Add(item with { ProposedRole = normalizedRole });
        }
        if (request.Staffing.Count == 0)
        {
            normalizedOverrides.AddRange(baseScenario.Members.Select(member => new ProjectStaffingOverrideDto(
                member.UserId,
                member.UserId == baseScenario.ManagerUserId ? ProjectRoleRules.Manager : member.ProposedRole,
                member.ProposedHours,
                true,
                member.UserId == baseScenario.ManagerUserId)));
        }

        var normalizedSprintsResult = await NormalizeReviewedSprintsAsync(
            entity.OrganizationId,
            delivery,
            request.Sprints,
            ct);
        if (normalizedSprintsResult.Error != null)
            return Result.Failure<ProjectLaunchPlanDto>(
                normalizedSprintsResult.Error,
                422,
                normalizedSprintsResult.ErrorCode ?? "project_launch_plan_invalid");
        var normalizedSprints = normalizedSprintsResult.Sprints!;

        var ruleSet = entity.RuleSetId.HasValue
            ? await _db.OrganizationWorkRuleSets.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == entity.RuleSetId.Value, ct)
            : null;
        var rules = ruleSet == null
            ? Array.Empty<OrganizationWorkRuleDto>()
            : JsonSerializer.Deserialize<OrganizationWorkRuleDto[]>(ruleSet.RulesJson, JsonOptions) ?? [];
        var maxUtilizationPercent = NumericRule(rules, "max_utilization_percent", 85m);
        var reviewerCoordinationOverheadPercent = NumericRule(rules, "reviewer_coordination_overhead_percent", 10m);

        var selectedScenario = BuildCustomizedScenario(
            baseScenario,
            normalizedOverrides,
            normalizedSprints,
            maxUtilizationPercent,
            delivery.AssignmentMode);
        var assignmentResult = BalanceReviewedAssignments(
            normalizedSprints,
            selectedScenario.Members,
            delivery.AssignmentMode,
            selectedScenario.ManagerUserId,
            reviewerCoordinationOverheadPercent,
            maxUtilizationPercent,
            explicitlyPinnedTaskIds,
            enforceAllocation: string.Equals(
                delivery.AssignmentMode,
                ProjectLaunchAssignmentModes.PreserveAssignments,
                StringComparison.Ordinal));
        if (assignmentResult.Error != null)
            selectedScenario = selectedScenario with
            {
                Feasible = false,
                BlockingReasons = selectedScenario.BlockingReasons
                    .Append(assignmentResult.Error)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        var updatedSprints = assignmentResult.Sprints ?? normalizedSprints;
        selectedScenario = EvaluateTimePhasedCapacity(
            selectedScenario,
            updatedSprints,
            reviewerCoordinationOverheadPercent,
            maxUtilizationPercent,
            entity.SourceVersionHash,
            delivery.AssignmentMode);
        var updatedScenarios = scenarios.Where(item => item.ScenarioId != "custom").Append(selectedScenario).ToArray();
        delivery = delivery with
        {
            Sprints = updatedSprints,
            SuggestedTeamSize = selectedScenario.Members.Count,
            ScheduleRisks = delivery.ScheduleRisks
                .Concat(selectedScenario.Risks)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };

        entity.StaffingScenariosJson = JsonSerializer.Serialize(updatedScenarios, JsonOptions);
        entity.DeliveryPlanJson = JsonSerializer.Serialize(delivery, JsonOptions);
        entity.SelectedScenarioId = selectedScenario.ScenarioId;
        entity.BlockingReasonsJson = JsonSerializer.Serialize(selectedScenario.BlockingReasons, JsonOptions);
        entity.State = selectedScenario.Feasible ? "pending_review" : "blocked";
        entity.RowRevision += 1;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organizationName = await _db.Organizations.AsNoTracking()
            .Where(item => item.Id == entity.OrganizationId)
            .Select(item => item.Name)
            .SingleAsync(ct);
        var mapped = await MapPlanAsync(entity, organizationName, ct);
        await UpdateAssistantResponseAsync(entity.AssistantTurnId, mapped, ct);
        return Result.Success(mapped);
    }

    private async Task<ReviewedSprintsResult> NormalizeReviewedSprintsAsync(
        Guid organizationId,
        ProjectLaunchDeliveryPlanDto delivery,
        IReadOnlyList<ProjectLaunchSprintPlanDto> sprints,
        CancellationToken ct)
    {
        var skills = await _db.OrganizationSkills.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.IsActive)
            .OrderBy(item => item.Id)
            .ToArrayAsync(ct);
        var skillsById = skills.ToDictionary(item => item.Id);
        var skillsByName = skills
            .GroupBy(item => Normalize(item.Name), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var features = (delivery.Features ?? []).Where(IsInScopeFeature).ToArray();
        if (features.Length == 0)
            return new(null, "Kế hoạch phải có ít nhất một chức năng trong phạm vi để truy vết Task.", "project_launch_feature_required");
        var featuresById = features.ToDictionary(item => item.FeatureId, StringComparer.Ordinal);
        var metricIds = (delivery.ObjectiveMetrics ?? []).Select(item => item.MetricId).ToArray();
        var validMetricIds = metricIds.ToHashSet(StringComparer.Ordinal);
        var normalizedSprints = new List<ProjectLaunchSprintPlanDto>();
        var taskOrdinal = 0;
        foreach (var sprint in sprints)
        {
            var normalizedTasks = new List<ProjectLaunchTaskPlanDto>();
            foreach (var task in sprint.Tasks)
            {
                var featureId = string.IsNullOrWhiteSpace(task.FeatureId)
                    ? features[taskOrdinal % features.Length].FeatureId
                    : task.FeatureId;
                if (!featuresById.TryGetValue(featureId, out var feature))
                    return new(null, $"Task '{task.Title}' đang trỏ tới chức năng không còn trong phạm vi.", "project_launch_feature_invalid");

                var requestedSkillIds = task.RequiredSkillIds.Distinct().ToArray();
                if (requestedSkillIds.Any(id => !skillsById.ContainsKey(id)))
                    return new(null, $"Task '{task.Title}' chứa kỹ năng không hoạt động hoặc không thuộc tổ chức này.", "project_launch_skill_invalid");
                var requiredNames = task.RequiredSkillNames
                    .Concat(feature.RequiredSkillNames)
                    .Concat(requestedSkillIds.Select(id => skillsById[id].Name))
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var resolvedSkillIds = requiredNames
                    .Select(item => skillsByName.GetValueOrDefault(Normalize(item))?.Id)
                    .Where(item => item.HasValue)
                    .Select(item => item!.Value)
                    .Concat(requestedSkillIds)
                    .Distinct()
                    .ToArray();

                var requestedMetricIds = (task.ObjectiveMetricIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
                if (requestedMetricIds.Any(id => !validMetricIds.Contains(id)))
                    return new(null, $"Task '{task.Title}' đang trỏ tới thước đo không còn trong Launch Brief.", "project_launch_metric_invalid");
                var linkedMetricIds = requestedMetricIds.Length > 0 || metricIds.Length == 0
                    ? requestedMetricIds
                    : [metricIds[taskOrdinal % metricIds.Length]];
                normalizedTasks.Add(task with
                {
                    FeatureId = featureId,
                    ObjectiveMetricIds = linkedMetricIds,
                    RequiredSkillIds = resolvedSkillIds,
                    RequiredSkillNames = requiredNames
                });
                taskOrdinal++;
            }
            normalizedSprints.Add(sprint with { Tasks = normalizedTasks });
        }
        return new(normalizedSprints, null, null);
    }

    private static ProjectStaffingScenarioDto BuildCustomizedScenario(
        ProjectStaffingScenarioDto source,
        IReadOnlyList<ProjectStaffingOverrideDto> overrides,
        IReadOnlyList<ProjectLaunchSprintPlanDto> sprints,
        decimal maxUtilizationPercent,
        string assignmentMode)
    {
        var candidates = source.ManagerCandidates.ToDictionary(item => item.UserId);
        var selected = overrides.Where(item => item.Included).DistinctBy(item => item.UserId).ToArray();
        var requiredSkills = sprints.Where(item => item.Selected)
            .SelectMany(item => item.Tasks.Where(task => task.Selected))
            .SelectMany(item => item.RequiredSkillNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var blocking = new List<string>();
        var managerOverrides = selected.Where(item => item.Manager).ToArray();
        if (managerOverrides.Length != 1)
            blocking.Add("Hãy chọn đúng một người quản lý dự án.");

        var members = new List<ProjectStaffingMemberDto>();
        foreach (var item in selected)
        {
            if (!candidates.TryGetValue(item.UserId, out var candidate))
            {
                blocking.Add("Một thành viên đã chọn không còn thuộc danh sách được phép.");
                continue;
            }
            if (!candidate.StaffingEligible || candidate.HardRejects.Count > 0)
                blocking.Add($"{candidate.DisplayName} chưa vượt kiểm tra lịch, capacity hoặc giới hạn đa dự án.");
            if (item.Manager && !candidate.ManagerEligible)
                blocking.Add($"{candidate.DisplayName} không có vai trò phù hợp để quản lý dự án.");
            if ((string.Equals(assignmentMode, ProjectLaunchAssignmentModes.PreserveAssignments, StringComparison.Ordinal) && item.ProposedHours <= 0) ||
                item.ProposedHours > candidate.AvailableHours)
                blocking.Add(string.Equals(assignmentMode, ProjectLaunchAssignmentModes.PreserveAssignments, StringComparison.Ordinal)
                    ? $"Số giờ của {candidate.DisplayName} phải lớn hơn 0 và không vượt {candidate.AvailableHours:0.#} giờ khả dụng."
                    : $"Số giờ của {candidate.DisplayName} không được vượt {candidate.AvailableHours:0.#} giờ khả dụng; chế độ tự cân bằng sẽ tính lại allocation thực tế.");

            var normalizedEvidence = candidate.EvidenceSkills.Select(Normalize).ToHashSet(StringComparer.Ordinal);
            var covered = requiredSkills.Where(skill => normalizedEvidence.Contains(Normalize(skill))).ToArray();
            var missing = requiredSkills.Where(skill => !normalizedEvidence.Contains(Normalize(skill))).ToArray();
            var loadAfter = candidate.WindowCapacityHours <= 0
                ? 100m
                : Math.Round((candidate.ExistingCommittedHours + item.ProposedHours) * 100m / candidate.WindowCapacityHours, 2);
            members.Add(new ProjectStaffingMemberDto(
                item.UserId,
                candidate.DisplayName,
                item.Manager ? "Manager" : item.ProposedRole,
                item.ProposedHours,
                covered,
                missing,
                loadAfter,
                ["user_reviewed", "capacity_revalidated", covered.Length > 0 ? "confirmed_skill_evidence" : "capacity_support"],
                0m,
                candidate.WeeklyCapacity));
        }

        if (members.Count == 0) blocking.Add("Cần chọn ít nhất một thành viên.");
        var uncovered = requiredSkills.Where(skill => members.All(member =>
            !member.CoveredSkills.Contains(skill, StringComparer.OrdinalIgnoreCase))).ToArray();
        if (uncovered.Length > 0)
            blocking.Add($"Chưa đủ bằng chứng kỹ năng: {string.Join(", ", uncovered)}.");
        // Allocation is derived from the reviewed Task graph in auto-balance
        // mode, while preserve mode may deliberately keep work in backlog.
        // A raw sum check here would therefore either block a valid automatic
        // rebalance or silently undo the user's backlog choice.
        if (members.Any(item => item.LoadAfterPercent > maxUtilizationPercent))
            blocking.Add($"Một hoặc nhiều thành viên vượt ngưỡng sử dụng {maxUtilizationPercent:0.#}% theo quy tắc làm việc.");

        var manager = managerOverrides.Length == 1
            ? members.FirstOrDefault(item => item.UserId == managerOverrides[0].UserId)
            : null;
        var feasible = blocking.Count == 0;
        var score = feasible && members.Count > 0
            ? Math.Max(0m, 100m - members.Average(item => item.LoadAfterPercent) * 0.35m)
            : 0m;
        return source with
        {
            ScenarioId = "custom",
            Title = "Tùy chỉnh của bạn",
            Description = "Đội hình đã được kiểm tra lại theo kỹ năng, lịch, tải đa dự án và capacity hiện tại.",
            Feasible = feasible,
            Score = Math.Round(score, 2),
            ManagerUserId = manager?.UserId,
            ManagerName = manager?.DisplayName,
            Members = members,
            MissingSkills = uncovered,
            BlockingReasons = blocking,
            Risks = feasible ? [] : ["Cần xử lý các điểm bị chặn trước khi tạo Project."],
            Assumptions = source.Assumptions.Concat(["Đội hình do người dùng tùy chỉnh và đã được máy chủ revalidate."]).ToArray()
        };
    }

    private static AssignmentBalanceResult BalanceReviewedAssignments(
        IReadOnlyList<ProjectLaunchSprintPlanDto> sprints,
        IReadOnlyList<ProjectStaffingMemberDto> members,
        string assignmentMode,
        Guid? managerUserId,
        decimal reviewerCoordinationOverheadPercent,
        decimal maxUtilizationPercent,
        HashSet<string>? explicitlyPinnedTaskIds = null,
        bool enforceAllocation = true)
    {
        var memberIds = members.Select(item => item.UserId).ToHashSet();
        var assignedHours = members.ToDictionary(item => item.UserId, _ => 0m);
        var weeklyAllocations = BuildMutableWeeklyAllocations(members);
        var normalized = new List<ProjectLaunchSprintPlanDto>();
        foreach (var sprint in sprints)
        {
            var tasks = new List<ProjectLaunchTaskPlanDto>();
            foreach (var task in sprint.Tasks)
            {
                if (!task.Selected)
                {
                    tasks.Add(task);
                    continue;
                }

                ProjectStaffingMemberDto? assignee = null;
                ProjectStaffingMemberDto? reviewer = null;
                if (string.Equals(assignmentMode, ProjectLaunchAssignmentModes.AutoBalance, StringComparison.Ordinal))
                {
                    // Auto-balance is an explicit instruction to recompute the
                    // whole selected graph. Assignee ids in the submitted draft
                    // may be leftovers from an older scenario/revision and must
                    // not pin every Task to that person. A reviewer chosen by the
                    // user is only a preference; hard skill and weekly-capacity
                    // constraints still win.
                    var assignment = SelectTimePhasedAssignment(
                        sprint,
                        task,
                        members,
                        weeklyAllocations,
                        reviewerCoordinationOverheadPercent,
                        maxUtilizationPercent,
                        managerUserId,
                        requiredAssigneeId: explicitlyPinnedTaskIds?.Contains(task.ClientId) == true
                            ? task.ProposedAssigneeId
                            : null,
                        preferredReviewerId: task.ProposedReviewerId);
                    assignee = assignment?.Assignee;
                    reviewer = assignment?.Reviewer;
                }
                else if (task.ProposedAssigneeId.HasValue && memberIds.Contains(task.ProposedAssigneeId.Value))
                {
                    assignee = members.Single(item => item.UserId == task.ProposedAssigneeId.Value);
                    reviewer = task.ProposedReviewerId.HasValue && memberIds.Contains(task.ProposedReviewerId.Value) &&
                               task.ProposedReviewerId != assignee.UserId
                        ? members.Single(item => item.UserId == task.ProposedReviewerId.Value)
                        : null;
                }
                else if (task.ProposedAssigneeId.HasValue)
                    return new(null, $"Người được chọn cho Task '{task.Title}' không còn thuộc đội hình. Hãy chọn lại hoặc để chưa giao.");

                if (string.Equals(assignmentMode, ProjectLaunchAssignmentModes.AutoBalance, StringComparison.Ordinal) &&
                    (assignee == null || !CombinedSkillsCover(task.RequiredSkillNames, assignee, reviewer)))
                    return new(null, $"Chưa có cặp người thực hiện/reviewer vừa phủ đủ kỹ năng vừa còn capacity ở đúng các tuần của Task '{task.Title}'. Hãy thêm người có bằng chứng kỹ năng, đổi lịch Sprint, giảm estimate hoặc chuyển việc này sang backlog chưa giao.");

                if (assignee != null)
                {
                    assignedHours[assignee.UserId] += task.EstimatedHours;
                    if (enforceAllocation && assignedHours[assignee.UserId] > assignee.ProposedHours)
                        return new(null, $"Task đã giao cho {assignee.DisplayName} cần {assignedHours[assignee.UserId]:0.##} giờ, vượt allocation {assignee.ProposedHours:0.##} giờ đã review.");
                }

                var reviewerId = reviewer?.UserId ??
                    (task.ProposedReviewerId.HasValue && memberIds.Contains(task.ProposedReviewerId.Value) &&
                     task.ProposedReviewerId != assignee?.UserId
                        ? task.ProposedReviewerId
                        : string.Equals(assignmentMode, ProjectLaunchAssignmentModes.AutoBalance, StringComparison.Ordinal)
                             ? SelectReviewerForAssignee(task.RequiredSkillNames, assignee, members)?.UserId
                             : null);
                if (task.ProposedReviewerId.HasValue && reviewerId == null &&
                    string.Equals(assignmentMode, ProjectLaunchAssignmentModes.PreserveAssignments, StringComparison.Ordinal))
                    return new(null, $"Người review của Task '{task.Title}' không hợp lệ hoặc trùng người thực hiện.");
                if (string.Equals(assignmentMode, ProjectLaunchAssignmentModes.AutoBalance, StringComparison.Ordinal) && assignee != null)
                {
                    ReserveTimePhasedAssignment(
                        sprint,
                        task,
                        assignee.UserId,
                        reviewerId ?? managerUserId ?? assignee.UserId,
                        weeklyAllocations,
                        reviewerCoordinationOverheadPercent);
                }
                tasks.Add(task with { ProposedAssigneeId = assignee?.UserId, ProposedReviewerId = reviewerId });
            }
            normalized.Add(sprint with { Tasks = tasks });
        }
        return new(normalized, null);
    }

    private static HashSet<string> FindExplicitlyPinnedTaskIds(
        IReadOnlyList<ProjectLaunchSprintPlanDto> current,
        IReadOnlyList<ProjectLaunchSprintPlanDto> submitted)
    {
        var currentTasks = current.SelectMany(item => item.Tasks)
            .ToDictionary(item => item.ClientId, StringComparer.Ordinal);
        var selected = submitted.SelectMany(item => item.Tasks).Where(item => item.Selected).ToArray();
        var changed = selected.Where(item => currentTasks.TryGetValue(item.ClientId, out var prior) &&
                                             item.ProposedAssigneeId.HasValue &&
                                             (item.ProposedAssigneeId != prior.ProposedAssigneeId ||
                                              item.ProposedReviewerId != prior.ProposedReviewerId))
            .ToArray();

        // A single assignment copied over the whole graph is scenario/default
        // state, not an instruction to defeat auto-balance. A bounded subset is
        // treated as an intentional user pin and remains stable on save.
        if (changed.Length == 0 ||
            (changed.Length == selected.Length &&
             changed.Select(item => item.ProposedAssigneeId).Distinct().Count() == 1 &&
             changed.Select(item => item.ProposedReviewerId).Distinct().Count() <= 1))
            return new HashSet<string>(StringComparer.Ordinal);

        return changed.Select(item => item.ClientId).ToHashSet(StringComparer.Ordinal);
    }

    private static AssignmentSelection? SelectBalancedAssignment(
        IReadOnlyList<string> requiredSkills,
        decimal taskHours,
        IReadOnlyList<ProjectStaffingMemberDto> members,
        IReadOnlyDictionary<Guid, decimal> assignedHours,
        bool enforceAllocation = true)
    {
        var choices = members.SelectMany(assignee =>
            members.Where(item => item.UserId != assignee.UserId)
                .Cast<ProjectStaffingMemberDto?>()
                .Append(null)
                .Select(reviewer => new
                {
                    Assignee = assignee,
                    Reviewer = reviewer,
                    Remaining = enforceAllocation
                        ? assignee.ProposedHours - assignedHours.GetValueOrDefault(assignee.UserId)
                        : assignee.ProposedHours,
                    Assigned = assignedHours.GetValueOrDefault(assignee.UserId),
                    AssigneeSkillMatches = requiredSkills.Count(required =>
                        assignee.CoveredSkills.Any(skill => Normalize(skill) == Normalize(required))),
                    CoversAll = CombinedSkillsCover(requiredSkills, assignee, reviewer)
                }));
        var selected = choices
            .Where(item => (!enforceAllocation || item.Remaining >= taskHours) && item.CoversAll)
            .OrderByDescending(item => item.AssigneeSkillMatches)
            .ThenBy(item => item.Assigned)
            .ThenByDescending(item => item.Remaining)
            .ThenBy(item => item.Reviewer == null ? 1 : 0)
            .ThenByDescending(item => string.Equals(item.Reviewer?.ProposedRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal))
            .ThenBy(item => item.Assignee.UserId)
            .FirstOrDefault();
        return selected == null ? null : new AssignmentSelection(selected.Assignee, selected.Reviewer);
    }

    private static ProjectStaffingMemberDto? SelectReviewerForAssignee(
        IReadOnlyList<string> requiredSkills,
        ProjectStaffingMemberDto? assignee,
        IReadOnlyList<ProjectStaffingMemberDto> members)
    {
        if (assignee == null) return null;
        return members
            .Where(item => item.UserId != assignee.UserId && CombinedSkillsCover(requiredSkills, assignee, item))
            .OrderByDescending(item => string.Equals(item.ProposedRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal))
            .ThenBy(item => item.LoadAfterPercent)
            .ThenBy(item => item.UserId)
            .FirstOrDefault();
    }

    private static bool CombinedSkillsCover(
        IReadOnlyList<string> requiredSkills,
        ProjectStaffingMemberDto assignee,
        ProjectStaffingMemberDto? reviewer)
    {
        if (requiredSkills.Count == 0) return true;
        var covered = assignee.CoveredSkills
            .Concat(reviewer?.CoveredSkills ?? [])
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);
        return requiredSkills.All(skill => covered.Contains(Normalize(skill)));
    }

    private sealed record AssignmentSelection(
        ProjectStaffingMemberDto Assignee,
        ProjectStaffingMemberDto? Reviewer);

    private static ProjectStaffingMemberDto? SelectReviewer(
        IReadOnlyList<ProjectStaffingMemberDto> members,
        Guid? assigneeId)
        => members.Where(item => item.UserId != assigneeId)
            .OrderByDescending(item => string.Equals(item.ProposedRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal))
            .ThenBy(item => item.LoadAfterPercent)
            .ThenBy(item => item.UserId)
            .FirstOrDefault();

    private static string? ValidateSprints(
        ProjectLaunchDeliveryPlanDto delivery,
        IReadOnlyList<ProjectLaunchSprintPlanDto> sprints)
    {
        var selected = sprints.Where(item => item.Selected).ToArray();
        if (selected.Length == 0) return "Cần giữ lại ít nhất một Sprint.";
        if (selected.Any(item => string.IsNullOrWhiteSpace(item.Name) || item.EndDate <= item.StartDate))
            return "Tên và khoảng thời gian Sprint chưa hợp lệ.";
        if (selected.Any(item => item.StartDate < delivery.StartDate || item.EndDate > delivery.EndDate))
            return "Sprint phải nằm hoàn toàn trong timebox của Project đã review.";
        var chronological = selected.OrderBy(item => item.StartDate).ThenBy(item => item.EndDate).ToArray();
        if (string.Equals(delivery.ScheduleMode, ProjectLaunchScheduleModes.SequentialSprints, StringComparison.Ordinal) &&
            chronological.Zip(chronological.Skip(1), (previous, current) => current.StartDate < previous.EndDate).Any(overlap => overlap))
            return "Các Sprint được chọn không được chồng thời gian lên nhau.";
        var tasks = selected.SelectMany(item => item.Tasks.Where(task => task.Selected)).ToArray();
        if (tasks.Length == 0) return "Cần giữ lại ít nhất một Task.";
        if (tasks.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.EstimatedHours is <= 0 or > 1000))
            return "Task phải có tiêu đề và estimate từ 1 đến 1000 giờ.";
        var ids = tasks.Select(item => item.ClientId).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            return "Client ID của Task phải duy nhất.";
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        if (tasks.SelectMany(item => item.DependencyClientIds).Any(dependency => !idSet.Contains(dependency)))
            return "Một dependency đang trỏ tới Task không còn được chọn.";
        var sprintByTask = chronological.SelectMany(sprint => sprint.Tasks.Where(task => task.Selected)
                .Select(task => new { task.ClientId, Sprint = sprint }))
            .ToDictionary(item => item.ClientId, item => item.Sprint, StringComparer.Ordinal);
        if (tasks.Any(task => task.DependencyClientIds.Any(predecessor =>
                sprintByTask[predecessor].ClientId != sprintByTask[task.ClientId].ClientId &&
                sprintByTask[predecessor].EndDate > sprintByTask[task.ClientId].StartDate)))
            return "Dependency không hợp lệ: workstream/Sprint tiền nhiệm chưa kết thúc trước khi công việc phụ thuộc bắt đầu.";
        return HasDependencyCycle(tasks) ? "Dependency giữa các Task đang tạo thành vòng lặp." : null;
    }

    private static bool HasDependencyCycle(IReadOnlyList<ProjectLaunchTaskPlanDto> tasks)
    {
        var dependencies = tasks.ToDictionary(item => item.ClientId, item => item.DependencyClientIds, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            foreach (var dependency in dependencies.GetValueOrDefault(id) ?? [])
                if (Visit(dependency)) return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
        return dependencies.Keys.Any(Visit);
    }

    private sealed record ReviewedSprintsResult(
        IReadOnlyList<ProjectLaunchSprintPlanDto>? Sprints,
        string? Error,
        string? ErrorCode);

    private sealed record AssignmentBalanceResult(
        IReadOnlyList<ProjectLaunchSprintPlanDto>? Sprints,
        string? Error);
}
