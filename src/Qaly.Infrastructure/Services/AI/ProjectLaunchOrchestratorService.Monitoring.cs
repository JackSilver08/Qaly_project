using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
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
        var dueIds = await _db.ProjectLaunchExecutions.AsNoTracking()
            .Where(item => item.Status == "executed" && item.MonitoringEnabled &&
                (!item.NextMonitorAt.HasValue || item.NextMonitorAt <= now))
            .OrderBy(item => item.NextMonitorAt)
            .Select(item => item.Id)
            .Take(25)
            .ToArrayAsync(ct);

        foreach (var executionId in dueIds)
        {
            var execution = await _db.ProjectLaunchExecutions
                .Include(item => item.ProjectLaunchPlanArtifact)
                .SingleOrDefaultAsync(item => item.Id == executionId, ct);
            if (execution != null) await EvaluateExecutionAsync(execution, null, ct);
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
        var tasks = await _db.TaskItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .OrderBy(item => item.Id)
            .Select(item => new MonitorTaskFact(
                item.Id, item.Title, item.Status, item.AssigneeId, item.DueDate,
                item.EstimatedHours, item.ActualHours, item.IsDeleted, item.UpdatedAt))
            .ToArrayAsync(ct);
        var memberIds = await _db.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .OrderBy(item => item.UserId)
            .Select(item => item.UserId)
            .ToArrayAsync(ct);
        var effectiveRule = await ResolveEffectiveRuleSetAsync(execution.OrganizationId, ct);

        var selectedTasks = delivery.Sprints.Where(item => item.Selected)
            .SelectMany(item => item.Tasks)
            .Where(item => item.Selected)
            .ToArray();
        var plannedMemberIds = scenario.Members.Select(item => item.UserId)
            .Append(scenario.ManagerUserId.GetValueOrDefault())
            .Append(execution.ExecutedByUserId)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
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
            memberIds,
            ruleSetId = effectiveRule?.Id,
            ruleVersion = effectiveRule?.Version,
            observedAt = DateTimeOffset.UtcNow
        };
        var baselineHash = Hash(JsonSerializer.Serialize(baseline, JsonOptions));
        var currentHash = Hash(JsonSerializer.Serialize(current, JsonOptions));
        var changes = BuildReplanChanges(project, delivery, selectedTasks, tasks, plannedMemberIds, memberIds, plan.RuleSetId, effectiveRule);

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
        IReadOnlyCollection<Guid> plannedMemberIds,
        IReadOnlyCollection<Guid> currentMemberIds,
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

        var plannedMembers = plannedMemberIds.OrderBy(item => item).ToArray();
        var currentMembers = currentMemberIds.OrderBy(item => item).ToArray();
        if (!plannedMembers.SequenceEqual(currentMembers))
            changes.Add(new("staffing_changed", "warning", "Project membership differs from the confirmed staffing scenario.", plannedMembers.Length.ToString(CultureInfo.InvariantCulture), currentMembers.Length.ToString(CultureInfo.InvariantCulture), "Recheck role coverage, capacity and separation-of-duties before reassigning work."));

        var now = DateTimeOffset.UtcNow;
        var overdue = activeTasks.Where(item => item.DueDate < now && !CompletedTaskStates.Contains(item.Status)).ToArray();
        if (overdue.Length > 0)
            changes.Add(new("tasks_overdue", "critical", $"{overdue.Length} task(s) are overdue.", "0", overdue.Length.ToString(CultureInfo.InvariantCulture), "Review blockers and propose a new dependency-aware schedule; do not silently move due dates."));

        var unassigned = activeTasks.Count(item => !item.AssigneeId.HasValue && !CompletedTaskStates.Contains(item.Status));
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
        var actual = activeTasks.Sum(item => item.ActualHours ?? 0);
        if (estimated > 0 && actual > estimated * 1.2m)
            changes.Add(new("effort_overrun", "warning", "Recorded effort exceeds the current estimate by more than 20%.", estimated.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture), "Re-estimate remaining work from observed effort and keep historical actuals unchanged."));
        return changes;
    }

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
        DateTimeOffset? DueDate,
        int? EstimatedHours,
        int? ActualHours,
        bool IsDeleted,
        DateTimeOffset? UpdatedAt);
}
