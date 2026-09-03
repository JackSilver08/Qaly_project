using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class ProjectLaunchOrchestratorService
{
    private static readonly HashSet<string> CompletedTaskStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "done", "completed", "closed"
    };

    public async Task<Result<ProjectLaunchPlanDto>> MonitorAsync(
        Guid executionId,
        MonitorProjectLaunchExecutionRequestDto request,
        CancellationToken ct = default)
    {
        if (!_options.ProjectOperationMonitoringEnabled)
            return Result.Failure<ProjectLaunchPlanDto>("Project operation monitoring is disabled.", 503, "project_operation_monitoring_disabled");

        var execution = await _db.ProjectLaunchExecutions.IgnoreQueryFilters()
            .Include(item => item.ProjectLaunchPlanArtifact)
            .SingleOrDefaultAsync(item => item.Id == executionId, ct);
        if (execution == null) return Result.NotFound<ProjectLaunchPlanDto>();

        var access = await AuthorizeOrganizationAsync(execution.OrganizationId, manage: true, ct);
        if (!access.IsSuccess)
            return Result.Failure<ProjectLaunchPlanDto>(access.Error!, access.StatusCode, access.ErrorCode);
        if (execution.RowRevision != request.ExpectedRevision)
            return Result.Failure<ProjectLaunchPlanDto>("The execution receipt changed; reload before monitoring.", 409, "project_launch_execution_stale");

        await EvaluateExecutionAsync(execution, _currentUser.UserId, ct);
        var organizationName = await _db.Organizations.AsNoTracking()
            .Where(item => item.Id == execution.OrganizationId)
            .Select(item => item.Name)
            .SingleAsync(ct);
        var plan = await MapPlanAsync(execution.ProjectLaunchPlanArtifact, organizationName, ct);
        await UpdateAssistantResponseAsync(execution.ProjectLaunchPlanArtifact.AssistantTurnId, plan, ct);
        return Result.Success(plan);
    }

    public async Task<Result<ProjectLaunchPlanDto>> MonitorProjectAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        if (!_options.ProjectOperationMonitoringEnabled)
            return Result.Failure<ProjectLaunchPlanDto>("Project operation monitoring is disabled.", 503, "project_operation_monitoring_disabled");
        var execution = await _db.ProjectLaunchExecutions.IgnoreQueryFilters()
            .Include(item => item.ProjectLaunchPlanArtifact)
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.ExecutedAt)
            .FirstOrDefaultAsync(ct);
        if (execution == null)
            return Result.Failure<ProjectLaunchPlanDto>("This Project has no AI launch execution baseline to monitor.", 404, "project_launch_execution_not_found");
        var access = await AuthorizeOrganizationAsync(execution.OrganizationId, manage: true, ct);
        if (!access.IsSuccess)
            return Result.Failure<ProjectLaunchPlanDto>(access.Error!, access.StatusCode, access.ErrorCode);
        await EvaluateExecutionAsync(execution, _currentUser.UserId, ct);
        var organizationName = await _db.Organizations.AsNoTracking()
            .Where(item => item.Id == execution.OrganizationId)
            .Select(item => item.Name)
            .SingleAsync(ct);
        var plan = await MapPlanAsync(execution.ProjectLaunchPlanArtifact, organizationName, ct);
        await UpdateAssistantResponseAsync(execution.ProjectLaunchPlanArtifact.AssistantTurnId, plan, ct);
        return Result.Success(plan);
    }

    public async Task EvaluateDueAsync(CancellationToken ct = default)
    {
        if (!_options.ProjectOperationMonitoringEnabled) return;
        var now = DateTimeOffset.UtcNow;
        // Monitoring is an operational read: it must still observe executions whose
        // Project was soft-deleted so that deletion becomes an explicit replan signal.
        var dueIds = await _db.ProjectLaunchExecutions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Status == "executed" && item.MonitoringEnabled &&
                (!item.NextMonitorAt.HasValue || item.NextMonitorAt <= now))
            .OrderBy(item => item.NextMonitorAt)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .Take(25)
            .ToArrayAsync(ct);

        foreach (var executionId in dueIds)
        {
            try
            {
                var execution = await _db.ProjectLaunchExecutions.IgnoreQueryFilters()
                    .Include(item => item.ProjectLaunchPlanArtifact)
                    .SingleOrDefaultAsync(item => item.Id == executionId, ct);
                if (execution != null) await EvaluateExecutionAsync(execution, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _db.ChangeTracker.Clear();
                MonitorExecutionChanged(_logger, executionId, exception);
            }
            catch (Exception exception)
            {
                _db.ChangeTracker.Clear();
                await DeferFailedMonitorAsync(executionId, now.AddMinutes(30), ct);
                MonitorExecutionFailed(_logger, executionId, exception);
            }
        }
    }

    private async Task EvaluateExecutionAsync(
        ProjectLaunchExecution execution,
        Guid? actorUserId,
        CancellationToken ct)
    {
        if (execution.Status != "executed")
            throw new InvalidOperationException("Only an executed Project launch can be monitored.");

        var plan = execution.ProjectLaunchPlanArtifact;
        var delivery = JsonSerializer.Deserialize<ProjectLaunchDeliveryPlanDto>(plan.DeliveryPlanJson, JsonOptions)
            ?? throw new InvalidOperationException("Persisted Project launch delivery plan is invalid.");
        var scenarios = JsonSerializer.Deserialize<ProjectStaffingScenarioDto[]>(plan.StaffingScenariosJson, JsonOptions) ?? [];
        var scenario = scenarios.SingleOrDefault(item => item.ScenarioId == plan.SelectedScenarioId)
            ?? throw new InvalidOperationException("The executed staffing scenario is unavailable.");

        var project = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == execution.ProjectId, ct);
        var rawTasks = await _db.TaskItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                item.Id, item.Title, item.Status, item.AssigneeId, item.ReviewerId, item.SprintId,
                item.DueDate, item.EstimatedHours, item.ActualHours, item.IsDeleted, item.UpdatedAt
            })
            .ToArrayAsync(ct);
        var rawTraces = await _db.ProjectLaunchTaskTraces.AsNoTracking()
            .Where(item => item.ProjectLaunchBriefId == plan.ProjectLaunchBriefId && item.TaskItem.ProjectId == execution.ProjectId)
            .Select(item => new
            {
                item.TaskItemId, item.TaskClientId, item.SprintClientId, item.FeatureId,
                item.ObjectiveMetricIdsJson, item.SourceRefsJson
            })
            .ToArrayAsync(ct);
        var taskIds = rawTasks.Select(item => item.Id).ToArray();
        var skillRows = await _db.TaskSkillRequirements.AsNoTracking()
            .Where(item => taskIds.Contains(item.TaskItemId))
            .Select(item => new { item.TaskItemId, item.OrganizationSkillId })
            .ToArrayAsync(ct);
        var dependencyRows = await _db.TaskDependencies.AsNoTracking()
            .Where(item => taskIds.Contains(item.PredecessorId) || taskIds.Contains(item.SuccessorId))
            .Select(item => new { item.PredecessorId, item.SuccessorId })
            .ToArrayAsync(ct);
        var traceByTaskId = rawTraces.ToDictionary(item => item.TaskItemId);
        var clientIdByTaskId = rawTraces.ToDictionary(item => item.TaskItemId, item => item.TaskClientId);
        var tasks = rawTasks.Select(item =>
        {
            traceByTaskId.TryGetValue(item.Id, out var trace);
            return new MonitorTaskFact(
                item.Id, item.Title, item.Status, item.AssigneeId, item.ReviewerId, item.SprintId, item.DueDate,
                item.EstimatedHours, item.ActualHours, item.IsDeleted, item.UpdatedAt,
                trace?.TaskClientId, trace?.SprintClientId, trace?.FeatureId,
                trace == null ? [] : ReadStringArray(trace.ObjectiveMetricIdsJson),
                trace == null ? [] : ReadStringArray(trace.SourceRefsJson),
                skillRows.Where(row => row.TaskItemId == item.Id).Select(row => row.OrganizationSkillId).ToArray(),
                dependencyRows.Where(row => row.SuccessorId == item.Id && clientIdByTaskId.ContainsKey(row.PredecessorId))
                    .Select(row => clientIdByTaskId[row.PredecessorId]).ToArray());
        }).ToArray();
        var memberRoles = await _db.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .OrderBy(item => item.UserId)
            .ToDictionaryAsync(item => item.UserId, item => item.Role, ct);
        var sprints = await _db.Set<Sprint>().AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .OrderBy(item => item.StartDate).ThenBy(item => item.Name)
            .Select(item => new MonitorSprintFact(item.Id, item.Name, item.Goal, item.StartDate, item.EndDate, item.Status))
            .ToArrayAsync(ct);
        var effectiveRule = await ResolveEffectiveRuleSetAsync(execution.OrganizationId, ct);

        var selectedTasks = delivery.Sprints.Where(item => item.Selected)
            .SelectMany(item => item.Tasks)
            .Where(item => item.Selected)
            .ToArray();
        var plannedRoles = BuildExpectedMemberRoles(scenario, execution.ExecutedByUserId);
        var plannedMemberIds = plannedRoles.Keys.OrderBy(item => item).ToArray();
        var baseline = new
        {
            projectId = execution.ProjectId,
            delivery.StartDate,
            delivery.EndDate,
            taskCount = selectedTasks.Length,
            estimatedHours = selectedTasks.Sum(item => item.EstimatedHours),
            memberIds = plannedMemberIds,
            ruleSetId = plan.RuleSetId,
            ruleVersion = effectiveRule is not null && effectiveRule.Id == plan.RuleSetId ? (int?)effectiveRule.Version : null
        };
        var current = new
        {
            projectId = execution.ProjectId,
            projectState = project == null ? "missing" : project.IsDeleted ? "deleted" : project.Status,
            project?.StartDate,
            project?.EndDate,
            tasks,
            memberRoles,
            sprints,
            ruleSetId = effectiveRule?.Id,
            ruleVersion = effectiveRule?.Version,
            observedAt = DateTimeOffset.UtcNow
        };
        var baselineHash = Hash(JsonSerializer.Serialize(baseline, JsonOptions));
        var currentHash = Hash(JsonSerializer.Serialize(current, JsonOptions));
        var changes = BuildReplanChanges(project, delivery, selectedTasks, tasks, sprints, plannedRoles, memberRoles, plan.RuleSetId, effectiveRule);
        changes.AddRange(await BuildCapacityAndAvailabilityChangesAsync(execution, delivery, scenario, ct));

        var now = DateTimeOffset.UtcNow;
        if (changes.Count > 0)
        {
            var latest = await _db.ProjectReplanProposals
                .Where(item => item.ProjectLaunchExecutionId == execution.Id)
                .OrderByDescending(item => item.Revision)
                .FirstOrDefaultAsync(ct);
            if (latest == null || !string.Equals(latest.CurrentHash, currentHash, StringComparison.Ordinal))
            {
                var proposal = new ProjectReplanProposal
                {
                    ProjectLaunchPlanArtifactId = plan.Id,
                    ProjectLaunchExecutionId = execution.Id,
                    OrganizationId = execution.OrganizationId,
                    ProjectId = execution.ProjectId,
                    Revision = (latest?.Revision ?? 0) + 1,
                    State = "pending_review",
                    TriggerCodesJson = JsonSerializer.Serialize(changes.Select(item => item.ChangeType).Distinct().ToArray(), JsonOptions),
                    ProposalJson = JsonSerializer.Serialize(changes, JsonOptions),
                    SourceSnapshotJson = JsonSerializer.Serialize(new[]
                    {
                        $"project:{execution.ProjectId}",
                        $"project_tasks:{execution.ProjectId}",
                        $"project_members:{execution.ProjectId}",
                        $"project_sprints:{execution.ProjectId}",
                        $"project_dependencies:{execution.ProjectId}",
                        $"organization_capacity:{execution.OrganizationId}",
                        $"organization_rulebook:{execution.OrganizationId}"
                    }, JsonOptions),
                    BaselineHash = baselineHash,
                    CurrentHash = currentHash,
                    CreatedByUserId = actorUserId,
                    RowRevision = 1
                };
                _db.ProjectReplanProposals.Add(proposal);
                _db.AuditLogs.Add(new AuditLog
                {
                    Action = "CreateProjectReplanProposal",
                    EntityType = nameof(ProjectLaunchExecution),
                    EntityId = execution.Id.ToString(),
                    UserId = actorUserId,
                    ChangesJson = JsonSerializer.Serialize(new
                    {
                        proposal.Id,
                        proposal.Revision,
                        triggerCodes = changes.Select(item => item.ChangeType).Distinct().ToArray()
                    }, JsonOptions)
                });
            }
        }

        execution.LastMonitoredAt = now;
        execution.NextMonitorAt = now.AddHours(6);
        execution.RowRevision++;
        execution.UpdatedAt = now;
        var receipt = JsonSerializer.Deserialize<ProjectLaunchExecutionReceiptDto>(execution.ReceiptJson, JsonOptions)
            ?? throw new InvalidOperationException("Execution receipt is invalid.");
        execution.ReceiptJson = JsonSerializer.Serialize(receipt with { Revision = execution.RowRevision }, JsonOptions);
        await _db.SaveChangesAsync(ct);
    }

    private static List<ProjectReplanChangeDto> BuildReplanChanges(
        Project? project,
        ProjectLaunchDeliveryPlanDto delivery,
        ProjectLaunchTaskPlanDto[] selectedTasks,
        IReadOnlyCollection<MonitorTaskFact> tasks,
        IReadOnlyCollection<MonitorSprintFact> sprints,
        Dictionary<Guid, string> plannedRoles,
        Dictionary<Guid, string> currentRoles,
        Guid? plannedRuleSetId,
        OrganizationWorkRuleSet? currentRule)
    {
        var changes = new List<ProjectReplanChangeDto>();
        if (project == null || project.IsDeleted)
        {
            changes.Add(new("project_state", "critical", "The launched Project is missing or deleted.", "active", project == null ? "missing" : "deleted", "Review recovery or formally close the launch plan."));
            return changes;
        }
        if (currentRule?.Id != plannedRuleSetId)
            changes.Add(new("rulebook_changed", "critical", "The effective Organization Rulebook changed.", plannedRuleSetId?.ToString() ?? "none", currentRule?.Id.ToString() ?? "none", "Revalidate staffing, capacity and approval constraints before changing work."));

        var activeTasks = tasks.Where(item => !item.IsDeleted).ToArray();
        if (activeTasks.Length != selectedTasks.Length)
            changes.Add(new("task_scope_changed", "warning", "The current task count differs from the confirmed launch scope.", selectedTasks.Length.ToString(CultureInfo.InvariantCulture), activeTasks.Length.ToString(CultureInfo.InvariantCulture), "Review added, removed or archived tasks and update the delivery baseline explicitly."));

        var membershipChanged = plannedRoles.Count != currentRoles.Count ||
            plannedRoles.Any(item => !currentRoles.TryGetValue(item.Key, out var role) || role != item.Value);
        if (membershipChanged)
            changes.Add(new("staffing_or_role_changed", "critical", "Thành viên hoặc vai trò Project đã khác phương án được duyệt.", DescribeRoles(plannedRoles), DescribeRoles(currentRoles), "Kiểm tra lại quyền, vai trò quản lý và khả năng phân tách người làm/người review trước khi tiếp tục."));

        var expectedSprints = delivery.Sprints.Where(item => item.Selected)
            .OrderBy(item => item.StartDate).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var actualSprints = sprints.OrderBy(item => item.StartDate).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var sprintChanged = expectedSprints.Length != actualSprints.Length;
        if (!sprintChanged)
        {
            for (var index = 0; index < expectedSprints.Length; index++)
            {
                var expected = expectedSprints[index];
                var actual = actualSprints[index];
                if (expected.Name != actual.Name || expected.Objective != actual.Goal || expected.StartDate != actual.StartDate ||
                    expected.EndDate != actual.EndDate || actual.Status != "Planning")
                {
                    sprintChanged = true;
                    break;
                }
            }
        }
        if (sprintChanged)
            changes.Add(new("sprint_baseline_changed", "critical", "Sprint hiện tại không còn khớp timebox/mục tiêu đã duyệt.", $"{expectedSprints.Length} Sprint theo baseline", $"{actualSprints.Length} Sprint hiện tại", "Review lại thứ tự, mốc thời gian và dependency; không tự dời lịch."));

        var expectedByClientId = delivery.Sprints.Where(item => item.Selected)
            .SelectMany(sprint => sprint.Tasks.Where(item => item.Selected).Select(task => (Sprint: sprint, Task: task)))
            .ToDictionary(item => item.Task.ClientId, StringComparer.Ordinal);
        var actualByClientId = activeTasks.Where(item => !string.IsNullOrWhiteSpace(item.TaskClientId))
            .ToDictionary(item => item.TaskClientId!, StringComparer.Ordinal);
        foreach (var (clientId, expected) in expectedByClientId)
        {
            if (!actualByClientId.TryGetValue(clientId, out var actual))
            {
                changes.Add(new("task_trace_missing", "critical", $"Không còn tìm thấy dấu vết canonical của task '{expected.Task.Title}'.", clientId, "missing", "Khôi phục trace hoặc review lại phạm vi; không tự tạo task thay thế."));
                continue;
            }
            if (actual.AssigneeId != expected.Task.ProposedAssigneeId || actual.ReviewerId != expected.Task.ProposedReviewerId)
                changes.Add(new("task_assignment_changed", "warning", $"Người thực hiện/review của '{expected.Task.Title}' đã thay đổi.", $"{expected.Task.ProposedAssigneeId}/{expected.Task.ProposedReviewerId}", $"{actual.AssigneeId}/{actual.ReviewerId}", "Kiểm tra lại kỹ năng, tải, lịch và nguyên tắc người review khác người thực hiện."));
            if (actual.SprintClientId != expected.Sprint.ClientId)
                changes.Add(new("task_sprint_changed", "warning", $"Task '{expected.Task.Title}' đã chuyển sang Sprint khác baseline.", expected.Sprint.Name, actual.SprintClientId ?? "none", "Kiểm tra critical path và thời gian của dependency trước khi chấp nhận thay đổi."));
            if (!SameSet(actual.RequiredSkillIds, expected.Task.RequiredSkillIds))
                changes.Add(new("task_skill_changed", "warning", $"Kỹ năng bắt buộc của '{expected.Task.Title}' đã thay đổi.", string.Join(",", expected.Task.RequiredSkillIds), string.Join(",", actual.RequiredSkillIds), "Đối chiếu catalog và skill evidence thật trước khi phân lại việc."));
            if (!SameSet(actual.DependencyClientIds, expected.Task.DependencyClientIds))
                changes.Add(new("task_dependency_changed", "critical", $"Dependency của '{expected.Task.Title}' đã khác graph được duyệt.", string.Join(",", expected.Task.DependencyClientIds), string.Join(",", actual.DependencyClientIds), "Kiểm tra chu trình và thứ tự Sprint trước khi cập nhật kế hoạch."));
            if (actual.FeatureId != (expected.Task.FeatureId ?? string.Empty) ||
                !SameSet(actual.ObjectiveMetricIds, expected.Task.ObjectiveMetricIds ?? []) ||
                !SameSet(actual.SourceRefs, expected.Task.SourceRefs))
                changes.Add(new("task_traceability_changed", "warning", $"Liên kết Feature/KPI/nguồn của '{expected.Task.Title}' đã thay đổi.", "trace theo Launch Brief", "trace hiện tại khác baseline", "Review lại khả năng truy vết từ mục tiêu → Feature → Sprint → Task trước khi báo tiến độ."));
        }

        var now = DateTimeOffset.UtcNow;
        var overdue = activeTasks.Where(item => TaskStatusRules.IsOverdue(item.Status, item.DueDate, now)).ToArray();
        if (overdue.Length > 0)
            changes.Add(new("tasks_overdue", "critical", $"{overdue.Length} task(s) are overdue.", "0", overdue.Length.ToString(CultureInfo.InvariantCulture), "Review blockers and propose a new dependency-aware schedule; do not silently move due dates."));

        var unassigned = activeTasks.Count(item => !item.AssigneeId.HasValue && TaskStatusRules.IsOpen(item.Status));
        if (unassigned > 0)
            changes.Add(new("tasks_unassigned", "warning", $"{unassigned} active task(s) have no assignee.", "0", unassigned.ToString(CultureInfo.InvariantCulture), "Propose eligible assignees using current skills, capacity and focus reserve."));

        var duration = delivery.EndDate - delivery.StartDate;
        if (duration > TimeSpan.Zero && now > delivery.StartDate && now < delivery.EndDate && activeTasks.Length > 0)
        {
            var elapsedRatio = Math.Clamp((now - delivery.StartDate).TotalSeconds / duration.TotalSeconds, 0d, 1d);
            var completedRatio = (double)activeTasks.Count(item => CompletedTaskStates.Contains(item.Status)) / activeTasks.Length;
            if (elapsedRatio - completedRatio >= 0.20d)
                changes.Add(new("progress_behind_baseline", "warning", "Completion is materially behind the elapsed delivery window.", $"{elapsedRatio:P0}", $"{completedRatio:P0}", "Review the critical path, blockers and capacity before proposing scope or schedule changes."));
        }

        var estimated = activeTasks.Sum(item => item.EstimatedHours ?? 0);
        var actualEffort = activeTasks.Sum(item => item.ActualHours ?? 0);
        if (estimated > 0 && actualEffort > estimated * 1.2m)
            changes.Add(new("effort_overrun", "warning", "Recorded effort exceeds the current estimate by more than 20%.", estimated.ToString(CultureInfo.InvariantCulture), actualEffort.ToString(CultureInfo.InvariantCulture), "Re-estimate remaining work from observed effort and keep historical actuals unchanged."));
        return changes;
    }

    private async Task<IReadOnlyList<ProjectReplanChangeDto>> BuildCapacityAndAvailabilityChangesAsync(
        ProjectLaunchExecution execution,
        ProjectLaunchDeliveryPlanDto delivery,
        ProjectStaffingScenarioDto scenario,
        CancellationToken ct)
    {
        var selectedIds = scenario.Members.Select(item => item.UserId)
            .Append(scenario.ManagerUserId.GetValueOrDefault())
            .Where(item => item != Guid.Empty)
            .Distinct().ToArray();
        var profiles = await _db.OrganizationMemberCapacityProfiles.AsNoTracking()
            .Include(item => item.AvailabilityWindows)
            .Where(item => item.OrganizationId == execution.OrganizationId && selectedIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, ct);
        var commitments = await _db.TaskItems.AsNoTracking()
            .Where(item => item.AssigneeId.HasValue && selectedIds.Contains(item.AssigneeId.Value) &&
                !item.Project.IsDeleted && !item.IsDeleted && TaskStatusRules.OpenStatuses.Contains(item.Status) &&
                (!item.StartDate.HasValue || item.StartDate < delivery.EndDate) && (!item.DueDate.HasValue || item.DueDate >= delivery.StartDate))
            .GroupBy(item => item.AssigneeId!.Value)
            .Select(group => new { UserId = group.Key, Hours = group.Sum(item => (decimal)(item.EstimatedHours ?? 8)) })
            .ToDictionaryAsync(item => item.UserId, item => item.Hours, ct);
        var changes = new List<ProjectReplanChangeDto>();
        var weeks = Math.Max(1m, decimal.Ceiling(Math.Max(1m, (decimal)(delivery.EndDate - delivery.StartDate).TotalDays) / 7m));
        foreach (var userId in selectedIds)
        {
            var baseline = scenario.ManagerCandidates.FirstOrDefault(item => item.UserId == userId);
            if (baseline == null || !profiles.TryGetValue(userId, out var profile))
            {
                changes.Add(new("capacity_evidence_missing", "critical", "Một thành viên trong phương án không còn capacity profile hợp lệ.", baseline?.CapacityState ?? "missing", "missing", "Bổ sung capacity/availability thật trước khi giao thêm việc."));
                continue;
            }
            var windowCapacity = profile.WeeklyCapacityHours * weeks;
            var unavailable = CalculateAvailabilityReduction(profile, delivery.StartDate, delivery.EndDate, profile.WeeklyCapacityHours);
            var currentCommitted = commitments.GetValueOrDefault(userId);
            var expectedCommitted = baseline.ExistingCommittedHours + baseline.ProposedHours;
            var currentEffectiveAvailable = windowCapacity - unavailable - currentCommitted - baseline.FocusReserveHours;
            if (profile.WeeklyCapacityHours != baseline.WeeklyCapacityHours || currentCommitted > expectedCommitted + 0.01m || currentEffectiveAvailable < 0m)
                changes.Add(new(
                    "capacity_or_availability_changed",
                    currentEffectiveAvailable < 0m ? "critical" : "warning",
                    $"Capacity/lịch của {baseline.DisplayName} đã khác lúc duyệt phương án.",
                    $"{baseline.WeeklyCapacityHours:0.##}h/tuần; tải dự kiến {expectedCommitted:0.##}h",
                    $"{profile.WeeklyCapacityHours:0.##}h/tuần; tải hiện tại {currentCommitted:0.##}h; nghỉ/giảm {unavailable:0.##}h",
                    "Tính lại allocation theo toàn bộ dự án và focus reserve; không dùng chỗ trống lịch như capacity."));
        }
        return changes;
    }

    private async Task DeferFailedMonitorAsync(Guid executionId, DateTimeOffset retryAt, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.ProjectLaunchExecutions.IgnoreQueryFilters()
                .Where(item => item.Id == executionId && item.Status == "executed" && item.MonitoringEnabled)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.NextMonitorAt, retryAt)
                    .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow)
                    .SetProperty(item => item.RowRevision, item => item.RowRevision + 1), ct);
            return;
        }

        var execution = await _db.ProjectLaunchExecutions.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == executionId, ct);
        if (execution == null || execution.Status != "executed" || !execution.MonitoringEnabled) return;
        execution.NextMonitorAt = retryAt;
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        execution.RowRevision++;
        await _db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Project launch execution {ExecutionId} changed while a scheduled monitor was running; the newer state wins.")]
    private static partial void MonitorExecutionChanged(ILogger logger, Guid executionId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Project launch execution {ExecutionId} could not be monitored and was deferred for retry.")]
    private static partial void MonitorExecutionFailed(ILogger logger, Guid executionId, Exception exception);

    private static Dictionary<Guid, string> BuildExpectedMemberRoles(ProjectStaffingScenarioDto scenario, Guid ownerId)
    {
        var roles = new Dictionary<Guid, string> { [ownerId] = ProjectRoleRules.Owner };
        if (scenario.ManagerUserId.HasValue && scenario.ManagerUserId.Value != ownerId)
            roles[scenario.ManagerUserId.Value] = ProjectRoleRules.Manager;
        foreach (var member in scenario.Members)
        {
            if (roles.ContainsKey(member.UserId)) continue;
            roles[member.UserId] = ProjectRoleRules.TryNormalizeAssignableRole(member.ProposedRole, out var role)
                ? role
                : member.ProposedRole;
        }
        return roles;
    }

    private static bool SameSet<T>(IEnumerable<T> left, IEnumerable<T> right) where T : notnull
        => new HashSet<T>(left).SetEquals(right);

    private static string DescribeRoles(Dictionary<Guid, string> roles)
        => string.Join(", ", roles.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"));

    private static ProjectReplanProposalDto MapReplan(ProjectReplanProposal entity)
        => new(
            entity.Id,
            AiProjectOrchestrationContract.ReplanSchemaId,
            entity.Revision,
            entity.State,
            entity.ProjectLaunchPlanArtifactId,
            entity.ProjectLaunchExecutionId,
            entity.ProjectId,
            JsonSerializer.Deserialize<string[]>(entity.TriggerCodesJson, JsonOptions) ?? [],
            JsonSerializer.Deserialize<ProjectReplanChangeDto[]>(entity.ProposalJson, JsonOptions) ?? [],
            [],
            JsonSerializer.Deserialize<string[]>(entity.SourceSnapshotJson, JsonOptions) ?? [],
            entity.BaselineHash,
            entity.CurrentHash,
            true,
            entity.CreatedAt,
            entity.RowRevision);

    private sealed record MonitorTaskFact(
        Guid Id,
        string Title,
        string Status,
        Guid? AssigneeId,
        Guid? ReviewerId,
        Guid? SprintId,
        DateTimeOffset? DueDate,
        int? EstimatedHours,
        int? ActualHours,
        bool IsDeleted,
        DateTimeOffset? UpdatedAt,
        string? TaskClientId,
        string? SprintClientId,
        string? FeatureId,
        IReadOnlyList<string> ObjectiveMetricIds,
        IReadOnlyList<string> SourceRefs,
        IReadOnlyList<Guid> RequiredSkillIds,
        IReadOnlyList<string> DependencyClientIds);

    private sealed record MonitorSprintFact(
        Guid Id,
        string Name,
        string? Goal,
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        string Status);
}
