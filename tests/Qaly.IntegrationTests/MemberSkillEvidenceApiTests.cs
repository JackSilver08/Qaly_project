using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class MemberSkillEvidenceApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid OwnerId = Guid.Parse("B0000000-0000-0000-0000-000000000000");
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _ownerClient;

    public MemberSkillEvidenceApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _ownerClient = factory.CreateClient();
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-EVIDENCE-01")]
    [Trait("TestId", "TEST-SKILL-EVIDENCE-02")]
    [Trait("TestId", "TEST-SKILL-EVIDENCE-03")]
    [Trait("TestId", "TEST-SKILL-EVIDENCE-04")]
    public async Task ManagerConfirm_ProfileGrounding_RedactionCorrectionAndTenantDeny_AreEnforced()
    {
        var data = await SeedAsync(privateEvidenceTask: true);
        var ownerCsrf = await GetCsrfTokenAsync(_ownerClient);

        var initial = await GetResultAsync<TaskCompletionAttributionsDto>(
            _ownerClient,
            $"/api/tasks/{data.EvidenceTaskId}/completion-contributors");
        initial.CanManage.Should().BeTrue();
        initial.IsEligibleForAttribution.Should().BeTrue();
        initial.Attributions.Should().BeEmpty();

        var confirmResponse = await SendWithCsrfAsync(
            _ownerClient,
            HttpMethod.Put,
            $"/api/tasks/{data.EvidenceTaskId}/completion-contributors",
            new ReplaceTaskCompletionAttributionsDto(
                initial.TaskRowVersion,
                [data.ContributorId],
                Confirmed: true,
                "Reviewed against the completed task"),
            ownerCsrf);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await ReadResultAsync<TaskCompletionAttributionsDto>(confirmResponse);
        confirmed.Attributions.Should().ContainSingle(item =>
            item.ContributorUserId == data.ContributorId &&
            item.Status == TaskCompletionAttribution.Confirmed);

        var ownerProfile = await GetResultAsync<MemberSkillProfileDto>(
            _ownerClient,
            $"/api/organizations/{data.OrganizationId}/members/{data.ContributorId}/skill-evidence");
        ownerProfile.Skills.Should().ContainSingle();
        ownerProfile.Skills[0].VerifiedTaskCount.Should().Be(1);
        ownerProfile.Skills[0].Sources.Should().ContainSingle(source =>
            !source.IsRestricted &&
            source.TaskUrl == $"/projects/{data.ProjectId}/tasks/{data.EvidenceTaskId}");

        using var organizationAdminClient = CreateClient(data.OrganizationAdminId);
        var restrictedProfile = await GetResultAsync<MemberSkillProfileDto>(
            organizationAdminClient,
            $"/api/organizations/{data.OrganizationId}/members/{data.ContributorId}/skill-evidence");
        restrictedProfile.Skills.Should().ContainSingle();
        restrictedProfile.Skills[0].RestrictedTaskCount.Should().Be(1);
        restrictedProfile.Skills[0].Sources.Should().ContainSingle(source =>
            source.IsRestricted && source.TaskTitle == null && source.TaskUrl == null);

        using var contributorClient = CreateClient(data.ContributorId);
        var contributorRead = await GetResultAsync<TaskCompletionAttributionsDto>(
            contributorClient,
            $"/api/tasks/{data.EvidenceTaskId}/completion-contributors");
        contributorRead.CanManage.Should().BeFalse();
        contributorRead.Attributions.Should().ContainSingle(item => item.CanRequestCorrection);

        var contributorCsrf = await GetCsrfTokenAsync(contributorClient);
        var forbiddenConfirm = await SendWithCsrfAsync(
            contributorClient,
            HttpMethod.Put,
            $"/api/tasks/{data.EvidenceTaskId}/completion-contributors",
            new ReplaceTaskCompletionAttributionsDto(
                contributorRead.TaskRowVersion,
                [data.ContributorId],
                Confirmed: true),
            contributorCsrf);
        forbiddenConfirm.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var attribution = contributorRead.Attributions.Single();
        await SetAttributionRowVersionAsync(attribution.Id, [4, 5, 6]);
        contributorRead = await GetResultAsync<TaskCompletionAttributionsDto>(
            contributorClient,
            $"/api/tasks/{data.EvidenceTaskId}/completion-contributors");
        attribution = contributorRead.Attributions.Single();
        var correction = await SendWithCsrfAsync(
            contributorClient,
            HttpMethod.Post,
            $"/api/tasks/{data.EvidenceTaskId}/completion-contributors/{attribution.Id}/correction",
            new RequestCompletionAttributionCorrectionDto(attribution.RowVersion, "Contributor disputes this attribution"),
            contributorCsrf);
        correction.StatusCode.Should().Be(HttpStatusCode.OK);

        var correctedProfile = await GetResultAsync<MemberSkillProfileDto>(
            _ownerClient,
            $"/api/organizations/{data.OrganizationId}/members/{data.ContributorId}/skill-evidence");
        correctedProfile.Skills.Should().BeEmpty();
        correctedProfile.PendingCorrectionCount.Should().Be(1);
        correctedProfile.EmptyState.Should().Contain("không phải đánh giá năng lực thấp");

        using var outsiderClient = CreateClient(data.OutsiderId);
        (await outsiderClient.GetAsync($"/api/tasks/{data.EvidenceTaskId}/completion-contributors"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsiderClient.GetAsync($"/api/organizations/{data.OrganizationId}/members/{data.ContributorId}/skill-evidence"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AuditLogs.CountAsync(item => item.Action == "ConfirmTaskCompletionContributors"))
            .Should().BeGreaterThan(0);
        (await db.AuditLogs.CountAsync(item => item.Action == "RequestTaskCompletionAttributionCorrection"))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("TestId", "TEST-ASSIGNEE-EVIDENCE-01")]
    [Trait("TestId", "TEST-ASSIGNEE-EVIDENCE-02")]
    public async Task AssignmentRecommendation_UsesOnlyConfirmedVisibleEvidenceAndReturnsSourceLinks()
    {
        var data = await SeedAsync(privateEvidenceTask: true, seedConfirmedAttribution: true);
        using var actualAiFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiService>();
                services.AddScoped<IAiService, AiService>();
            }));
        using var ownerClient = actualAiFactory.CreateClient();

        var ownerInsight = await GetResultAsync<TaskAssignmentInsightDto>(
            ownerClient,
            $"/api/ai/tasks/{data.TargetTaskId}/assignment-insight?projectId={data.ProjectId}");
        ownerInsight.EvidenceState.Should().Be("ready");
        ownerInsight.RecommendedUserId.Should().Be(data.ContributorId);
        ownerInsight.ScoringVersion.Should().Be("assignee-evidence-score.v1");
        var candidate = ownerInsight.Candidates.Single(item => item.UserId == data.ContributorId);
        candidate.SkillCoveragePercent.Should().Be(100);
        candidate.EvidenceSources.Should().ContainSingle(source =>
            source.TaskId == data.EvidenceTaskId &&
            source.TaskUrl == $"/projects/{data.ProjectId}/tasks/{data.EvidenceTaskId}");

        using var managerClient = actualAiFactory.CreateClient();
        managerClient.DefaultRequestHeaders.Add("X-Test-UserId", data.ProjectManagerId.ToString());
        var managerInsight = await GetResultAsync<TaskAssignmentInsightDto>(
            managerClient,
            $"/api/ai/tasks/{data.TargetTaskId}/assignment-insight?projectId={data.ProjectId}");
        managerInsight.EvidenceState.Should().Be("ready");
        managerInsight.RecommendedUserId.Should().Be(data.ContributorId);
        managerInsight.Candidates.Single(item => item.UserId == data.ContributorId)
            .EvidenceSources.Should().ContainSingle(source =>
                source.TaskId == data.EvidenceTaskId &&
                source.TaskUrl == $"/projects/{data.ProjectId}/tasks/{data.EvidenceTaskId}");
        managerInsight.RecommendationSummary.Should().Contain("phủ 100% kỹ năng yêu cầu");
    }

    [Theory]
    [InlineData(1, 1, "emerging", 0.50)]
    [InlineData(2, 2, "practiced", 0.75)]
    [InlineData(4, 1, "experienced", 0.95)]
    [Trait("TestId", "TEST-SKILL-EVIDENCE-05")]
    public void DeterministicBandAndConfidence_AreVersionedAndBounded(
        int count,
        int level,
        string band,
        decimal confidence)
    {
        MemberSkillEvidenceService.CalculateEvidenceBand(count, level).Should().Be(band);
        MemberSkillEvidenceService.CalculateConfidence(count, level).Should().Be(confidence);
        MemberSkillEvidenceService.EvidenceMethodVersion.Should().Be("member-skill-evidence.v1");
    }

    private async Task<SeededData> SeedAsync(bool privateEvidenceTask, bool seedConfirmedAttribution = false)
    {
        var contributorId = Guid.NewGuid();
        var organizationAdminId = Guid.NewGuid();
        var projectManagerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        await EnsureUserAsync(db, OwnerId, "Evidence Owner");
        await EnsureUserAsync(db, contributorId, "Verified Contributor");
        await EnsureUserAsync(db, organizationAdminId, "Organization Evidence Admin");
        await EnsureUserAsync(db, projectManagerId, "Project Evidence Manager");
        await EnsureUserAsync(db, outsiderId, "Foreign Outsider");

        var organization = new Organization
        {
            Name = $"Evidence Org {Guid.NewGuid():N}",
            Code = $"EV-{Guid.NewGuid():N}"[..12],
            OwnerId = OwnerId,
            IsActive = true
        };
        var project = new Project
        {
            Name = $"Evidence Project {Guid.NewGuid():N}",
            Code = $"EP-{Guid.NewGuid():N}"[..12],
            OwnerId = OwnerId,
            OrganizationId = organization.Id,
            Status = "Active"
        };
        var skill = new OrganizationSkill
        {
            OrganizationId = organization.Id,
            Name = "Vue Native UI",
            NormalizedName = "vue native ui",
            Description = "Frontend native AI surfaces",
            IsActive = true
        };
        var evidenceTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = OwnerId,
            AssigneeId = contributorId,
            Title = "Ship grounded native UI",
            Description = "A source task that proves confirmed Vue delivery.",
            Priority = "High",
            Status = "Done",
            IsPrivate = privateEvidenceTask,
            RowVersion = [1, 2, 3],
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var targetTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = OwnerId,
            Title = "Build the next Vue native card",
            Description = "This label and description must never create evidence by themselves.",
            Priority = "High",
            Status = "Todo",
            IsPrivate = false,
            RowVersion = [7, 8, 9]
        };
        var evidenceRequirement = Requirement(evidenceTask.Id, skill.Id, OwnerId);
        var targetRequirement = Requirement(targetTask.Id, skill.Id, OwnerId);
        db.AddRange(organization, project, skill, evidenceTask, targetTask, evidenceRequirement, targetRequirement);
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organization.Id, UserId = contributorId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organization.Id, UserId = organizationAdminId, Role = OrganizationRoleRules.OrganizationAdmin },
            new OrganizationMember { OrganizationId = organization.Id, UserId = projectManagerId, Role = OrganizationRoleRules.Member });
        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project.Id, UserId = contributorId, Role = ProjectRoleRules.Member },
            new ProjectMember { ProjectId = project.Id, UserId = projectManagerId, Role = ProjectRoleRules.Manager });
        db.TaskAssignments.Add(new TaskAssignment
        {
            TaskItemId = evidenceTask.Id,
            UserId = contributorId,
            AssignedByUserId = OwnerId
        });
        if (seedConfirmedAttribution)
        {
            db.TaskCompletionAttributions.Add(new TaskCompletionAttribution
            {
                TaskItemId = evidenceTask.Id,
                ContributorUserId = contributorId,
                ConfirmedByUserId = OwnerId,
                CompletedAt = DateTimeOffset.UtcNow.AddDays(-2),
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = TaskCompletionAttribution.Confirmed,
                RowVersion = [10, 11, 12]
            });
        }
        await db.SaveChangesAsync();
        return new SeededData(
            organization.Id,
            project.Id,
            evidenceTask.Id,
            targetTask.Id,
            contributorId,
            organizationAdminId,
            projectManagerId,
            outsiderId);
    }

    private static TaskSkillRequirement Requirement(Guid taskId, Guid skillId, Guid confirmerId)
        => new()
        {
            TaskItemId = taskId,
            OrganizationSkillId = skillId,
            RequiredLevel = TaskSkillService.LevelProficient,
            Provenance = TaskSkillService.ProvenanceManual,
            ConfirmedByUserId = confirmerId,
            ConfirmedAt = DateTimeOffset.UtcNow,
            RowVersion = [1]
        };

    private async Task SetAttributionRowVersionAsync(Guid attributionId, byte[] rowVersion)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var attribution = await db.TaskCompletionAttributions.SingleAsync(item => item.Id == attributionId);
        attribution.RowVersion = rowVersion;
        await db.SaveChangesAsync();
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
        string csrf)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
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
        Guid EvidenceTaskId,
        Guid TargetTaskId,
        Guid ContributorId,
        Guid OrganizationAdminId,
        Guid ProjectManagerId,
        Guid OutsiderId);

    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
