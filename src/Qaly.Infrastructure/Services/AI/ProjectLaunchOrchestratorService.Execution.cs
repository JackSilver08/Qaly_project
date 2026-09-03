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
            if (string.Equals(replay.Status, "verification_pending", StringComparison.Ordinal))
                await FinalizeCommittedExecutionAsync(replay.Id, ct);
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
        var sprintError = ValidateSprints(delivery, delivery.Sprints);
        if (sprintError != null)
            return Result.Failure<ProjectLaunchPlanDto>(sprintError, 422, "project_launch_sprint_invalid");

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
        foreach (var member in scenario.Members)
        {
            if (!ProjectRoleRules.TryNormalizeAssignableRole(member.ProposedRole, out var normalizedRole) ||
                string.Equals(normalizedRole, ProjectRoleRules.Owner, StringComparison.Ordinal) ||
                (member.UserId == scenario.ManagerUserId && !string.Equals(normalizedRole, ProjectRoleRules.Manager, StringComparison.Ordinal)))
                return Result.Failure<ProjectLaunchPlanDto>(
                    "The reviewed staffing scenario contains an invalid or privileged role.",
                    422,
                    "project_launch_role_invalid");
        }
        var selectedTasks = delivery.Sprints.Where(item => item.Selected)
            .SelectMany(item => item.Tasks.Where(task => task.Selected))
            .ToArray();
        if (selectedTasks.Any(task =>
                task.ProposedAssigneeId.HasValue && !selectedUserIds.Contains(task.ProposedAssigneeId.Value) ||
                task.ProposedReviewerId.HasValue && !selectedUserIds.Contains(task.ProposedReviewerId.Value)))
            return Result.Failure<ProjectLaunchPlanDto>(
                "A reviewed Task references an assignee or reviewer outside the selected team.",
                422,
                "project_launch_assignment_invalid");
        var allocationError = ValidateTaskAllocation(selectedTasks, scenario, delivery.AssignmentMode);
        if (allocationError != null)
            return Result.Failure<ProjectLaunchPlanDto>(allocationError, 422, "project_launch_assignment_capacity_invalid");
        var activeSkills = await _db.OrganizationSkills.AsNoTracking()
            .Where(item => item.OrganizationId == entity.OrganizationId && item.IsActive)
            .ToDictionaryAsync(item => item.Id, ct);
        var requiredSkillIds = selectedTasks.SelectMany(item => item.RequiredSkillIds).Distinct().ToArray();
        if (requiredSkillIds.Any(id => !activeSkills.ContainsKey(id)))
            return Result.Failure<ProjectLaunchPlanDto>(
                "A reviewed Task references an inactive skill or a skill outside this Organization.",
                409,
                "project_launch_skill_stale");

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(ExecuteConfirmedMutationAsync);

        async Task<Result<ProjectLaunchPlanDto>> ExecuteConfirmedMutationAsync()
        {
            IDbContextTransaction? transaction = null;
            var transactionCommitted = false;
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
                        ReviewerId = taskPlan.ProposedReviewerId,
                        ReporterId = userId,
                        ContributesToProgress = true
                    };
                    taskMap[taskPlan.ClientId] = task;
                    _db.TaskItems.Add(task);
                    _db.ProjectLaunchTaskTraces.Add(new ProjectLaunchTaskTrace
                    {
                        TaskItemId = task.Id,
                        ProjectLaunchBriefId = entity.ProjectLaunchBriefId,
                        SprintClientId = sprintPlan.ClientId,
                        TaskClientId = taskPlan.ClientId,
                        FeatureId = taskPlan.FeatureId ?? string.Empty,
                        ObjectiveMetricIdsJson = JsonSerializer.Serialize(taskPlan.ObjectiveMetricIds ?? [], JsonOptions),
                        SourceRefsJson = JsonSerializer.Serialize(taskPlan.SourceRefs, JsonOptions)
                    });
                    if (task.AssigneeId.HasValue)
                        _db.TaskAssignments.Add(new TaskAssignment { TaskItemId = task.Id, UserId = task.AssigneeId.Value, AssignedByUserId = userId });
                    foreach (var skillId in taskPlan.RequiredSkillIds.Distinct())
                    {
                        var skill = activeSkills[skillId];
                        _db.TaskSkillRequirements.Add(new TaskSkillRequirement
                        {
                            TaskItemId = task.Id,
                            OrganizationSkillId = skillId,
                            RequiredLevel = skill.DefaultRequiredLevel,
                            Provenance = "AI_REVIEW_CONFIRMED",
                            ConfirmedByUserId = userId,
                            ConfirmedAt = now
                        });
                    }
                    var checklistRows = taskPlan.AcceptanceCriteria
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => (Text: item.Trim(), Kind: TaskAcceptanceChecklistItem.Acceptance))
                        .Concat(taskPlan.DefinitionOfDone
                            .Where(item => !string.IsNullOrWhiteSpace(item))
                            .Select(item => (Text: item.Trim(), Kind: TaskAcceptanceChecklistItem.DefinitionOfDone)))
                        .Select((item, index) => new TaskAcceptanceChecklistItem
                        {
                            TaskId = task.Id,
                            Text = item.Text,
                            Kind = item.Kind,
                            SortOrder = index,
                            CreatedByUserId = userId,
                            IsCompleted = false
                        });
                    _db.TaskAcceptanceChecklistItems.AddRange(checklistRows);
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
            var receiptId = Guid.NewGuid();
            var receipt = new ProjectLaunchExecutionReceiptDto(
                receiptId,
                AiProjectOrchestrationContract.ExecutionReceiptSchemaId,
                "verification_pending",
                entity.Id,
                project.Id,
                normalizedKey,
                false,
                false,
                commands,
                [$"/projects/{project.Id}", $"/projects/{project.Id}?tab=tasks", $"/projects/{project.Id}?tab=roadmap"],
                delivery.ExternalDeferred,
                false,
                "Đang xác minh lại toàn bộ Project graph sau khi commit.",
                entity.RuleSetId,
                effectiveRuleSet.Version,
                entity.SourceVersionHash,
                entity.ActualProvider,
                entity.ActualModel,
                now,
                null,
                null,
                null,
                1);
            var execution = new ProjectLaunchExecution
            {
                Id = receiptId,
                ProjectLaunchPlanArtifactId = entity.Id,
                OrganizationId = entity.OrganizationId,
                ProjectId = project.Id,
                Status = "verification_pending",
                IdempotencyKey = normalizedKey,
                PayloadHash = payloadHash,
                ReceiptJson = JsonSerializer.Serialize(receipt, JsonOptions),
                ExecutedByUserId = userId,
                ExecutedAt = now,
                VerifiedAt = null,
                MonitoringEnabled = _options.ProjectOperationMonitoringEnabled,
                NextMonitorAt = _options.ProjectOperationMonitoringEnabled ? now.AddHours(6) : null,
                RowRevision = 1
            };
            _db.ProjectLaunchExecutions.Add(execution);
            entity.State = "executing";
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
            transactionCommitted = true;
            await FinalizeCommittedExecutionAsync(execution.Id, ct);
            var organizationName = await _db.Organizations.AsNoTracking().Where(item => item.Id == entity.OrganizationId).Select(item => item.Name).SingleAsync(ct);
            var mapped = await MapPlanAsync(entity, organizationName, ct);
            await UpdateAssistantResponseAsync(entity.AssistantTurnId, mapped, ct);
            return Result.Success(mapped);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transactionCommitted)
                    return await GetPlanAsync(planId, ct);
                if (transaction != null) await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return Result.Failure<ProjectLaunchPlanDto>(
                    "Project launch is already being confirmed. Reload the canonical receipt instead of submitting again.",
                    409,
                    "project_launch_confirmation_in_progress");
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
            {
                if (transactionCommitted)
                    return await GetPlanAsync(planId, ct);
                if (transaction != null) await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return Result.Failure<ProjectLaunchPlanDto>(exception.Message, 409, "project_launch_execution_failed");
            }
            catch (Exception) when (transactionCommitted && !ct.IsCancellationRequested)
            {
                _db.ChangeTracker.Clear();
                return await GetPlanAsync(planId, ct);
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
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
        var projectTasks = await _db.TaskItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId && !item.IsDeleted)
            .Select(item => new
            {
                item.Id,
                item.Status,
                item.ActualHours,
                item.UpdatedAt,
                HasComments = item.Comments.Any(),
                HasCompletionEvidence = item.CompletionAttributions.Any()
            })
            .ToArrayAsync(ct);
        var tracedTaskIds = await _db.ProjectLaunchTaskTraces.AsNoTracking()
            .Where(item => item.TaskItem.ProjectId == execution.ProjectId)
            .Select(item => item.TaskItemId)
            .ToArrayAsync(ct);
        var delivery = JsonSerializer.Deserialize<ProjectLaunchDeliveryPlanDto>(execution.ProjectLaunchPlanArtifact.DeliveryPlanJson, JsonOptions);
        var scenarios = JsonSerializer.Deserialize<ProjectStaffingScenarioDto[]>(execution.ProjectLaunchPlanArtifact.StaffingScenariosJson, JsonOptions) ?? [];
        var scenario = scenarios.SingleOrDefault(item => item.ScenarioId == execution.ProjectLaunchPlanArtifact.SelectedScenarioId);
        var expectedSprintCount = delivery?.Sprints.Count(item => item.Selected) ?? -1;
        var actualSprintCount = await _db.Set<Sprint>().AsNoTracking().CountAsync(item => item.ProjectId == execution.ProjectId, ct);
        var expectedMembers = new Dictionary<Guid, string> { [execution.ExecutedByUserId] = ProjectRoleRules.Owner };
        if (scenario?.ManagerUserId is Guid managerId && managerId != execution.ExecutedByUserId)
            expectedMembers[managerId] = ProjectRoleRules.Manager;
        if (scenario != null)
            foreach (var member in scenario.Members.Where(item => !expectedMembers.ContainsKey(item.UserId)))
                expectedMembers[member.UserId] = ProjectRoleRules.NormalizeProjectRole(member.ProposedRole);
        var actualMembers = await _db.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .Select(item => new { item.UserId, item.Role, item.UpdatedAt })
            .ToArrayAsync(ct);
        var hasRelatedUserData = await _db.WikiPages.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(item => item.ProjectId == execution.ProjectId && !item.IsDeleted, ct) ||
            await _db.ProjectLabels.AsNoTracking().AnyAsync(item => item.ProjectId == execution.ProjectId, ct) ||
            await _db.ProjectCustomRoles.AsNoTracking().AnyAsync(item => item.ProjectId == execution.ProjectId, ct) ||
            await _db.WebhookSubscriptions.AsNoTracking().AnyAsync(item => item.ProjectId == execution.ProjectId, ct) ||
            await _db.ProjectDigestSubscriptions.AsNoTracking().AnyAsync(item => item.ProjectId == execution.ProjectId, ct);
        var hasUserWork = delivery == null || scenario == null ||
            projectTasks.Any(item => !tracedTaskIds.Contains(item.Id) || (item.ActualHours ?? 0) > 0 ||
                item.Status != "Todo" || item.HasComments || item.HasCompletionEvidence || item.UpdatedAt > execution.ExecutedAt) ||
            tracedTaskIds.Length != projectTasks.Length ||
            actualSprintCount != expectedSprintCount ||
            actualMembers.Length != expectedMembers.Count ||
            actualMembers.Any(item => !expectedMembers.TryGetValue(item.UserId, out var role) ||
                !string.Equals(ProjectRoleRules.NormalizeProjectRole(item.Role), role, StringComparison.Ordinal) ||
                item.UpdatedAt > execution.ExecutedAt) ||
            hasRelatedUserData;
        if (hasUserWork)
            return Result.Failure<ProjectLaunchPlanDto>("Rollback is blocked because the launched Project has accrued user work. Use normal archive/replan controls.", 409, "project_launch_rollback_impact_blocked");

        var rollbackExecutionStrategy = _db.Database.CreateExecutionStrategy();
        return await rollbackExecutionStrategy.ExecuteAsync(ExecuteRollbackMutationAsync);

        async Task<Result<ProjectLaunchPlanDto>> ExecuteRollbackMutationAsync()
        {
            IDbContextTransaction? transaction = null;
            var transactionCommitted = false;
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
            transactionCommitted = true;
            var organizationName = await _db.Organizations.AsNoTracking().Where(item => item.Id == execution.OrganizationId).Select(item => item.Name).SingleAsync(ct);
            var mapped = await MapPlanAsync(execution.ProjectLaunchPlanArtifact, organizationName, ct);
            await UpdateAssistantResponseAsync(execution.ProjectLaunchPlanArtifact.AssistantTurnId, mapped, ct);
            return Result.Success(mapped);
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
            {
                if (transactionCommitted)
                    return await GetPlanAsync(execution.ProjectLaunchPlanArtifactId, ct);
                if (transaction != null) await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return Result.Failure<ProjectLaunchPlanDto>(exception.Message, 409, "project_launch_rollback_failed");
            }
            catch (Exception) when (transactionCommitted && !ct.IsCancellationRequested)
            {
                _db.ChangeTracker.Clear();
                return await GetPlanAsync(execution.ProjectLaunchPlanArtifactId, ct);
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
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

    private static string BuildTaskDescription(ProjectLaunchTaskPlanDto task)
        => task.Description;

    private static string? ValidateTaskAllocation(
        IReadOnlyList<ProjectLaunchTaskPlanDto> tasks,
        ProjectStaffingScenarioDto scenario,
        string assignmentMode)
    {
        var members = scenario.Members.ToDictionary(item => item.UserId);
        if (string.Equals(assignmentMode, ProjectLaunchAssignmentModes.AutoBalance, StringComparison.Ordinal) &&
            tasks.Any(item => !item.ProposedAssigneeId.HasValue))
            return "Mọi Task được chọn phải có người thực hiện trước khi tạo Project.";
        if (tasks.Any(item => item.ProposedReviewerId.HasValue && item.ProposedReviewerId == item.ProposedAssigneeId))
            return "Người review phải khác người thực hiện Task.";
        var assignedHours = tasks.Where(item => item.ProposedAssigneeId.HasValue)
            .GroupBy(item => item.ProposedAssigneeId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => (decimal)item.EstimatedHours));
        foreach (var (userId, hours) in assignedHours)
        {
            if (!members.TryGetValue(userId, out var member))
                return "Một Task đang được giao cho người không thuộc đội hình đã review.";
            if (hours > member.ProposedHours)
                return $"Task đã giao cho {member.DisplayName} cần {hours:0.##} giờ, vượt allocation {member.ProposedHours:0.##} giờ đã review.";
        }
        return null;
    }

    private async Task FinalizeCommittedExecutionAsync(Guid executionId, CancellationToken ct)
    {
        var execution = await _db.ProjectLaunchExecutions.IgnoreQueryFilters()
            .Include(item => item.ProjectLaunchPlanArtifact)
            .SingleAsync(item => item.Id == executionId, ct);
        if (!string.Equals(execution.Status, "verification_pending", StringComparison.Ordinal)) return;

        var artifact = execution.ProjectLaunchPlanArtifact;
        var delivery = JsonSerializer.Deserialize<ProjectLaunchDeliveryPlanDto>(artifact.DeliveryPlanJson, JsonOptions)
            ?? throw new InvalidOperationException("The committed launch no longer has a readable delivery plan.");
        var scenarios = JsonSerializer.Deserialize<ProjectStaffingScenarioDto[]>(artifact.StaffingScenariosJson, JsonOptions) ?? [];
        var scenario = scenarios.SingleOrDefault(item => item.ScenarioId == artifact.SelectedScenarioId)
            ?? throw new InvalidOperationException("The committed launch no longer has its reviewed staffing scenario.");
        var receipt = JsonSerializer.Deserialize<ProjectLaunchExecutionReceiptDto>(execution.ReceiptJson, JsonOptions)
            ?? throw new InvalidOperationException("The committed launch receipt is unreadable.");
        var verified = await VerifyExecutionReadBackAsync(execution, artifact, delivery, scenario, ct);
        var now = DateTimeOffset.UtcNow;
        receipt = receipt with
        {
            State = verified ? "executed" : "verification_failed",
            InternalTransactionCommitted = true,
            ReadBackVerified = verified,
            RollbackAvailable = verified,
            RollbackBlockReason = verified ? null : "Dữ liệu đã commit nhưng không khớp đầy đủ với phương án đã review; cần quản trị viên kiểm tra receipt.",
            VerifiedAt = now,
            Revision = receipt.Revision + 1
        };
        execution.Status = receipt.State;
        execution.ReceiptJson = JsonSerializer.Serialize(receipt, JsonOptions);
        execution.VerifiedAt = now;
        execution.RowRevision++;
        artifact.State = receipt.State;
        artifact.UpdatedAt = now;
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "VerifyProjectLaunchExecution",
            EntityType = nameof(ProjectLaunchExecution),
            EntityId = execution.Id.ToString(),
            UserId = execution.ExecutedByUserId,
            ChangesJson = JsonSerializer.Serialize(new { verified, projectId = execution.ProjectId }, JsonOptions)
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> VerifyExecutionReadBackAsync(
        ProjectLaunchExecution execution,
        ProjectLaunchPlanArtifact artifact,
        ProjectLaunchDeliveryPlanDto delivery,
        ProjectStaffingScenarioDto scenario,
        CancellationToken ct)
    {
        var project = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == execution.ProjectId)
            .Select(item => new
            {
                item.Name,
                item.Description,
                item.Status,
                item.StartDate,
                item.EndDate,
                item.OwnerId,
                item.OrganizationId,
                item.IsDeleted
            })
            .SingleOrDefaultAsync(ct);
        if (project == null || project.IsDeleted || project.Name != delivery.ProposedProjectName.Trim() ||
            project.Description != delivery.Objective || project.Status != "Active" ||
            project.StartDate != delivery.StartDate || project.EndDate != delivery.EndDate ||
            project.OwnerId != execution.ExecutedByUserId || project.OrganizationId != artifact.OrganizationId)
            return false;

        var expectedMembers = new Dictionary<Guid, string>
        {
            [execution.ExecutedByUserId] = ProjectRoleRules.Owner
        };
        if (scenario.ManagerUserId.HasValue && scenario.ManagerUserId.Value != execution.ExecutedByUserId)
            expectedMembers[scenario.ManagerUserId.Value] = ProjectRoleRules.Manager;
        foreach (var member in scenario.Members)
        {
            if (expectedMembers.ContainsKey(member.UserId)) continue;
            if (!ProjectRoleRules.TryNormalizeAssignableRole(member.ProposedRole, out var role)) return false;
            expectedMembers[member.UserId] = role;
        }
        var actualMembers = await _db.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .ToDictionaryAsync(item => item.UserId, item => item.Role, ct);
        if (actualMembers.Count != expectedMembers.Count ||
            expectedMembers.Any(item => !actualMembers.TryGetValue(item.Key, out var role) || role != item.Value))
            return false;

        var expectedSprints = delivery.Sprints.Where(item => item.Selected)
            .OrderBy(item => item.StartDate).ThenBy(item => item.EndDate).ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        var actualSprints = await _db.Set<Sprint>().AsNoTracking()
            .Where(item => item.ProjectId == execution.ProjectId)
            .OrderBy(item => item.StartDate).ThenBy(item => item.EndDate).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, item.Goal, item.StartDate, item.EndDate, item.Status })
            .ToArrayAsync(ct);
        if (actualSprints.Length != expectedSprints.Length) return false;
        for (var index = 0; index < expectedSprints.Length; index++)
        {
            var expected = expectedSprints[index];
            var actual = actualSprints[index];
            if (actual.Name != expected.Name || actual.Goal != expected.Objective || actual.StartDate != expected.StartDate ||
                actual.EndDate != expected.EndDate || actual.Status != "Planning") return false;
        }

        var expectedTasks = expectedSprints.SelectMany(item => item.Tasks.Where(task => task.Selected)
                .Select(task => (Sprint: item, Task: task)))
            .ToDictionary(item => item.Task.ClientId, StringComparer.Ordinal);
        var traces = await _db.ProjectLaunchTaskTraces.AsNoTracking()
            .Where(item => item.ProjectLaunchBriefId == artifact.ProjectLaunchBriefId && item.TaskItem.ProjectId == execution.ProjectId)
            .Select(item => new
            {
                item.TaskItemId,
                item.SprintClientId,
                item.TaskClientId,
                item.FeatureId,
                item.ObjectiveMetricIdsJson,
                item.SourceRefsJson,
                item.TaskItem.Title,
                item.TaskItem.Description,
                item.TaskItem.Status,
                item.TaskItem.Priority,
                item.TaskItem.StartDate,
                item.TaskItem.DueDate,
                item.TaskItem.EstimatedHours,
                item.TaskItem.AssigneeId,
                item.TaskItem.ReviewerId,
                item.TaskItem.SprintId,
                item.TaskItem.ReporterId,
                item.TaskItem.IsDeleted
            })
            .ToArrayAsync(ct);
        var actualTaskCount = await _db.TaskItems.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(item => item.ProjectId == execution.ProjectId && !item.IsDeleted, ct);
        if (traces.Length != expectedTasks.Count || actualTaskCount != expectedTasks.Count ||
            traces.Select(item => item.TaskClientId).Distinct(StringComparer.Ordinal).Count() != expectedTasks.Count)
            return false;

        var taskIds = traces.Select(item => item.TaskItemId).ToArray();
        var skillRows = await _db.TaskSkillRequirements.AsNoTracking()
            .Where(item => taskIds.Contains(item.TaskItemId))
            .Select(item => new { item.TaskItemId, item.OrganizationSkillId })
            .ToArrayAsync(ct);
        var assignmentRows = await _db.TaskAssignments.AsNoTracking()
            .Where(item => taskIds.Contains(item.TaskItemId))
            .Select(item => new { item.TaskItemId, item.UserId })
            .ToArrayAsync(ct);
        var checklistRows = await _db.TaskAcceptanceChecklistItems.AsNoTracking()
            .Where(item => taskIds.Contains(item.TaskId))
            .OrderBy(item => item.SortOrder)
            .Select(item => new { item.TaskId, item.Text, item.Kind, item.SortOrder, item.IsCompleted })
            .ToArrayAsync(ct);
        var dependencyRows = await _db.TaskDependencies.AsNoTracking()
            .Where(item => taskIds.Contains(item.SuccessorId) || taskIds.Contains(item.PredecessorId))
            .Select(item => new { item.PredecessorId, item.SuccessorId, item.DependencyType })
            .ToArrayAsync(ct);
        var clientIdByTaskId = traces.ToDictionary(item => item.TaskItemId, item => item.TaskClientId);
        if (dependencyRows.Any(item => !clientIdByTaskId.ContainsKey(item.PredecessorId) || !clientIdByTaskId.ContainsKey(item.SuccessorId)))
            return false;

        foreach (var trace in traces)
        {
            if (!expectedTasks.TryGetValue(trace.TaskClientId, out var tuple)) return false;
            var expectedSprint = tuple.Sprint;
            var expected = tuple.Task;
            var expectedSprintEntity = actualSprints.SingleOrDefault(item => item.Id == trace.SprintId);
            if (expectedSprintEntity == null || trace.SprintClientId != expectedSprint.ClientId ||
                expectedSprintEntity.Name != expectedSprint.Name || expectedSprintEntity.Goal != expectedSprint.Objective ||
                expectedSprintEntity.StartDate != expectedSprint.StartDate || expectedSprintEntity.EndDate != expectedSprint.EndDate ||
                trace.Title != expected.Title || trace.Description != expected.Description || trace.Status != "Todo" ||
                trace.Priority != expected.Priority || trace.StartDate != expectedSprint.StartDate || trace.DueDate != expectedSprint.EndDate ||
                trace.EstimatedHours != expected.EstimatedHours || trace.AssigneeId != expected.ProposedAssigneeId ||
                trace.ReviewerId != expected.ProposedReviewerId || trace.ReporterId != execution.ExecutedByUserId || trace.IsDeleted ||
                trace.FeatureId != (expected.FeatureId ?? string.Empty)) return false;

            var actualSkills = skillRows.Where(item => item.TaskItemId == trace.TaskItemId)
                .Select(item => item.OrganizationSkillId).OrderBy(item => item).ToArray();
            var expectedSkills = expected.RequiredSkillIds.Distinct().OrderBy(item => item).ToArray();
            if (!actualSkills.SequenceEqual(expectedSkills)) return false;
            var actualAssignments = assignmentRows.Where(item => item.TaskItemId == trace.TaskItemId).Select(item => item.UserId).ToArray();
            if (expected.ProposedAssigneeId.HasValue
                    ? actualAssignments.Length != 1 || actualAssignments[0] != expected.ProposedAssigneeId.Value
                    : actualAssignments.Length != 0)
                return false;

            var actualAcceptance = checklistRows.Where(item => item.TaskId == trace.TaskItemId && item.Kind == TaskAcceptanceChecklistItem.Acceptance)
                .Select(item => item.Text).ToArray();
            var actualDefinition = checklistRows.Where(item => item.TaskId == trace.TaskItemId && item.Kind == TaskAcceptanceChecklistItem.DefinitionOfDone)
                .Select(item => item.Text).ToArray();
            if (!actualAcceptance.SequenceEqual(expected.AcceptanceCriteria.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())) ||
                !actualDefinition.SequenceEqual(expected.DefinitionOfDone.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())) ||
                checklistRows.Any(item => item.TaskId == trace.TaskItemId && item.IsCompleted)) return false;

            var actualDependencies = dependencyRows.Where(item => item.SuccessorId == trace.TaskItemId)
                .Select(item => clientIdByTaskId.GetValueOrDefault(item.PredecessorId))
                .Where(item => item != null).Cast<string>().OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var expectedDependencies = expected.DependencyClientIds.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!actualDependencies.SequenceEqual(expectedDependencies, StringComparer.Ordinal) ||
                dependencyRows.Any(item => (item.SuccessorId == trace.TaskItemId || item.PredecessorId == trace.TaskItemId) && item.DependencyType != "FinishToStart"))
                return false;
            if (!TryReadStringArray(trace.ObjectiveMetricIdsJson, out var actualMetricIds) ||
                !TryReadStringArray(trace.SourceRefsJson, out var actualSourceRefs) ||
                !actualMetricIds.OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual((expected.ObjectiveMetricIds ?? []).OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal) ||
                !actualSourceRefs.OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual(expected.SourceRefs.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal))
                return false;
        }
        return true;
    }

    private static string[] ReadStringArray(string json)
    {
        return TryReadStringArray(json, out var values) ? values : [];
    }

    private static bool TryReadStringArray(string json, out string[] values)
    {
        try
        {
            values = JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
            return true;
        }
        catch (JsonException)
        {
            values = [];
            return false;
        }
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
        try
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Session projection is a best-effort read model. Canonical Project data and
            // its verified receipt must never be reported as failed because this sync lags.
            var trackedTurn = _db.ChangeTracker.Entries<AssistantTurn>()
                .SingleOrDefault(item => item.Entity.Id == assistantTurnId);
            if (trackedTurn != null) trackedTurn.State = EntityState.Unchanged;
        }
    }
}
