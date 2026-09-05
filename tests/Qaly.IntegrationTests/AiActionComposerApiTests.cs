using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.IntegrationTests;

public sealed class AiActionComposerApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid DefaultUserId = Guid.Parse("B0000000-0000-0000-0000-000000000000");
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiActionComposerApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task NamedAssigneeAndThreeDeliverables_SurviveConfirmAndCanonicalReloadWithoutDuplication()
    {
        var seeded = await SeedScopeAsync(includeViewer: false);
        var csrf = await GetCsrfTokenAsync(_client);
        var response = await ComposeAsync(_client, seeded.ProjectId, csrf, $"named-{Guid.NewGuid():N}",
            prompt: "Tạo 3 task: Sửa đăng nhập, Viết testcase, Kiểm tra thanh toán; giao cho Action Composer Contributor");
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        var job = await ReadResultAsync<AiJobCreatedDto>(response);
        var draftId = await CompleteActionJobAsync(job.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var plan = draft.WorkingPayload.Deserialize<AiActionPlanDto>(JsonOptions)!;
        var commands = plan.Options[0].Commands;
        commands.Select(command => command.Title).Should().Equal("Sửa đăng nhập", "Viết testcase", "Kiểm tra thanh toán");
        commands.Should().OnlyContain(command => command.AssigneeId != null && command.AssigneeMode == "user_selected");
        commands.Select(command => command.AssigneeId).Distinct().Should().ContainSingle();
        commands.Should().OnlyContain(command => !command.Description!.Contains("giao cho"));
        using (var checkScope = _factory.Services.CreateScope())
        {
            var db = checkScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await db.TaskItems.CountAsync(task => task.ProjectId == seeded.ProjectId)).Should().Be(0);
        }
        var key = $"confirm-named-{Guid.NewGuid():N}";
        var confirm = await ConfirmAsync(_client, draftId, draft.WorkingPayload.GetRawText(), draft.RowVersion, key, csrf);
        confirm.StatusCode.Should().Be(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        var receipt = await ReadResultAsync<AiDraftConfirmResultDto>(confirm);
        receipt.CreatedTaskCount.Should().Be(3);
        receipt.ActionReceipt!.Status.Should().Be("succeeded");
        var retry = await ConfirmAsync(_client, draftId, draft.WorkingPayload.GetRawText(), draft.RowVersion, key, csrf);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<AiDraftConfirmResultDto>(retry)).CreatedTaskIds.Should().BeEquivalentTo(receipt.CreatedTaskIds);
        using var reloadScope = _factory.Services.CreateScope();
        var reloadDb = reloadScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var tasks = await reloadDb.TaskItems.AsNoTracking().Where(task => task.ProjectId == seeded.ProjectId).ToListAsync();
        tasks.Should().HaveCount(3);
        tasks.Select(task => task.Title).Should().BeEquivalentTo(commands.Select(command => command.Title));
        tasks.Should().OnlyContain(task => task.AssigneeId == commands[0].AssigneeId);
        (await reloadDb.TaskAssignments.CountAsync(assignment => receipt.CreatedTaskIds.Contains(assignment.TaskItemId))).Should().Be(3);
    }

    [Theory]
    [InlineData("Tạo 3 task giao cho Action Composer Contributor")]
    [InlineData("Tạo 1 task sửa login; giao cho người xx")]
    public async Task MissingContentOrUnknownRecipient_DoesNotEnqueueOrCreateTasks(string prompt)
    {
        var seeded = await SeedScopeAsync(false);
        var csrf = await GetCsrfTokenAsync(_client);
        var response = await ComposeAsync(_client, seeded.ProjectId, csrf, $"missing-{Guid.NewGuid():N}", prompt: prompt);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync(job => job.ProjectId == seeded.ProjectId)).Should().Be(0);
        (await db.TaskItems.CountAsync(task => task.ProjectId == seeded.ProjectId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-01")]
    [Trait("TestId", "TEST-ACTION-07")]
    [Trait("TestId", "TEST-ACTION-09")]
    [Trait("TestId", "TEST-ACTION-12")]
    public async Task ComposeReviewConfirmReplay_PersistsTasksSkillsReceiptActivityAndAudit()
    {
        var scope = await SeedScopeAsync(includeViewer: false);
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"compose-{Guid.NewGuid():N}");
        compose.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);

        var initialActivity = await GetResultAsync<AiActionActivityFeedDto>(
            _client,
            $"/api/ai/jobs/{created.JobId}/activity");
        initialActivity.Events.Should().Contain(item =>
            item.Stage == AiActionActivityStages.UnderstandIntent &&
            item.Status == AiActionActivityStatuses.Queued);

        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var reviewed = draft.WorkingPayload.Deserialize<AiActionPlanDto>(JsonOptions)!;
        var option = reviewed.Options[0];
        var editedCommand = option.Commands[0] with
        {
            Title = "Reviewed native AI task",
            AssigneeId = DefaultUserId,
            AssigneeMode = "user_selected",
            Priority = "Critical"
        };
        reviewed = reviewed with
        {
            Options = [option with { Commands = [editedCommand] }],
            Review = new AiActionReviewSelectionDto(option.OptionId, [editedCommand.CommandId])
        };
        var reviewedJson = JsonSerializer.Serialize(reviewed, JsonOptions);
        var confirmKey = $"confirm-{Guid.NewGuid():N}";

        var confirmedResponse = await ConfirmAsync(
            _client,
            draftId,
            reviewedJson,
            draft.RowVersion,
            confirmKey,
            csrf);
        confirmedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await ReadResultAsync<AiDraftConfirmResultDto>(confirmedResponse);
        confirmed.CreatedTaskCount.Should().Be(1);
        confirmed.AppliedSkillCount.Should().Be(1);
        confirmed.ActionReceipt.Should().NotBeNull();
        confirmed.ActionReceipt!.Status.Should().Be("succeeded");
        confirmed.ActionReceipt.CommandResults.Should().ContainSingle(item =>
            item.Status == "succeeded" && item.EntityUrl != null);

        var replay = await ConfirmAsync(
            _client,
            draftId,
            reviewedJson,
            draft.RowVersion,
            confirmKey,
            csrf);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<AiDraftConfirmResultDto>(replay)).Should().BeEquivalentTo(confirmed);

        var activity = await GetResultAsync<AiActionActivityFeedDto>(
            _client,
            $"/api/ai/jobs/{created.JobId}/activity");
        activity.Events.Should().Contain(item =>
            item.Stage == AiActionActivityStages.ExecuteCommands &&
            item.Status == AiActionActivityStatuses.Succeeded);
        activity.Events.Should().Contain(item => item.Stage == AiActionActivityStages.PersistReceipt);
        activity.Events.Should().Contain(item => item.Stage == AiActionActivityStages.ReadBack);

        using var dbScope = _factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var taskId = confirmed.CreatedTaskIds.Single();
        var task = await db.TaskItems.AsNoTracking().SingleAsync(item => item.Id == taskId);
        task.Title.Should().Be("Reviewed native AI task");
        task.Priority.Should().Be("Critical");
        task.AssigneeId.Should().Be(DefaultUserId);
        (await db.TaskAssignments.CountAsync(item => item.TaskItemId == taskId)).Should().Be(1);
        (await db.TaskSkillRequirements.CountAsync(item =>
            item.TaskItemId == taskId &&
            item.OrganizationSkillId == scope.SkillId &&
            item.Provenance == TaskSkillService.ProvenanceAiConfirmed)).Should().Be(1);
        (await db.TaskItems.CountAsync(item => item.ProjectId == scope.ProjectId && item.Title == task.Title)).Should().Be(1);
        (await db.AuditLogs.CountAsync(item =>
            item.Action == "ConfirmAiDraft" && item.EntityId == draftId.ToString())).Should().Be(1);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-SPRINT-01")]
    public async Task SprintScopedCompose_ConfirmationPersistsTasksInTheSelectedSprint()
    {
        var scope = await SeedScopeAsync(includeViewer: false);
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"sprint-compose-{Guid.NewGuid():N}",
            scope.SprintId);
        compose.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);
        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");

        var confirmedResponse = await ConfirmAsync(
            _client,
            draftId,
            draft.WorkingPayload.GetRawText(),
            draft.RowVersion,
            $"sprint-confirm-{Guid.NewGuid():N}",
            csrf);
        confirmedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await ReadResultAsync<AiDraftConfirmResultDto>(confirmedResponse);
        confirmed.CreatedTaskCount.Should().Be(1);
        confirmed.ActionReceipt!.ReadBackLinks.Should().Contain(link =>
            link.Contains($"#milestone-{scope.SprintId:D}", StringComparison.Ordinal));

        using var dbScope = _factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var task = await db.TaskItems.AsNoTracking()
            .SingleAsync(item => item.Id == confirmed.CreatedTaskIds.Single());
        task.ProjectId.Should().Be(scope.ProjectId);
        task.SprintId.Should().Be(scope.SprintId);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-COUNT-10")]
    public async Task ExplicitTenTaskRequest_ConfirmPersistsExactlyTenAndReplayCreatesNoDuplicate()
    {
        var scope = await SeedScopeAsync(includeViewer: false);
        using (var catalogScope = _factory.Services.CreateScope())
        {
            var catalogDb = catalogScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var skills = new[]
            {
                ("Business Analysis", "business analysis product discovery"),
                ("UX Design", "ux user flow interface design"),
                ("Backend / .NET APIs", "backend api .net"),
                ("Database / EF Core SQL", "database ef core sql"),
                ("Security / Auth", "security authentication authorization"),
                ("Payments", "payment integration reconciliation"),
                ("QA / Playwright", "qa e2e test playwright"),
                ("DevOps / Operations", "devops operations documentation")
            };
            catalogDb.OrganizationSkills.AddRange(skills.Select(item => new OrganizationSkill
            {
                OrganizationId = scope.OrganizationId,
                Name = item.Item1,
                NormalizedName = item.Item1.ToLowerInvariant(),
                Description = item.Item2,
                IsActive = true
            }));
            await catalogDb.SaveChangesAsync();

            var verifiedSkills = await catalogDb.OrganizationSkills
                .Where(item => item.OrganizationId == scope.OrganizationId && item.IsActive)
                .ToListAsync();
            var evidenceProject = new Project
            {
                OrganizationId = scope.OrganizationId,
                Name = "Action Composer verified skill baseline",
                Code = $"EV-{Guid.NewGuid():N}"[..12],
                OwnerId = DefaultUserId,
                Status = "Archived"
            };
            var evidenceTask = new TaskItem
            {
                ProjectId = evidenceProject.Id,
                ReporterId = DefaultUserId,
                AssigneeId = DefaultUserId,
                Title = "Verified delivery baseline",
                Status = "Done",
                Priority = "Medium",
                EstimatedHours = 8,
                ActualHours = 8,
                DueDate = DateTimeOffset.UtcNow.AddDays(-2)
            };
            catalogDb.AddRange(evidenceProject, evidenceTask);
            catalogDb.TaskSkillRequirements.AddRange(verifiedSkills.Select(skill => new TaskSkillRequirement
            {
                TaskItemId = evidenceTask.Id,
                OrganizationSkillId = skill.Id,
                RequiredLevel = "Proficient",
                Provenance = TaskSkillService.ProvenanceManual,
                ConfirmedByUserId = DefaultUserId,
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2)
            }));
            var eligibleMemberIds = await catalogDb.ProjectMembers
                .Where(item => item.ProjectId == scope.ProjectId)
                .Select(item => item.UserId)
                .ToListAsync();
            catalogDb.TaskCompletionAttributions.AddRange(eligibleMemberIds.Select(memberId => new TaskCompletionAttribution
            {
                TaskItemId = evidenceTask.Id,
                ContributorUserId = memberId,
                ConfirmedByUserId = DefaultUserId,
                CompletedAt = DateTimeOffset.UtcNow.AddDays(-2),
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = TaskCompletionAttribution.Confirmed
            }));
            await catalogDb.SaveChangesAsync();
        }
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"ten-compose-{Guid.NewGuid():N}",
            scope.SprintId,
            "Trong Project đang chọn, soạn đúng 10 Task cho Sprint 1: khảo sát, user flow, UI kit, API contract, database, auth, booking, payment, test E2E và tài liệu vận hành. Mỗi Task có mô tả, acceptance criteria, estimate, dependency, priority và required skill. Mở bản nháp để tôi chỉnh; chưa ghi dữ liệu.");
        compose.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);
        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var plan = draft.WorkingPayload.Deserialize<AiActionPlanDto>(JsonOptions)!;
        plan.Options.Single().Commands.Should().HaveCount(10);
        plan.Review.SelectedCommandIds.Should().HaveCount(10);
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("Khảo sát"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("user flow"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("UI kit"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("API contract"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("database"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("xác thực"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("booking"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("thanh toán"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("E2E"));
        plan.Options.Single().Commands.Select(item => item.Title).Should().Contain(title => title.Contains("vận hành"));
        plan.Options.Single().Commands.Should().OnlyContain(item =>
            !string.IsNullOrWhiteSpace(item.Description) && item.AcceptanceCriteria.Count >= 2 &&
            item.EstimatedHours > 0 && item.RequiredSkills.Count > 0 &&
            item.DueDate.HasValue && item.AssigneeId.HasValue && item.AssigneeMode == "system_suggested");
        plan.Options.Single().Commands.SelectMany(item => item.DependencyCommandIds).Should().NotBeEmpty();

        var confirmKey = $"ten-confirm-{Guid.NewGuid():N}";
        var confirmedResponse = await ConfirmAsync(
            _client,
            draftId,
            draft.WorkingPayload.GetRawText(),
            draft.RowVersion,
            confirmKey,
            csrf);
        confirmedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await ReadResultAsync<AiDraftConfirmResultDto>(confirmedResponse);
        confirmed.CreatedTaskCount.Should().Be(10);
        confirmed.CreatedTaskIds.Should().HaveCount(10).And.OnlyHaveUniqueItems();

        var replay = await ConfirmAsync(
            _client,
            draftId,
            draft.WorkingPayload.GetRawText(),
            draft.RowVersion,
            confirmKey,
            csrf);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<AiDraftConfirmResultDto>(replay)).CreatedTaskIds
            .Should().BeEquivalentTo(confirmed.CreatedTaskIds);

        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(item =>
            item.ProjectId == scope.ProjectId && item.SprintId == scope.SprintId)).Should().Be(10);
        var taskIds = await db.TaskItems.Where(item => item.ProjectId == scope.ProjectId && item.SprintId == scope.SprintId)
            .Select(item => item.Id).ToListAsync();
        (await db.TaskItems.CountAsync(item => taskIds.Contains(item.Id) &&
            item.DueDate != null && item.AssigneeId != null)).Should().Be(10);
        (await db.TaskSkillRequirements.CountAsync(item => taskIds.Contains(item.TaskItemId))).Should().BeGreaterThanOrEqualTo(10);
        (await db.TaskDependencies.CountAsync(item =>
            taskIds.Contains(item.PredecessorId) && taskIds.Contains(item.SuccessorId))).Should().Be(14);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-03")]
    public async Task ViewerAndForeignProject_ComposeAreDeniedWithoutCreatingJobs()
    {
        var scope = await SeedScopeAsync(includeViewer: true);
        var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.Add("X-Test-UserId", scope.ViewerId!.Value.ToString());
        var viewerCsrf = await GetCsrfTokenAsync(viewer);

        var viewerResponse = await ComposeAsync(
            viewer,
            scope.ProjectId,
            viewerCsrf,
            $"viewer-{Guid.NewGuid():N}");
        viewerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var foreignResponse = await ComposeAsync(
            _client,
            Guid.NewGuid(),
            await GetCsrfTokenAsync(_client),
            $"foreign-{Guid.NewGuid():N}");
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var dbScope = _factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync(item => item.ProjectId == scope.ProjectId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-08")]
    public async Task SourceChangesAfterGeneration_ConfirmationFailsClosedWithoutMutation()
    {
        var scope = await SeedScopeAsync(includeViewer: false);
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"stale-compose-{Guid.NewGuid():N}");
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);
        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");

        using (var updateScope = _factory.Services.CreateScope())
        {
            var db = updateScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var project = await db.Projects.SingleAsync(item => item.Id == scope.ProjectId);
            project.Name = $"Changed after generation {Guid.NewGuid():N}";
            project.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
            await db.SaveChangesAsync();
        }

        var response = await ConfirmAsync(
            _client,
            draftId,
            draft.WorkingPayload.GetRawText(),
            draft.RowVersion,
            $"stale-confirm-{Guid.NewGuid():N}",
            csrf);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadEnvelopeAsync<AiDraftConfirmResultDto>(response)).ErrorCode
            .Should().Be(AiErrorCodes.SourceStale);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await assertDb.TaskItems.CountAsync(item => item.ProjectId == scope.ProjectId)).Should().Be(0);
        (await assertDb.AiGeneratedDrafts.SingleAsync(item => item.Id == draftId)).Status
            .Should().Be(AiDraftStatuses.PendingReview);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-ASSIGNEE-STALE-01")]
    public async Task AssigneeRemovedAfterReview_ConfirmationFailsClosedWithoutMutation()
    {
        var scope = await SeedScopeAsync(includeViewer: true);
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"assignee-stale-compose-{Guid.NewGuid():N}");
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);
        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var plan = draft.WorkingPayload.Deserialize<AiActionPlanDto>(JsonOptions)!;
        var option = plan.Options.Single();
        var command = option.Commands.Single() with
        {
            AssigneeId = scope.ViewerId,
            AssigneeMode = "user_selected"
        };
        plan = plan with { Options = [option with { Commands = [command] }] };

        using (var updateScope = _factory.Services.CreateScope())
        {
            var db = updateScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var membership = await db.ProjectMembers.SingleAsync(item =>
                item.ProjectId == scope.ProjectId && item.UserId == scope.ViewerId);
            db.ProjectMembers.Remove(membership);
            await db.SaveChangesAsync();
        }

        var response = await ConfirmAsync(
            _client,
            draftId,
            JsonSerializer.Serialize(plan, JsonOptions),
            draft.RowVersion,
            $"assignee-stale-confirm-{Guid.NewGuid():N}",
            csrf);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadEnvelopeAsync<AiDraftConfirmResultDto>(response)).ErrorCode
            .Should().Be(AiErrorCodes.SourceStale);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await assertDb.TaskItems.CountAsync(item => item.ProjectId == scope.ProjectId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-QUEUE-01")]
    public async Task Compose_WhenWorkerIsDisabled_FailsFastWithoutCreatingAnUnservedJob()
    {
        var seeded = await SeedScopeAsync(includeViewer: false);
        using var optionsScope = _factory.Services.CreateScope();
        var platform = optionsScope.ServiceProvider.GetRequiredService<IOptionsMonitor<AiJobPlatformOptions>>().CurrentValue;
        var previousAllow = platform.AllowEnqueueWhenWorkerDisabled;
        var previousWorker = platform.WorkerEnabled;
        platform.AllowEnqueueWhenWorkerDisabled = false;
        platform.WorkerEnabled = false;
        try
        {
            var response = await ComposeAsync(
                _client,
                seeded.ProjectId,
                await GetCsrfTokenAsync(_client),
                $"worker-disabled-{Guid.NewGuid():N}");

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await ReadEnvelopeAsync<AiJobCreatedDto>(response)).ErrorCode.Should().Be(AiErrorCodes.WorkerPaused);
            using var assertScope = _factory.Services.CreateScope();
            var db = assertScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await db.AiJobs.CountAsync(item => item.ProjectId == seeded.ProjectId)).Should().Be(0);
        }
        finally
        {
            platform.WorkerEnabled = previousWorker;
            platform.AllowEnqueueWhenWorkerDisabled = previousAllow;
        }
    }

    private async Task<SeededScope> SeedScopeAsync(bool includeViewer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        await EnsureUserAsync(db, DefaultUserId, "Action Composer Owner");
        Guid? viewerId = includeViewer ? Guid.NewGuid() : null;
        if (viewerId.HasValue) await EnsureUserAsync(db, viewerId.Value, "Read-only Viewer");

        var organization = new Organization
        {
            Name = $"Action Composer Org {Guid.NewGuid():N}",
            OwnerId = DefaultUserId
        };
        var project = new Project
        {
            Name = $"Action Composer Project {Guid.NewGuid():N}",
            Code = $"AC-{Guid.NewGuid():N}"[..12],
            OwnerId = DefaultUserId,
            OrganizationId = organization.Id,
            Status = "Active",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var skill = new OrganizationSkill
        {
            OrganizationId = organization.Id,
            Name = "Vue.js",
            NormalizedName = "vue.js",
            Description = "Frontend engineering",
            IsActive = true
        };
        var sprint = new Sprint
        {
            ProjectId = project.Id,
            Name = "Sprint 1",
            Status = "Active",
            StartDate = DateTimeOffset.UtcNow.AddDays(-2),
            EndDate = DateTimeOffset.UtcNow.AddDays(12)
        };
        var contributorId = Guid.NewGuid();
        await EnsureUserAsync(db, contributorId, "Action Composer Contributor");
        db.AddRange(
            organization,
            project,
            skill,
            sprint,
            new OrganizationMember { OrganizationId = organization.Id, UserId = DefaultUserId, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organization.Id, UserId = contributorId, Role = OrganizationRoleRules.Member },
            new OrganizationMemberCapacityProfile { OrganizationId = organization.Id, UserId = DefaultUserId, WeeklyCapacityHours = 40, TimeZoneId = "Asia/Ho_Chi_Minh" },
            new OrganizationMemberCapacityProfile { OrganizationId = organization.Id, UserId = contributorId, WeeklyCapacityHours = 40, TimeZoneId = "Asia/Ho_Chi_Minh" },
            new ProjectMember { ProjectId = project.Id, UserId = DefaultUserId, Role = ProjectRoleRules.Manager },
            new ProjectMember { ProjectId = project.Id, UserId = contributorId, Role = ProjectRoleRules.Member });
        if (viewerId.HasValue)
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = viewerId.Value,
                Role = ProjectRoleRules.Viewer
            });
        }
        await db.SaveChangesAsync();
        return new SeededScope(organization.Id, project.Id, sprint.Id, skill.Id, viewerId);
    }

    private async Task<Guid> CompleteActionJobAsync(Guid jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var job = await db.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == jobId);
        using var request = JsonDocument.Parse(job.RequestJson);
        var snapshotJson = request.RootElement.GetProperty("sourceText").GetString()!;
        var snapshot = JsonSerializer.Deserialize<AiActionContextSnapshotDto>(snapshotJson, JsonOptions)!;
        var skill = snapshot.Skills[0];
        var sourceRefs = new List<string> { snapshot.Project.SourceRef, skill.SourceRef };
        if (snapshot.Sprint != null) sourceRefs.Add(snapshot.Sprint.SourceRef);
        var command = new AiActionTaskCommandDto(
            "task-1",
            AiActionComposerContract.TaskCreateTool,
            "1.0",
            "AI proposed task",
            "Draft only until a human confirms.",
            ["Authorized happy path works", "Permission denial is tested"],
            "High",
            DateTimeOffset.UtcNow.AddDays(7),
            8,
            null,
            "unassigned",
            [new AiActionSkillSelectionDto(skill.SkillId, "Proficient")],
            sourceRefs,
            []);
        var model = new AiActionPlanDto(
            AiActionComposerContract.SchemaId,
            "1.0",
            snapshot.Project.Id,
            snapshot.SourceVersion,
            snapshot.UserIntent,
            "task.create",
            0.91m,
            [],
            [],
            [],
            [],
            [new AiActionOptionDto("balanced", "Cân bằng", "Một task có thể duyệt.", [], [command])],
            new AiActionReviewSelectionDto("ignored", []),
            DateTimeOffset.UtcNow);
        string resultJson;
        string? error;
        if (snapshot.RequestedTaskCount is > 1)
        {
            AiActionComposerOutputContract.TryBuildDeterministicFallback(
                snapshotJson,
                out resultJson,
                out error).Should().BeTrue(error);
        }
        else
        {
            AiActionComposerOutputContract.TryBuildResult(
                JsonSerializer.Serialize(model, JsonOptions),
                snapshotJson,
                out resultJson,
                out error).Should().BeTrue(error);
        }

        job.Status = AiJobStatuses.Succeeded;
        job.ProgressPercent = 100;
        job.FinishedAt = DateTimeOffset.UtcNow;
        job.ResultJson = resultJson;
        job.ResultHash = Hash(resultJson);
        job.SelectedProvider = "DeepSeek";
        job.SelectedModel = "deepseek-v4-pro";
        if (job.Dispatch != null)
        {
            job.Dispatch.CompletedAt = DateTimeOffset.UtcNow;
            job.Dispatch.LeaseOwner = null;
            job.Dispatch.LeaseExpiresAt = null;
        }
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = job.ProjectId!.Value,
            DraftType = AiActionComposerContract.DraftType,
            PayloadJson = resultJson,
            OriginalPayloadJson = resultJson,
            WorkingPayloadJson = resultJson,
            Status = AiDraftStatuses.PendingReview,
            SchemaId = AiActionComposerContract.SchemaId,
            SourceHashAtGeneration = job.RequestHash,
            RowVersion = [7, 8, 9]
        };
        db.AiGeneratedDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft.Id;
    }

    private static async Task<HttpResponseMessage> ComposeAsync(
        HttpClient client,
        Guid projectId,
        string csrf,
        string idempotencyKey,
        Guid? sprintId = null,
        string? prompt = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/actions/compose")
        {
            Content = JsonContent.Create(new AiActionComposeRequestDto(
                prompt ?? "Tạo một task frontend có skill và tiêu chí nghiệm thu.",
                new AiActionClientContextDto(
                    sprintId.HasValue ? $"/projects/test#milestone-{sprintId:D}" : "/projects/test",
                    "project_tasks",
                    projectId,
                    sprintId.HasValue ? "sprint" : "project",
                    sprintId ?? projectId)))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid draftId,
        string payload,
        string rowVersion,
        string idempotencyKey,
        string csrf)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/ai/drafts/{draftId}/confirm")
        {
            Content = JsonContent.Create(new ConfirmAiDraftDto(
                payload,
                AiActionComposerContract.ConfirmAction,
                "Reviewed by integration test",
                rowVersion,
                idempotencyKey))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task EnsureUserAsync(QalyDbContext db, Guid userId, string name)
    {
        if (await db.Users.AnyAsync(item => item.Id == userId)) return;
        db.Users.Add(new User
        {
            Id = userId,
            FullName = name,
            Email = $"{userId:N}@action-composer.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task<T> GetResultAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadResultAsync<T>(response);
    }

    private static async Task<T> ReadResultAsync<T>(HttpResponseMessage response)
    {
        var envelope = await ReadEnvelopeAsync<T>(response);
        envelope.IsSuccess.Should().BeTrue(envelope.Error);
        envelope.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    private static async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions))!;

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record SeededScope(Guid OrganizationId, Guid ProjectId, Guid SprintId, Guid SkillId, Guid? ViewerId);
    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
