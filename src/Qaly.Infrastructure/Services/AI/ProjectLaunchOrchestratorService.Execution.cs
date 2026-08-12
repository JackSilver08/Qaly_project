using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class ProjectLaunchOrchestratorService
{
    public async Task<Result<ProjectLaunchPlanDto>> ConfirmAsync(
        Guid planId,
        ConfirmProjectLaunchPlanRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (!_options.ProjectLaunchExecutionEnabled)
            return Result.Failure<ProjectLaunchPlanDto>("Project launch execution is disabled.", 503, "project_launch_execution_disabled");
        if (!request.Confirmed || string.IsNullOrWhiteSpace(idempotencyKey))
            return Result.Failure<ProjectLaunchPlanDto>("Explicit confirmation and Idempotency-Key are required.", 400, "project_launch_confirmation_required");
        if (_currentUser.UserId is not Guid userId)
            return Result.Forbidden<ProjectLaunchPlanDto>();

        var entity = await _db.ProjectLaunchPlanArtifacts
            .SingleOrDefaultAsync(item => item.Id == planId, ct);
        if (entity == null) return Result.NotFound<ProjectLaunchPlanDto>();
        var access = await AuthorizeOrganizationAsync(entity.OrganizationId, manage: true, ct);
        if (!access.IsSuccess) return Result.Failure<ProjectLaunchPlanDto>(access.Error!, access.StatusCode, access.ErrorCode);
        var payloadHash = Hash(JsonSerializer.Serialize(new { planId, request.ExpectedRevision, request.SelectedScenarioId }, JsonOptions));
        var normalizedKey = idempotencyKey.Trim();
        var replay = await _db.ProjectLaunchExecutions.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == normalizedKey, ct);
        if (replay != null)
        {
            if (replay.ProjectLaunchPlanArtifactId != planId || replay.PayloadHash != payloadHash)
                return Result.Failure<ProjectLaunchPlanDto>("The Idempotency-Key belongs to another launch payload.", 409, "idempotency_conflict");
            return await GetPlanAsync(planId, ct);
        }
        if (entity.RowRevision != request.ExpectedRevision)
            return Result.Failure<ProjectLaunchPlanDto>("The launch plan changed; reload before confirmation.", 409, "project_launch_plan_stale");
        if (entity.State != "pending_review")
            return Result.Failure<ProjectLaunchPlanDto>("Only a feasible pending-review plan can be executed.", 409, "project_launch_plan_not_confirmable");
        if (await _db.ProjectLaunchExecutions.IgnoreQueryFilters().AnyAsync(item => item.ProjectLaunchPlanArtifactId == planId, ct))
            return Result.Failure<ProjectLaunchPlanDto>("This launch plan already has an execution receipt.", 409, "project_launch_already_executed");

        var scenarios = JsonSerializer.Deserialize<ProjectStaffingScenarioDto[]>(entity.StaffingScenariosJson, JsonOptions) ?? [];
        var scenario = scenarios.SingleOrDefault(item => item.ScenarioId == request.SelectedScenarioId);
        if (scenario == null || !scenario.Feasible || scenario.ManagerUserId == null || scenario.BlockingReasons.Count > 0)
            return Result.Failure<ProjectLaunchPlanDto>("Select a feasible staffing scenario without blocking decisions.", 422, "project_launch_scenario_blocked");
        var delivery = JsonSerializer.Deserialize<ProjectLaunchDeliveryPlanDto>(entity.DeliveryPlanJson, JsonOptions);
        if (delivery == null || delivery.Sprints.Count == 0 || delivery.Sprints.SelectMany(item => item.Tasks).All(item => !item.Selected))
            return Result.Failure<ProjectLaunchPlanDto>("The reviewed delivery plan has no selected work.", 422, "project_launch_plan_invalid");
        delivery = ReassignDeliveryPlan(delivery, scenario);

        var effectiveRuleSet = await ResolveEffectiveRuleSetAsync(entity.OrganizationId, ct);
        if (!entity.RuleSetId.HasValue || effectiveRuleSet == null || effectiveRuleSet.Id != entity.RuleSetId)
            return Result.Failure<ProjectLaunchPlanDto>("The effective Organization Rulebook changed after planning.", 409, "rulebook_stale");
        var currentSourceHash = await ComputeOrganizationSourceHashAsync(entity.OrganizationId, ct);
        if (!string.Equals(currentSourceHash, entity.SourceVersionHash, StringComparison.Ordinal))
            return Result.Failure<ProjectLaunchPlanDto>("Membership, capacity, workload, availability, skills or source facts changed after planning.", 409, "project_launch_sources_stale");
        var selectedUserIds = scenario.Members.Select(item => item.UserId).Append(scenario.ManagerUserId.Value).Distinct().ToArray();
        var activeMembers = await _db.OrganizationMembers.AsNoTracking()
            .Where(item => item.OrganizationId == entity.OrganizationId && selectedUserIds.Contains(item.UserId) && item.User.IsActive)
            .Select(item => item.UserId)
            .ToArrayAsync(ct);
        var organizationOwnerId = await _db.Organizations.AsNoTracking()
            .Where(item => item.Id == entity.OrganizationId)
            .Select(item => item.OwnerId)
            .SingleAsync(ct);
        if (selectedUserIds.Any(id => id != organizationOwnerId && !activeMembers.Contains(id)))
            return Result.Failure<ProjectLaunchPlanDto>("A selected manager/member is no longer an active Organization member.", 409, "project_launch_member_stale");

        IDbContextTransaction? transaction = null;
        try
        {
            if (_db.Database.IsRelational()) transaction = await _db.Database.BeginTransactionAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var project = new Project
            {
                Name = delivery.ProposedProjectName.Trim(),
                Code = await GenerateUniqueProjectCodeAsync(delivery.ProposedProjectCode, ct),
                Description = delivery.Objective,
                Status = "Active",
                StartDate = delivery.StartDate,
                EndDate = delivery.EndDate,
                OwnerId = userId,
                OrganizationId = entity.OrganizationId
            };
            _db.Projects.Add(project);
            var projectMembers = new Dictionary<Guid, ProjectMember>
            {
                [userId] = BuildProjectMember(project.Id, userId, ProjectRoleRules.Owner)
            };
            if (scenario.ManagerUserId.Value != userId)
                projectMembers[scenario.ManagerUserId.Value] = BuildProjectMember(project.Id, scenario.ManagerUserId.Value, ProjectRoleRules.Manager);
            foreach (var member in scenario.Members)
            {
                if (projectMembers.ContainsKey(member.UserId)) continue;
                projectMembers[member.UserId] = BuildProjectMember(project.Id, member.UserId, member.ProposedRole);
            }
            _db.ProjectMembers.AddRange(projectMembers.Values);

            var sprintMap = new Dictionary<string, Sprint>(StringComparer.Ordinal);
            var taskMap = new Dictionary<string, TaskItem>(StringComparer.Ordinal);
            foreach (var sprintPlan in delivery.Sprints.Where(item => item.Selected))
            {
                var sprint = new Sprint
                {
                    ProjectId = project.Id,
                    Name = sprintPlan.Name,
                    Goal = sprintPlan.Objective,
                    StartDate = sprintPlan.StartDate,
                    EndDate = sprintPlan.EndDate,
                    Status = "Planning"
                };
                sprintMap[sprintPlan.ClientId] = sprint;
                _db.Set<Sprint>().Add(sprint);
                foreach (var taskPlan in sprintPlan.Tasks.Where(item => item.Selected))
                {
                    if (taskMap.ContainsKey(taskPlan.ClientId))
                        return Result.Failure<ProjectLaunchPlanDto>("Duplicate task client ID in reviewed plan.", 422, "project_launch_plan_invalid");
                    var task = new TaskItem
                    {
                        ProjectId = project.Id,
                        SprintId = sprint.Id,
                        Title = taskPlan.Title,
                        Description = BuildTaskDescription(taskPlan),
                        Status = "Todo",
                        Priority = taskPlan.Priority,
                        StartDate = sprintPlan.StartDate,
                        DueDate = sprintPlan.EndDate,
                        EstimatedHours = taskPlan.EstimatedHours,
                        AssigneeId = taskPlan.ProposedAssigneeId,
                        ReporterId = userId,
                        ContributesToProgress = true
                    };
                    taskMap[taskPlan.ClientId] = task;
                    _db.TaskItems.Add(task);
                    if (task.AssigneeId.HasValue)
                        _db.TaskAssignments.Add(new TaskAssignment { TaskItemId = task.Id, UserId = task.AssigneeId.Value, AssignedByUserId = userId });
                    foreach (var skillId in taskPlan.RequiredSkillIds.Distinct())
                    {
                        _db.TaskSkillRequirements.Add(new TaskSkillRequirement
                        {
                            TaskItemId = task.Id,
                            OrganizationSkillId = skillId,
                            RequiredLevel = "Familiar",
                            Provenance = "AI_REVIEW_CONFIRMED",
                            ConfirmedByUserId = userId,
                            ConfirmedAt = now
                        });
                    }
                }
            }
            foreach (var taskPlan in delivery.Sprints.Where(item => item.Selected).SelectMany(item => item.Tasks).Where(item => item.Selected))
            {
                foreach (var predecessorClientId in taskPlan.DependencyClientIds.Distinct(StringComparer.Ordinal))
                {
                    if (!taskMap.TryGetValue(predecessorClientId, out var predecessor) || !taskMap.TryGetValue(taskPlan.ClientId, out var successor))
                        return Result.Failure<ProjectLaunchPlanDto>("A selected task dependency references unselected work.", 422, "project_launch_dependency_invalid");
                    _db.TaskDependencies.Add(new TaskDependency
                    {
                        PredecessorId = predecessor.Id,
                        SuccessorId = successor.Id,
                        DependencyType = "FinishToStart"
                    });
                }
            }

            entity.SelectedScenarioId = scenario.ScenarioId;
            entity.DeliveryPlanJson = JsonSerializer.Serialize(delivery, JsonOptions);
            entity.State = "executing";
            entity.RowRevision++;
            entity.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            var commands = new List<ProjectLaunchCommandReceiptDto>
            {
                new("project", "project.create.v1", "applied", $"Đã tạo Project {project.Name}.", project.Id, $"/projects/{project.Id}"),
                new("members", "project.membership.upsert.v1", "applied", $"Đã tạo {projectMembers.Count} Project memberships.", project.Id, $"/projects/{project.Id}?tab=members"),
                new("roles", "project.role.assign.v1", "applied", $"Đã gán manager {scenario.ManagerName} theo phương án đã review.", scenario.ManagerUserId, $"/projects/{project.Id}?tab=members"),
                new("sprints", "milestone.create.v1/canonical-sprint", "applied", $"Đã tạo {sprintMap.Count} Sprints theo entity Sprint hiện hữu.", project.Id, $"/projects/{project.Id}"),
                new("tasks", "task.create.v1", "applied", $"Đã tạo {taskMap.Count} Tasks đã chọn.", project.Id, $"/projects/{project.Id}?tab=tasks"),
                new("dependencies", "task.dependency.set.v1", "applied", "Đã áp dụng dependency graph đã kiểm tra acyclic.", project.Id, $"/projects/{project.Id}?tab=roadmap"),
                new("external", "external.adapters", "external_deferred", "Repository, webhook, calendar và deployment chưa được gọi; cần credential/scope/receipt riêng.")
            };
            var readBack = await VerifyExecutionReadBackAsync(project.Id, projectMembers.Count, sprintMap.Count, taskMap.Count, ct);
            if (!readBack)
                throw new InvalidOperationException("Project launch read-back did not match the reviewed command set.");
            var receiptId = Guid.NewGuid();
            var receipt = new ProjectLaunchExecutionReceiptDto(
                receiptId,
                AiProjectOrchestrationContract.ExecutionReceiptSchemaId,
                "executed",
                entity.Id,
                project.Id,
                normalizedKey,
                true,
                true,
                commands,
                [$"/projects/{project.Id}", $"/projects/{project.Id}?tab=tasks", $"/projects/{project.Id}?tab=roadmap"],
                delivery.ExternalDeferred,
                true,
                null,
                entity.RuleSetId,
                effectiveRuleSet.Version,
                entity.SourceVersionHash,
                entity.ActualProvider,
                entity.ActualModel,
                now,
                DateTimeOffset.UtcNow,
                null,
                null,
                1);
            var execution = new ProjectLaunchExecution
            {
                Id = receiptId,
                ProjectLaunchPlanArtifactId = entity.Id,
                OrganizationId = entity.OrganizationId,
                ProjectId = project.Id,
                Status = "executed",
                IdempotencyKey = normalizedKey,
                PayloadHash = payloadHash,
                ReceiptJson = JsonSerializer.Serialize(receipt, JsonOptions),
                ExecutedByUserId = userId,
                ExecutedAt = now,
                VerifiedAt = receipt.VerifiedAt,
                MonitoringEnabled = _options.ProjectOperationMonitoringEnabled,
                NextMonitorAt = _options.ProjectOperationMonitoringEnabled ? now.AddHours(6) : null,
                RowRevision = 1
            };
            _db.ProjectLaunchExecutions.Add(execution);
            entity.State = "executed";
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "ConfirmProjectLaunchPlan",
                EntityType = nameof(ProjectLaunchPlanArtifact),
                EntityId = entity.Id.ToString(),
                UserId = userId,
                ChangesJson = JsonSerializer.Serialize(new { project.Id, scenario.ScenarioId, sprintCount = sprintMap.Count, taskCount = taskMap.Count }, JsonOptions)
            });
            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            var organizationName = await _db.Organizations.AsNoTracking().Where(item => item.Id == entity.OrganizationId).Select(item => item.Name).SingleAsync(ct);
            var mapped = await MapPlanAsync(entity, organizationName, ct);
            await UpdateAssistantResponseAsync(entity.AssistantTurnId, mapped, ct);
            return Result.Success(mapped);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            if (transaction != null) await transaction.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return Result.Failure<ProjectLaunchPlanDto>(exception.Message, 409, "project_launch_execution_failed");
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<Result<ProjectLaunchPlanDto>> RollbackAsync(
        Guid executionId,
        RollbackProjectLaunchExecutionRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (!_options.ProjectLaunchExecutionEnabled)
            return Result.Failure<ProjectLaunchPlanDto>("Project launch execution is disabled.", 503, "project_launch_execution_disabled");
        if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5 || string.IsNullOrWhiteSpace(idempotencyKey))
            return Result.Failure<ProjectLaunchPlanDto>("Rollback requires explicit confirmation, Idempotency-Key and a reason.", 400, "project_launch_rollback_confirmation_required");
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<ProjectLaunchPlanDto>();
        var execution = await _db.ProjectLaunchExecutions.IgnoreQueryFilters().Include(item => item.ProjectLaunchPlanArtifact)
            .SingleOrDefaultAsync(item => item.Id == executionId, ct);
        if (execution == null) return Result.NotFound<ProjectLaunchPlanDto>();
        var access = await AuthorizeOrganizationAsync(execution.OrganizationId, manage: true, ct);
        if (!access.IsSuccess) return Result.Failure<ProjectLaunchPlanDto>(access.Error!, access.StatusCode, access.ErrorCode);
        var payloadHash = Hash(JsonSerializer.Serialize(new { executionId, request.ExpectedRevision, reason = request.Reason.Trim() }, JsonOptions));
        var normalizedKey = idempotencyKey.Trim();
        if (execution.Status == "rolled_back")
        {
            if (execution.RollbackIdempotencyKey != normalizedKey || execution.RollbackPayloadHash != payloadHash)
                return Result.Failure<ProjectLaunchPlanDto>("Rollback was already completed with another payload.", 409, "idempotency_conflict");
            return await GetPlanAsync(execution.ProjectLaunchPlanArtifactId, ct);
        }
        if (execution.RowRevision != request.ExpectedRevision)
            return Result.Failure<ProjectLaunchPlanDto>("Execution receipt changed; reload before rollback.", 409, "project_launch_execution_stale");
        var hasUserWork = await _db.TaskItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .AnyAsync(item => (item.ActualHours ?? 0) > 0 || item.Status != "Todo" || item.Comments.Any() || item.CompletionAttributions.Any(), ct);
        if (hasUserWork)
            return Result.Failure<ProjectLaunchPlanDto>("Rollback is blocked because the launched Project has accrued user work. Use normal archive/replan controls.", 409, "project_launch_rollback_impact_blocked");

        IDbContextTransaction? transaction = null;
        try
        {
            if (_db.Database.IsRelational()) transaction = await _db.Database.BeginTransactionAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var project = await _db.Projects.IgnoreQueryFilters().SingleAsync(item => item.Id == execution.ProjectId, ct);
            project.IsDeleted = true;
            project.DeletedAt = now;
            project.UpdatedAt = now;
            var tasks = await _db.TaskItems.IgnoreQueryFilters().Where(item => item.ProjectId == project.Id).ToListAsync(ct);
            foreach (var task in tasks)
            {
                task.IsDeleted = true;
                task.DeletedAt = now;
                task.UpdatedAt = now;
            }
            execution.Status = "rolled_back";
            execution.RolledBackAt = now;
            execution.RollbackReason = request.Reason.Trim();
            execution.RollbackIdempotencyKey = normalizedKey;
            execution.RollbackPayloadHash = payloadHash;
            execution.MonitoringEnabled = false;
            execution.NextMonitorAt = null;
            execution.RowRevision++;
            execution.UpdatedAt = now;
            execution.ProjectLaunchPlanArtifact.State = "rolled_back";
            execution.ProjectLaunchPlanArtifact.RowRevision++;
            execution.ProjectLaunchPlanArtifact.UpdatedAt = now;
            var previous = JsonSerializer.Deserialize<ProjectLaunchExecutionReceiptDto>(execution.ReceiptJson, JsonOptions)
                ?? throw new InvalidOperationException("Execution receipt is invalid.");
            var updated = previous with
            {
                State = "rolled_back",
                RollbackAvailable = false,
                RolledBackAt = now,
                RollbackReason = request.Reason.Trim(),
                Revision = execution.RowRevision
            };
            execution.ReceiptJson = JsonSerializer.Serialize(updated, JsonOptions);
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "RollbackProjectLaunchExecution",
                EntityType = nameof(ProjectLaunchExecution),
                EntityId = execution.Id.ToString(),
                UserId = userId,
                ChangesJson = JsonSerializer.Serialize(new { project.Id, request.Reason }, JsonOptions)
            });
            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            var organizationName = await _db.Organizations.AsNoTracking().Where(item => item.Id == execution.OrganizationId).Select(item => item.Name).SingleAsync(ct);
            var mapped = await MapPlanAsync(execution.ProjectLaunchPlanArtifact, organizationName, ct);
            await UpdateAssistantResponseAsync(execution.ProjectLaunchPlanArtifact.AssistantTurnId, mapped, ct);
            return Result.Success(mapped);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            if (transaction != null) await transaction.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return Result.Failure<ProjectLaunchPlanDto>(exception.Message, 409, "project_launch_rollback_failed");
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private static ProjectMember BuildProjectMember(Guid projectId, Guid userId, string role)
    {
        var normalizedRole = ProjectRoleRules.NormalizeProjectRole(role);
        var canManage = ProjectRoleRules.CanManageProject(normalizedRole);
        return new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = normalizedRole,
            CanViewProjectTimeline = canManage,
            CanViewTaskRisk = canManage,
            CanNudgeAssignee = canManage,
            CanViewUnseenTaskSignal = canManage
        };
    }

    private static ProjectLaunchDeliveryPlanDto ReassignDeliveryPlan(ProjectLaunchDeliveryPlanDto delivery, ProjectStaffingScenarioDto scenario)
    {
        var members = scenario.Members.ToArray();
        var sprints = delivery.Sprints.Select(sprint => sprint with
        {
            Tasks = sprint.Tasks.Select(task =>
            {
                var assignee = members.FirstOrDefault(member => task.RequiredSkillNames.Count == 0 ||
                    member.CoveredSkills.Any(skill => task.RequiredSkillNames.Any(required => Normalize(required) == Normalize(skill))));
                var reviewer = members.FirstOrDefault(member => member.UserId != assignee?.UserId);
                return task with { ProposedAssigneeId = assignee?.UserId, ProposedReviewerId = reviewer?.UserId };
            }).ToArray()
        }).ToArray();
        return delivery with { Sprints = sprints };
    }

    private static string BuildTaskDescription(ProjectLaunchTaskPlanDto task)
        => $"{task.Description}\n\nAcceptance criteria:\n- {string.Join("\n- ", task.AcceptanceCriteria)}\n\nDefinition of Done:\n- {string.Join("\n- ", task.DefinitionOfDone)}";

    private async Task<bool> VerifyExecutionReadBackAsync(Guid projectId, int memberCount, int sprintCount, int taskCount, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AsNoTracking().AnyAsync(item => item.Id == projectId && !item.IsDeleted, ct);
        var actualMembers = await _db.ProjectMembers.AsNoTracking().CountAsync(item => item.ProjectId == projectId, ct);
        var actualSprints = await _db.Set<Sprint>().AsNoTracking().CountAsync(item => item.ProjectId == projectId, ct);
        var actualTasks = await _db.TaskItems.AsNoTracking().CountAsync(item => item.ProjectId == projectId && !item.IsDeleted, ct);
        return projectExists && actualMembers == memberCount && actualSprints == sprintCount && actualTasks == taskCount;
    }

    private async Task<string> GenerateUniqueProjectCodeAsync(string proposed, CancellationToken ct)
    {
        var root = BuildProjectCode(proposed);
        var candidate = root;
        for (var suffix = 2; suffix < 1000; suffix++)
        {
            if (!await _db.Projects.IgnoreQueryFilters().AsNoTracking().AnyAsync(item => item.Code == candidate, ct)) return candidate;
            candidate = $"{root[..Math.Min(root.Length, 6)]}{suffix}";
        }
        return $"PRJ{Guid.NewGuid():N}"[..10].ToUpperInvariant();
    }

    private async Task<OrganizationWorkRuleSet?> ResolveEffectiveRuleSetAsync(Guid organizationId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.OrganizationWorkRuleSets.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.Status == "active" &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= now) &&
                (!item.EffectiveUntil.HasValue || item.EffectiveUntil > now))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(ct);
    }

    private async Task UpdateAssistantResponseAsync(Guid assistantTurnId, ProjectLaunchPlanDto plan, CancellationToken ct)
    {
        var turn = await _db.AssistantTurns.SingleOrDefaultAsync(item => item.Id == assistantTurnId, ct);
        if (turn == null || string.IsNullOrWhiteSpace(turn.ResponseJson)) return;
        AiAssistantTurnResponseDto? response;
        try { response = JsonSerializer.Deserialize<AiAssistantTurnResponseDto>(turn.ResponseJson, JsonOptions); }
        catch (JsonException) { return; }
        if (response == null) return;
        turn.ResponseJson = JsonSerializer.Serialize(response with { ProjectLaunchPlan = plan, ProcessEvents = null }, JsonOptions);
        turn.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
