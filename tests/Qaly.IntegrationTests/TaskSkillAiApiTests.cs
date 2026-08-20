using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.IntegrationTests;

public sealed class TaskSkillAiApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid DefaultUserId = Guid.Parse("B0000000-0000-0000-0000-000000000000");
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public TaskSkillAiApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-01")]
    [Trait("TestId", "TEST-SKILL-09")]
    [Trait("TestId", "TEST-SKILL-11")]
    public async Task ManualTagThenSelectiveAiConfirm_PersistsProvenanceIdempotencyAndAuditReadBack()
    {
        var seeded = await SeedScopeAsync(_factory.Services, includeCatalog: false);
        var csrf = await GetCsrfTokenAsync(_client);

        var createFrontend = await SendWithCsrfAsync(
            _client,
            HttpMethod.Post,
            $"/api/organizations/{seeded.OrganizationId}/skills",
            new CreateOrganizationSkillDto(
                "  Vue.js  ",
                "Frontend components",
                "Kỹ thuật phần mềm",
                ["Vue", "Vue 3", "vue"],
                "Advanced"),
            csrf);
        createFrontend.StatusCode.Should().Be(HttpStatusCode.Created);
        var frontend = await ReadResultAsync<OrganizationSkillDto>(createFrontend);
        frontend.Name.Should().Be("Vue.js");
        frontend.NormalizedName.Should().Be("vue.js");
        frontend.Category.Should().Be("Kỹ thuật phần mềm");
        frontend.Aliases.Should().BeEquivalentTo("Vue", "Vue 3");
        frontend.DefaultRequiredLevel.Should().Be("Advanced");
        frontend.IsSystemSeed.Should().BeFalse();

        var createBackend = await SendWithCsrfAsync(
            _client,
            HttpMethod.Post,
            $"/api/organizations/{seeded.OrganizationId}/skills",
            new CreateOrganizationSkillDto("ASP.NET Core", "Backend APIs"),
            csrf);
        var backend = await ReadResultAsync<OrganizationSkillDto>(createBackend);

        var duplicate = await SendWithCsrfAsync(
            _client,
            HttpMethod.Post,
            $"/api/organizations/{seeded.OrganizationId}/skills",
            new CreateOrganizationSkillDto("vue.js"),
            csrf);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadEnvelopeAsync<object>(duplicate)).ErrorCode.Should().Be(AiErrorCodes.SkillCatalogConflict);

        var before = await GetResultAsync<TaskSkillsDto>(_client, $"/api/tasks/{seeded.TaskId}/skills");
        before.CanManage.Should().BeTrue();
        before.CanManageCatalog.Should().BeTrue();
        before.Availability.Should().Be("ready");

        var manual = await SendWithCsrfAsync(
            _client,
            HttpMethod.Put,
            $"/api/tasks/{seeded.TaskId}/skills",
            new ReplaceTaskSkillsDto(
                before.TaskRowVersion,
                [new TaskSkillSelectionDto(backend.Id, "Proficient")]),
            csrf);
        manual.StatusCode.Should().Be(HttpStatusCode.OK);
        var manuallyTagged = await ReadResultAsync<TaskSkillsDto>(manual);
        manuallyTagged.Requirements.Should().ContainSingle(item =>
            item.SkillId == backend.Id &&
            item.Provenance == TaskSkillService.ProvenanceManual);

        var enqueue = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            "task-skill-selective-happy-1",
            new TaskSkillSuggestionRequestDto());
        enqueue.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var job = await ReadResultAsync<AiJobCreatedDto>(enqueue);
        var draftId = await CompleteJobWithDraftAsync(
            _factory.Services,
            job.JobId,
            [(frontend.Id, frontend.Name, "Expert"), (backend.Id, backend.Name, "Proficient")]);

        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var edited = draft.WorkingPayload.Deserialize<TaskSkillSuggestionOutputDto>(JsonOptions)! with
        {
            Suggestions =
            [
                draft.WorkingPayload.Deserialize<TaskSkillSuggestionOutputDto>(JsonOptions)!
                    .Suggestions.Single(item => item.SkillId == frontend.Id) with
                    {
                        RequiredLevel = "Proficient"
                    }
            ]
        };
        var editedJson = JsonSerializer.Serialize(edited, JsonOptions);
        var patch = await SendWithCsrfAsync(
            _client,
            HttpMethod.Patch,
            $"/api/ai/drafts/{draftId}",
            new PatchAiDraftDto(editedJson, draft.RowVersion),
            csrf);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await ReadResultAsync<AiDraftDetailDto>(patch);

        const string confirmKey = "task-skill-selective-confirm-1";
        var confirmed = await ConfirmAsync(
            _client,
            draftId,
            editedJson,
            patched.RowVersion,
            confirmKey,
            csrf);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmation = await ReadResultAsync<AiDraftConfirmResultDto>(confirmed);
        confirmation.AppliedSkillCount.Should().Be(1);

        var replay = await ConfirmAsync(
            _client,
            draftId,
            editedJson,
            patched.RowVersion,
            confirmKey,
            csrf);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<AiDraftConfirmResultDto>(replay)).Should().BeEquivalentTo(confirmation);

        var reloaded = await GetResultAsync<TaskSkillsDto>(_client, $"/api/tasks/{seeded.TaskId}/skills");
        reloaded.Requirements.Should().HaveCount(2);
        reloaded.Requirements.Should().Contain(item =>
            item.SkillId == backend.Id &&
            item.Provenance == TaskSkillService.ProvenanceManual);
        reloaded.Requirements.Should().Contain(item =>
            item.SkillId == frontend.Id &&
            item.RequiredLevel == TaskSkillService.LevelProficient &&
            item.Provenance == TaskSkillService.ProvenanceAiConfirmed);

        var reloadedDraft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        reloadedDraft.Status.Should().Be(AiDraftStatuses.Confirmed);
        var listed = await GetResultAsync<IReadOnlyList<AiJobSummaryDto>>(
            _client,
            $"/api/ai/jobs?projectId={seeded.ProjectId}");
        listed.Should().Contain(item =>
            item.JobId == job.JobId &&
            item.ScopeSourceType == "task" &&
            item.ScopeSourceEntityId == seeded.TaskId &&
            item.DraftIds.Contains(draftId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AuditLogs.CountAsync(item =>
            item.EntityId == seeded.TaskId.ToString() &&
            item.Action == "ReplaceTaskSkills")).Should().Be(1);
        (await db.AuditLogs.CountAsync(item =>
            item.EntityId == draftId.ToString() &&
            item.Action == "ConfirmAiDraft")).Should().Be(1);
        (await db.OrganizationSkills.CountAsync(item =>
            item.OrganizationId == seeded.OrganizationId)).Should().Be(2);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-02")]
    public async Task CrossTenantAndViewerMutations_AreHiddenAndForeignSkillsNeverEnterSnapshot()
    {
        var seeded = await SeedScopeAsync(_factory.Services, includeCatalog: true, includeViewer: true);
        var csrf = await GetCsrfTokenAsync(_client);
        var current = await GetResultAsync<TaskSkillsDto>(_client, $"/api/tasks/{seeded.TaskId}/skills");

        var foreignSelection = await SendWithCsrfAsync(
            _client,
            HttpMethod.Put,
            $"/api/tasks/{seeded.TaskId}/skills",
            new ReplaceTaskSkillsDto(
                current.TaskRowVersion,
                [new TaskSkillSelectionDto(seeded.ForeignSkillId, "Expert")]),
            csrf);
        foreignSelection.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadEnvelopeAsync<TaskSkillsDto>(foreignSelection)).ErrorCode
            .Should().Be(AiErrorCodes.SkillSemanticInvalid);

        var hiddenCatalog = await _client.GetAsync(
            $"/api/organizations/{seeded.ForeignOrganizationId}/skills");
        hiddenCatalog.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.Add("X-Test-UserId", seeded.ViewerId!.Value.ToString());
        var viewerRead = await GetResultAsync<TaskSkillsDto>(viewer, $"/api/tasks/{seeded.TaskId}/skills");
        viewerRead.CanManage.Should().BeFalse();
        viewerRead.CanManageCatalog.Should().BeFalse();
        var viewerCsrf = await GetCsrfTokenAsync(viewer);
        var viewerWrite = await SendWithCsrfAsync(
            viewer,
            HttpMethod.Put,
            $"/api/tasks/{seeded.TaskId}/skills",
            new ReplaceTaskSkillsDto(viewerRead.TaskRowVersion, []),
            viewerCsrf);
        viewerWrite.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var viewerAi = await EnqueueAsync(
            viewer,
            seeded.TaskId,
            viewerCsrf,
            "task-skill-viewer-deny-1",
            new TaskSkillSuggestionRequestDto());
        viewerAi.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerAi = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            "task-skill-tenant-snapshot-1",
            new TaskSkillSuggestionRequestDto());
        ownerAi.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(ownerAi);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var job = await db.AiJobs.Include(item => item.Sources)
            .SingleAsync(item => item.Id == created.JobId);
        using var request = JsonDocument.Parse(job.RequestJson);
        var snapshot = JsonSerializer.Deserialize<TaskSkillSuggestionSnapshotDto>(
            request.RootElement.GetProperty("sourceText").GetString()!,
            JsonOptions)!;
        var skillIds = snapshot.Skills.Select(item => item.Id).ToList();
        skillIds.Should().BeEquivalentTo([seeded.FrontendSkillId, seeded.BackendSkillId]);
        skillIds.Should().NotContain(seeded.ForeignSkillId);
        job.Sources.Should().Contain(source =>
            source.SourceType == "skillcatalog" &&
            source.SourceEntityId == seeded.OrganizationId);
        (await db.AiJobs.CountAsync(item =>
            item.RequestedById == seeded.ViewerId &&
            item.JobType == TaskSkillAiContract.JobType)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-04")]
    public async Task EmptyCatalog_ReportsDistinctAvailabilityAndCanonicalEmptySnapshot()
    {
        var seeded = await SeedScopeAsync(_factory.Services, includeCatalog: false);
        var taskSkills = await GetResultAsync<TaskSkillsDto>(_client, $"/api/tasks/{seeded.TaskId}/skills");
        taskSkills.Availability.Should().Be("catalog_empty");
        taskSkills.Requirements.Should().BeEmpty();

        var csrf = await GetCsrfTokenAsync(_client);
        var enqueue = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            "task-skill-empty-catalog-1",
            new TaskSkillSuggestionRequestDto());
        enqueue.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(enqueue);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var job = await db.AiJobs.SingleAsync(item => item.Id == created.JobId);
        using var request = JsonDocument.Parse(job.RequestJson);
        var snapshotJson = request.RootElement.GetProperty("sourceText").GetString()!;
        TaskSkillSuggestionContract.SnapshotIsEmpty(snapshotJson).Should().BeTrue();
        TaskSkillSuggestionContract.TryBuildEmptyResult(
            snapshotJson,
            out var resultJson,
            out var error).Should().BeTrue(error);
        using var result = JsonDocument.Parse(resultJson);
        result.RootElement.GetProperty("dataState").GetString().Should().Be("empty");
        result.RootElement.GetProperty("suggestions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-05")]
    public async Task PrivateTask_IsNotVisibleOrQueueableByUnauthorizedViewer()
    {
        var seeded = await SeedScopeAsync(
            _factory.Services,
            includeCatalog: true,
            includeViewer: true,
            isPrivate: true);
        var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.Add("X-Test-UserId", seeded.ViewerId!.Value.ToString());

        (await viewer.GetAsync($"/api/tasks/{seeded.TaskId}/skills")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        var csrf = await GetCsrfTokenAsync(viewer);
        var enqueue = await EnqueueAsync(
            viewer,
            seeded.TaskId,
            csrf,
            "task-skill-private-viewer-1",
            new TaskSkillSuggestionRequestDto());
        enqueue.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync(item =>
            item.ProjectId == seeded.ProjectId &&
            item.JobType == TaskSkillAiContract.JobType)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-08")]
    public async Task RequestIdempotencyCancelAndSourceChange_UseCanonicalLifecycleAndCacheKey()
    {
        var seeded = await SeedScopeAsync(_factory.Services, includeCatalog: true);
        var csrf = await GetCsrfTokenAsync(_client);
        const string key = "task-skill-idempotency-1";

        var first = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            key,
            new TaskSkillSuggestionRequestDto(Language: "vi"));
        var replay = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            key,
            new TaskSkillSuggestionRequestDto(Language: "vi"));
        var conflict = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            key,
            new TaskSkillSuggestionRequestDto(Language: "en"));

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        replay.StatusCode.Should().Be(HttpStatusCode.Accepted);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var firstJob = await ReadResultAsync<AiJobCreatedDto>(first);
        (await ReadResultAsync<AiJobCreatedDto>(replay)).JobId.Should().Be(firstJob.JobId);
        (await ReadEnvelopeAsync<AiJobCreatedDto>(conflict)).ErrorCode
            .Should().Be(AiErrorCodes.IdempotencyConflict);

        var canceled = await SendWithCsrfAsync(
            _client,
            HttpMethod.Post,
            $"/api/ai/jobs/{firstJob.JobId}/cancel",
            new CancelAiJobDto("No longer needed"),
            csrf);
        canceled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<AiJobDetailDto>(canceled)).Status.Should().Be(AiJobStatuses.Canceled);

        string firstCacheKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            firstCacheKey = (await db.AiJobs.SingleAsync(item => item.Id == firstJob.JobId)).CacheKey;
            var task = await db.TaskItems.SingleAsync(item => item.Id == seeded.TaskId);
            task.Description = "Changed source now explicitly requires frontend accessibility.";
            task.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
            await db.SaveChangesAsync();
        }

        var refreshed = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            "task-skill-idempotency-2",
            new TaskSkillSuggestionRequestDto(Language: "vi"));
        refreshed.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var refreshedJob = await ReadResultAsync<AiJobCreatedDto>(refreshed);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await db.AiJobs.SingleAsync(item => item.Id == refreshedJob.JobId)).CacheKey
                .Should().NotBe(firstCacheKey);
        }
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-09")]
    [Trait("TestId", "TEST-SKILL-10")]
    public async Task ForeignSkillAndStaleCatalogDrafts_CannotConfirmOrCreateTaxonomy()
    {
        var seeded = await SeedScopeAsync(_factory.Services, includeCatalog: true);
        var csrf = await GetCsrfTokenAsync(_client);
        var enqueue = await EnqueueAsync(
            _client,
            seeded.TaskId,
            csrf,
            "task-skill-semantic-confirm-1",
            new TaskSkillSuggestionRequestDto());
        var created = await ReadResultAsync<AiJobCreatedDto>(enqueue);
        var draftId = await CompleteJobWithDraftAsync(
            _factory.Services,
            created.JobId,
            [(seeded.FrontendSkillId, "Vue.js", "Proficient")]);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var valid = draft.WorkingPayload.Deserialize<TaskSkillSuggestionOutputDto>(JsonOptions)!;
        var inventedId = Guid.NewGuid();
        var invented = valid with
        {
            Suggestions =
            [
                valid.Suggestions[0] with
                {
                    SkillId = inventedId,
                    CanonicalName = "Invented by model"
                }
            ]
        };
        var inventedJson = JsonSerializer.Serialize(invented, JsonOptions);
        var patch = await SendWithCsrfAsync(
            _client,
            HttpMethod.Patch,
            $"/api/ai/drafts/{draftId}",
            new PatchAiDraftDto(inventedJson, draft.RowVersion),
            csrf);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await ReadResultAsync<AiDraftDetailDto>(patch);
        var invalidConfirm = await ConfirmAsync(
            _client,
            draftId,
            inventedJson,
            patched.RowVersion,
            "task-skill-invented-confirm-1",
            csrf);
        invalidConfirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadEnvelopeAsync<AiDraftConfirmResultDto>(invalidConfirm)).ErrorCode
            .Should().Be(AiErrorCodes.SkillSemanticInvalid);

        var catalogSkillNotSuggested = valid with
        {
            Suggestions =
            [
                valid.Suggestions[0] with
                {
                    SkillId = seeded.BackendSkillId,
                    CanonicalName = "ASP.NET Core"
                }
            ]
        };
        var catalogSkillNotSuggestedJson = JsonSerializer.Serialize(catalogSkillNotSuggested, JsonOptions);
        var catalogSkillPatch = await SendWithCsrfAsync(
            _client,
            HttpMethod.Patch,
            $"/api/ai/drafts/{draftId}",
            new PatchAiDraftDto(catalogSkillNotSuggestedJson, patched.RowVersion),
            csrf);
        catalogSkillPatch.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalogSkillPatched = await ReadResultAsync<AiDraftDetailDto>(catalogSkillPatch);
        var catalogSkillConfirm = await ConfirmAsync(
            _client,
            draftId,
            catalogSkillNotSuggestedJson,
            catalogSkillPatched.RowVersion,
            "task-skill-not-original-confirm-1",
            csrf);
        catalogSkillConfirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadEnvelopeAsync<AiDraftConfirmResultDto>(catalogSkillConfirm)).ErrorCode
            .Should().Be(AiErrorCodes.SkillSemanticInvalid);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await db.OrganizationSkills.AnyAsync(item => item.Id == inventedId)).Should().BeFalse();
            var catalogSkill = await db.OrganizationSkills.SingleAsync(item =>
                item.Id == seeded.BackendSkillId);
            catalogSkill.Description = "Catalog changed after draft generation.";
            catalogSkill.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(2);
            await db.SaveChangesAsync();
        }

        var stalePayloadJson = JsonSerializer.Serialize(valid, JsonOptions);
        var staleConfirm = await ConfirmAsync(
            _client,
            draftId,
            stalePayloadJson,
            catalogSkillPatched.RowVersion,
            "task-skill-stale-confirm-1",
            csrf);
        staleConfirm.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadEnvelopeAsync<AiDraftConfirmResultDto>(staleConfirm)).ErrorCode
            .Should().Be(AiErrorCodes.SourceStale);
        var requirements = await GetResultAsync<TaskSkillsDto>(_client, $"/api/tasks/{seeded.TaskId}/skills");
        requirements.Requirements.Should().BeEmpty();
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-04")]
    [Trait("TestId", "TEST-SKILL-06")]
    public async Task FeatureDisabled_LeavesManualCatalogAndTaggingAvailable()
    {
        using var disabledFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiJobsV4:TaskSkillSuggestionEnabled"] = "false"
                })));
        var client = disabledFactory.CreateClient();
        var seeded = await SeedScopeAsync(disabledFactory.Services, includeCatalog: true);
        var csrf = await GetCsrfTokenAsync(client);
        var current = await GetResultAsync<TaskSkillsDto>(client, $"/api/tasks/{seeded.TaskId}/skills");
        var manual = await SendWithCsrfAsync(
            client,
            HttpMethod.Put,
            $"/api/tasks/{seeded.TaskId}/skills",
            new ReplaceTaskSkillsDto(
                current.TaskRowVersion,
                [new TaskSkillSelectionDto(seeded.FrontendSkillId, "Familiar")]),
            csrf);
        manual.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<TaskSkillsDto>(manual)).Requirements.Should().ContainSingle();

        var ai = await EnqueueAsync(
            client,
            seeded.TaskId,
            csrf,
            "task-skill-disabled-1",
            new TaskSkillSuggestionRequestDto());
        ai.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ReadEnvelopeAsync<AiJobCreatedDto>(ai)).ErrorCode.Should().Be(AiErrorCodes.PlatformDisabled);
    }

    private static async Task<SeededScope> SeedScopeAsync(
        IServiceProvider services,
        bool includeCatalog,
        bool includeViewer = false,
        bool isPrivate = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        await EnsureUserAsync(db, DefaultUserId, "Task Skill Owner");
        var foreignOwnerId = Guid.NewGuid();
        await EnsureUserAsync(db, foreignOwnerId, "Foreign Owner");
        Guid? viewerId = includeViewer ? Guid.NewGuid() : null;
        if (viewerId.HasValue) await EnsureUserAsync(db, viewerId.Value, "Task Skill Viewer");

        var organization = new Organization
        {
            Name = "Task Skill Organization",
            Code = $"TS-{Guid.NewGuid():N}"[..12],
            OwnerId = DefaultUserId
        };
        var foreignOrganization = new Organization
        {
            Name = "Foreign Skill Organization",
            Code = $"FX-{Guid.NewGuid():N}"[..12],
            OwnerId = foreignOwnerId
        };
        var project = new Project
        {
            Name = "Task Skill Project",
            Code = $"TP-{Guid.NewGuid():N}"[..12],
            OwnerId = DefaultUserId,
            OrganizationId = organization.Id
        };
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = DefaultUserId,
            Title = "Build native Vue task card with ASP.NET Core API",
            Description = "Implement frontend review states and backend authorization.",
            Priority = "High",
            Status = "Todo",
            IsPrivate = isPrivate,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var frontend = new OrganizationSkill
        {
            OrganizationId = organization.Id,
            Name = "Vue.js",
            NormalizedName = "vue.js",
            Description = "Frontend components"
        };
        var backend = new OrganizationSkill
        {
            OrganizationId = organization.Id,
            Name = "ASP.NET Core",
            NormalizedName = "asp.net core",
            Description = "Backend APIs"
        };
        var foreign = new OrganizationSkill
        {
            OrganizationId = foreignOrganization.Id,
            Name = "Foreign Secret Skill",
            NormalizedName = "foreign secret skill",
            Description = "Must never cross tenant boundaries"
        };
        db.AddRange(organization, foreignOrganization, project, task, foreign);
        if (includeCatalog) db.AddRange(frontend, backend);
        if (viewerId.HasValue)
        {
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = viewerId.Value,
                Role = OrganizationRoleRules.Member
            });
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = viewerId.Value,
                Role = ProjectRoleRules.Viewer
            });
        }
        await db.SaveChangesAsync();
        return new SeededScope(
            organization.Id,
            foreignOrganization.Id,
            project.Id,
            task.Id,
            frontend.Id,
            backend.Id,
            foreign.Id,
            viewerId);
    }

    private static async Task<Guid> CompleteJobWithDraftAsync(
        IServiceProvider services,
        Guid jobId,
        IReadOnlyList<(Guid SkillId, string Name, string Level)> suggestions)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var job = await db.AiJobs
            .Include(item => item.Dispatch)
            .SingleAsync(item => item.Id == jobId);
        using var request = JsonDocument.Parse(job.RequestJson);
        var snapshotJson = request.RootElement.GetProperty("sourceText").GetString()!;
        var snapshot = JsonSerializer.Deserialize<TaskSkillSuggestionSnapshotDto>(snapshotJson, JsonOptions)!;
        var model = new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            snapshot.Task.Id,
            snapshot.SourceVersion,
            suggestions.Count == 0 ? "empty" : "ready",
            suggestions.Select(item => new TaskSkillSuggestionItemDto(
                item.SkillId,
                item.Name,
                item.Level,
                0.88m,
                $"The authorized task content requires {item.Name}.",
                [snapshot.Task.SourceRef])).ToList(),
            [],
            DateTimeOffset.UtcNow);
        TaskSkillSuggestionContract.TryBuildResult(
            JsonSerializer.Serialize(model, JsonOptions),
            snapshotJson,
            out var resultJson,
            out var error).Should().BeTrue(error);

        job.Status = AiJobStatuses.Succeeded;
        job.ProgressPercent = 100;
        job.FinishedAt = DateTimeOffset.UtcNow;
        job.ResultJson = resultJson;
        job.ResultHash = Hash(resultJson);
        job.SelectedProvider = "test-provider";
        job.SelectedModel = "task-skill-test-model";
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
            DraftType = TaskSkillAiContract.DraftType,
            PayloadJson = resultJson,
            OriginalPayloadJson = resultJson,
            WorkingPayloadJson = resultJson,
            Status = AiDraftStatuses.PendingReview,
            SchemaId = TaskSkillAiContract.SchemaId,
            SourceHashAtGeneration = job.RequestHash,
            RowVersion = [1, 2, 3]
        };
        db.AiGeneratedDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft.Id;
    }

    private static async Task EnsureUserAsync(QalyDbContext db, Guid userId, string name)
    {
        if (await db.Users.AnyAsync(item => item.Id == userId)) return;
        db.Users.Add(new User
        {
            Id = userId,
            FullName = name,
            Email = $"{userId:N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User"
        });
    }

    private static async Task<HttpResponseMessage> EnqueueAsync(
        HttpClient client,
        Guid taskId,
        string csrf,
        string idempotencyKey,
        TaskSkillSuggestionRequestDto body)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/ai/tasks/{taskId}/skill-suggestions")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid draftId,
        string editedPayload,
        string rowVersion,
        string idempotencyKey,
        string csrf)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/ai/drafts/{draftId}/confirm")
        {
            Content = JsonContent.Create(new ConfirmAiDraftDto(
                editedPayload,
                TaskSkillAiContract.ConfirmAction,
                "Reviewed in integration test",
                rowVersion,
                idempotencyKey))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpClient client,
        HttpMethod method,
        string url,
        T body,
        string csrf)
    {
        var message = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
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
    {
        var payload = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions);
        return payload!.Token;
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record SeededScope(
        Guid OrganizationId,
        Guid ForeignOrganizationId,
        Guid ProjectId,
        Guid TaskId,
        Guid FrontendSkillId,
        Guid BackendSkillId,
        Guid ForeignSkillId,
        Guid? ViewerId);

    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(
        bool IsSuccess,
        T? Data,
        string? Error,
        string? ErrorCode,
        int StatusCode);
}
