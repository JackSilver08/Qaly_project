using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class PortfolioScheduleApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid OwnerId = Guid.Parse("B0000000-0000-0000-0000-000000000000");
    private readonly IntegrationTestFactory _factory;

    public PortfolioScheduleApiTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task RequestedMemberWithInsufficientCapacity_IsKeptButBlockedWithoutSilentReplacement()
    {
        var data = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.OrganizationMemberCapacityProfiles.Add(new OrganizationMemberCapacityProfile
            {
                OrganizationId = data.OrganizationId,
                UserId = data.ContributorId,
                WeeklyCapacityHours = 0,
                TimeZoneId = "Asia/Ho_Chi_Minh"
            });
            await db.SaveChangesAsync();
        }
        using var manager = CreateClient(data.ManagerId);
        var csrf = await GetCsrfTokenAsync(manager);
        var start = DateTimeOffset.UtcNow.Date;
        var response = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals",
            new CreatePortfolioScheduleProposalDto([data.TargetTaskId], start, start.AddDays(14), "Portfolio Contributor"),
            csrf, new Dictionary<string, string> { ["Idempotency-Key"] = $"requested-{Guid.NewGuid():N}" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var proposal = await ReadResultAsync<PortfolioScheduleProposalDto>(response);
        var item = proposal.Items.Single();
        item.ProposedAssigneeId.Should().Be(data.ContributorId);
        item.BlockingReasons.Should().NotBeEmpty();
        item.Selected.Should().BeFalse();
        item.Alternatives.Should().NotBeEmpty();
        var changedRequest = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals",
            new CreatePortfolioScheduleProposalDto([data.TargetTaskId], start, start.AddDays(14), "Portfolio Manager"),
            csrf, new Dictionary<string, string> { ["Idempotency-Key"] = await ReadJobKeyAsync(proposal.JobId) });
        changedRequest.StatusCode.Should().Be(HttpStatusCode.Conflict, "the same key must not return the old assignee for a different request");
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.TaskItems.AsNoTracking().SingleAsync(task => task.Id == data.TargetTaskId)).AssigneeId.Should().BeNull();
    }

    private async Task<string> ReadJobKeyAsync(Guid jobId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<QalyDbContext>().AiJobs.AsNoTracking()
            .SingleAsync(job => job.Id == jobId)).IdempotencyKey;
    }

    [Fact]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-01")]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-02")]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-03")]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-04")]
    public async Task Manager_CanReviewEditConfirmAndReadBack_WhilePrivatePortfolioLoadIsAggregated()
    {
        var data = await SeedAsync();
        using var manager = CreateClient(data.ManagerId);
        var csrf = await GetCsrfTokenAsync(manager);
        var start = DateTimeOffset.UtcNow.Date;
        var end = start.AddDays(14);

        var capacity = await GetResultAsync<PortfolioCapacityDto>(manager,
            $"/api/projects/{data.ProjectId}/portfolio-capacity?from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(end.ToString("O"))}");
        capacity.VisibilityState.Should().Be("partial_private_aggregate");
        capacity.ScoringVersion.Should().Be(PortfolioScheduleService.ScoringVersion);
        capacity.Members.Should().NotContain(item => item.UserId == data.InactiveMemberId,
            "inactive Organization members must never be treated as staffing capacity");
        capacity.Members.Single(item => item.UserId == data.ContributorId).HasRestrictedLoad.Should().BeTrue();
        capacity.Members.SelectMany(item => item.ProjectLoads).Should().NotContain(item => item.ProjectName == data.PrivateProjectName && !item.SourcesRestricted);

        var profileResponse = await SendWithCsrfAsync(manager, HttpMethod.Put,
            $"/api/organizations/{data.OrganizationId}/members/{data.ContributorId}/capacity",
            new UpdateMemberCapacityProfileDto(30m, "Asia/Ho_Chi_Minh",
            [new MemberAvailabilityWindowDto(null, start.AddDays(2), start.AddDays(3), MemberAvailabilityWindow.Unavailable, null)],
            null, Confirmed: true), csrf);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await ReadResultAsync<PortfolioMemberCapacityDto>(profileResponse);
        profile.WeeklyCapacityHours.Should().Be(30m);
        profile.AvailabilityWindows.Should().ContainSingle();

        const string createKey = "portfolio-create-stable-key";
        var createResponse = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals",
            new CreatePortfolioScheduleProposalDto([data.TargetTaskId], start, end), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = createKey });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposal = await ReadResultAsync<PortfolioScheduleProposalDto>(createResponse);
        proposal.SchemaId.Should().Be(PortfolioScheduleService.SchemaId);
        proposal.Status.Should().Be(AiDraftStatuses.PendingReview);
        proposal.ProviderName.Should().Be("LocalRules");
        proposal.Items.Should().ContainSingle();
        proposal.Items[0].SourceRefs.Should().NotBeEmpty();
        proposal.Sources.Should().OnlyContain(source => source.Url != null || source.Restricted || source.Type == "member_capacity");

        var replayResponse = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals",
            new CreatePortfolioScheduleProposalDto([data.TargetTaskId], start, end), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = createKey });
        (await ReadResultAsync<PortfolioScheduleProposalDto>(replayResponse)).DraftId.Should().Be(proposal.DraftId);

        var item = proposal.Items.Single() with { ProposedDue = proposal.Items.Single().ProposedDue.AddDays(1) };
        var updateResponse = await SendWithCsrfAsync(manager, HttpMethod.Patch,
            $"/api/projects/{data.ProjectId}/schedule-proposals/{proposal.DraftId}",
            new UpdatePortfolioScheduleProposalDto([item], proposal.RowVersion), csrf);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        proposal = await ReadResultAsync<PortfolioScheduleProposalDto>(updateResponse);

        const string confirmKey = "portfolio-confirm-stable-key";
        var confirmRequest = new ConfirmPortfolioScheduleProposalDto(
            [proposal.Items.Single().ItemId], proposal.RowVersion, confirmKey, Confirmed: true);
        var confirmResponse = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals/{proposal.DraftId}/confirm",
            confirmRequest, csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = confirmKey });
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await ReadResultAsync<PortfolioScheduleProposalDto>(confirmResponse);
        confirmed.Status.Should().Be(AiDraftStatuses.Confirmed);
        confirmed.Receipt.Should().NotBeNull();
        confirmed.Receipt!.AppliedTaskIds.Should().Equal(data.TargetTaskId);
        confirmed.Receipt.Status.Should().Be(AiActionReceiptStatuses.Succeeded);
        confirmed.Receipt.ReadBackVerified.Should().BeTrue();
        confirmed.Receipt.VerificationErrors.Should().BeEmpty();

        var confirmReplay = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals/{proposal.DraftId}/confirm",
            confirmRequest, csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = confirmKey });
        (await ReadResultAsync<PortfolioScheduleProposalDto>(confirmReplay)).Receipt!.ExecutionId
            .Should().Be(confirmed.Receipt.ExecutionId);

        var readBack = await GetResultAsync<PortfolioScheduleProposalDto>(manager,
            $"/api/projects/{data.ProjectId}/schedule-proposals/{proposal.DraftId}");
        readBack.Status.Should().Be(AiDraftStatuses.Confirmed);
        readBack.Receipt!.ReadBackLinks.Should().ContainSingle(link => link.Contains(data.TargetTaskId.ToString()));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var task = await db.TaskItems.SingleAsync(value => value.Id == data.TargetTaskId);
        task.AssigneeId.Should().Be(proposal.Items.Single().ProposedAssigneeId);
        task.DueDate.Should().Be(item.ProposedDue);
        (await db.Set<AiUsageLedger>().SingleAsync(value => value.AiJobId == proposal.JobId)).ActualCostUsd.Should().Be(0m);
        (await db.AuditLogs.CountAsync(value => value.Action == "ConfirmPortfolioScheduleProposal")).Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-05")]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-06")]
    public async Task CrossTenantAndStaleSource_FailClosedWithoutMutation()
    {
        var data = await SeedAsync();
        using var outsider = CreateClient(data.OutsiderId);
        (await outsider.GetAsync($"/api/projects/{data.ProjectId}/portfolio-capacity"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var manager = CreateClient(data.ManagerId);
        var csrf = await GetCsrfTokenAsync(manager);
        var start = DateTimeOffset.UtcNow.Date;
        var end = start.AddDays(14);
        var create = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals",
            new CreatePortfolioScheduleProposalDto([data.TargetTaskId], start, end), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = $"stale-{Guid.NewGuid():N}" });
        var proposal = await ReadResultAsync<PortfolioScheduleProposalDto>(create);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var task = await db.TaskItems.SingleAsync(value => value.Id == data.TargetTaskId);
            task.RowVersion = [99, 1, 2];
            await db.SaveChangesAsync();
        }

        var item = proposal.Items.Single();
        var confirm = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals/{proposal.DraftId}/confirm",
            new ConfirmPortfolioScheduleProposalDto([item.ItemId], proposal.RowVersion, "stale-confirm", Confirmed: true), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = "stale-confirm" });
        confirm.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var unchanged = await verifyDb.TaskItems.SingleAsync(value => value.Id == data.TargetTaskId);
        unchanged.AssigneeId.Should().BeNull();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P14-CAPACITY-BLOCK-01")]
    public async Task P14_NoDeclaredCapacityAvailable_ReturnsBlockedAlternativesAndCannotMutate()
    {
        var data = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var profiles = await db.OrganizationMemberCapacityProfiles
                .Where(item => item.OrganizationId == data.OrganizationId)
                .ToListAsync();
            profiles.Should().NotBeEmpty();
            foreach (var profile in profiles) profile.WeeklyCapacityHours = 0m;
            await db.SaveChangesAsync();
        }

        using var manager = CreateClient(data.ManagerId);
        var csrf = await GetCsrfTokenAsync(manager);
        var start = DateTimeOffset.UtcNow.Date;
        var end = start.AddDays(14);
        var create = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals",
            new CreatePortfolioScheduleProposalDto([data.TargetTaskId], start, end), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = $"p14-{Guid.NewGuid():N}" });
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var proposal = await ReadResultAsync<PortfolioScheduleProposalDto>(create);
        var item = proposal.Items.Should().ContainSingle().Subject;
        item.Selected.Should().BeFalse();
        item.BlockingReasons.Should().Contain(reason => reason.Contains("capacity", StringComparison.OrdinalIgnoreCase));
        item.Alternatives.Should().NotBeEmpty();
        proposal.Warnings.Should().Contain(warning => warning.Contains("phương án", StringComparison.OrdinalIgnoreCase));

        var confirmKey = $"p14-confirm-{Guid.NewGuid():N}";
        var confirm = await SendWithCsrfAsync(manager, HttpMethod.Post,
            $"/api/projects/{data.ProjectId}/schedule-proposals/{proposal.DraftId}/confirm",
            new ConfirmPortfolioScheduleProposalDto([item.ItemId], proposal.RowVersion, confirmKey, Confirmed: true), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = confirmKey });
        confirm.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.TaskItems.AsNoTracking().SingleAsync(value => value.Id == data.TargetTaskId))
            .AssigneeId.Should().BeNull();
        (await verifyDb.TaskAssignments.CountAsync(value => value.TaskItemId == data.TargetTaskId)).Should().Be(0);
    }

    private async Task<SeededData> SeedAsync()
    {
        var managerId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var inactiveMemberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        await EnsureUserAsync(db, OwnerId, "Portfolio Owner");
        await EnsureUserAsync(db, managerId, "Portfolio Manager");
        await EnsureUserAsync(db, contributorId, "Portfolio Contributor");
        await EnsureUserAsync(db, inactiveMemberId, "Inactive Portfolio Member");
        await EnsureUserAsync(db, outsiderId, "Portfolio Outsider");
        db.Users.Local.Single(item => item.Id == inactiveMemberId).IsActive = false;

        var organization = new Organization
        {
            Name = $"Portfolio Org {Guid.NewGuid():N}",
            Code = $"PO-{Guid.NewGuid():N}"[..12],
            OwnerId = OwnerId,
            IsActive = true
        };
        var project = new Project
        {
            Name = "Visible planning project",
            Code = $"VP-{Guid.NewGuid():N}"[..12],
            OwnerId = OwnerId,
            OrganizationId = organization.Id,
            Status = "Active"
        };
        var privateProjectName = $"Restricted portfolio project {Guid.NewGuid():N}";
        var privateProject = new Project
        {
            Name = privateProjectName,
            Code = $"RP-{Guid.NewGuid():N}"[..12],
            OwnerId = OwnerId,
            OrganizationId = organization.Id,
            Status = "Active"
        };
        var targetTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = OwnerId,
            Title = "Schedule this authorized task",
            Status = "Todo",
            Priority = "High",
            EstimatedHours = 10,
            RowVersion = [1, 2, 3]
        };
        var privateLoad = new TaskItem
        {
            ProjectId = privateProject.Id,
            ReporterId = OwnerId,
            AssigneeId = contributorId,
            Title = "Never disclose this private title",
            Status = "InProgress",
            Priority = "Medium",
            EstimatedHours = 20,
            IsPrivate = true,
            RowVersion = [4, 5, 6]
        };
        db.AddRange(organization, project, privateProject, targetTask, privateLoad);
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organization.Id, UserId = managerId, Role = OrganizationRoleRules.OrganizationAdmin },
            new OrganizationMember { OrganizationId = organization.Id, UserId = contributorId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organization.Id, UserId = inactiveMemberId, Role = OrganizationRoleRules.Member });
        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project.Id, UserId = managerId, Role = ProjectRoleRules.Manager },
            new ProjectMember { ProjectId = project.Id, UserId = contributorId, Role = ProjectRoleRules.Member },
            new ProjectMember { ProjectId = project.Id, UserId = inactiveMemberId, Role = ProjectRoleRules.Member });
        db.TaskAssignments.Add(new TaskAssignment
        {
            TaskItemId = privateLoad.Id,
            UserId = contributorId,
            AssignedByUserId = OwnerId
        });
        db.OrganizationMemberCapacityProfiles.AddRange(
            new OrganizationMemberCapacityProfile
            {
                OrganizationId = organization.Id,
                UserId = OwnerId,
                WeeklyCapacityHours = 40m,
                TimeZoneId = "Asia/Ho_Chi_Minh"
            },
            new OrganizationMemberCapacityProfile
            {
                OrganizationId = organization.Id,
                UserId = managerId,
                WeeklyCapacityHours = 40m,
                TimeZoneId = "Asia/Ho_Chi_Minh"
            },
            new OrganizationMemberCapacityProfile
            {
                OrganizationId = organization.Id,
                UserId = inactiveMemberId,
                WeeklyCapacityHours = 168m,
                TimeZoneId = "Asia/Ho_Chi_Minh"
            });
        await db.SaveChangesAsync();
        return new SeededData(organization.Id, project.Id, targetTask.Id, contributorId, inactiveMemberId, managerId, outsiderId, privateProjectName);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
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

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpClient client,
        HttpMethod method,
        string url,
        T body,
        string csrf,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (headers != null)
        {
            foreach (var header in headers) request.Headers.Add(header.Key, header.Value);
        }
        return await client.SendAsync(request);
    }

    private static async Task<T> GetResultAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadResultAsync<T>(response);
    }

    private static async Task<T> ReadResultAsync<T>(HttpResponseMessage response)
    {
        var envelope = (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions))!;
        envelope.IsSuccess.Should().BeTrue(envelope.Error);
        envelope.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;

    private sealed record SeededData(
        Guid OrganizationId,
        Guid ProjectId,
        Guid TargetTaskId,
        Guid ContributorId,
        Guid InactiveMemberId,
        Guid ManagerId,
        Guid OutsiderId,
        string PrivateProjectName);

    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
