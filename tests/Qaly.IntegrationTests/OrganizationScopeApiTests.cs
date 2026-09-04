using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class OrganizationScopeApiTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public OrganizationScopeApiTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task ProjectList_ScopedToOrganization_DoesNotReturnAnotherOrganizationsProject()
    {
        var data = await SeedAsync();
        using var client = CreateClient(data.OwnerId);

        var response = await client.GetAsync($"/api/projects?pageSize=100&organizationId={data.OrganizationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<PagedResult<ProjectDto>>>();
        payload!.Data!.Items.Select(item => item.Id).Should().Equal(data.ProjectId);
        payload.Data.Items.Should().NotContain(item => item.Id == data.OtherProjectId);
    }

    [Fact]
    public async Task GroupList_ScopedToOrganization_DoesNotReturnAnotherOrganizationsGroup()
    {
        var data = await SeedAsync();
        using var client = CreateClient(data.OwnerId);

        var response = await client.GetAsync($"/api/groups?pageSize=100&organizationId={data.OrganizationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<PagedResult<GroupDto>>>();
        payload!.Data!.Items.Select(item => item.Id).Should().Equal(data.GroupId);
        payload.Data.Items.Should().NotContain(item => item.Id == data.OtherGroupId);
    }

    [Fact]
    public async Task Outsider_IsBlockedFromOrganizationAndItsScopedResources()
    {
        var data = await SeedAsync();
        using var client = CreateClient(data.OutsiderId);

        (await client.GetAsync($"/api/organizations/{data.OrganizationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/projects?organizationId={data.OrganizationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/groups?organizationId={data.OrganizationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/organizations/{data.OrganizationId}/activity"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/organizations/{data.OrganizationId}/users"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InactiveOrganization_BlocksOverviewProjectsGroupsAndActivity()
    {
        var data = await SeedAsync(organizationActive: false);
        using var client = CreateClient(data.OwnerId);

        (await client.GetAsync($"/api/organizations/{data.OrganizationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/projects?organizationId={data.OrganizationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/groups?organizationId={data.OrganizationId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/organizations/{data.OrganizationId}/activity"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/organizations/{data.OrganizationId}/users"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrganizationMemberDirectory_ReturnsProjectRoleAndCapacityForManager()
    {
        var data = await SeedAsync();
        using var client = CreateClient(data.OwnerId);

        var response = await client.GetAsync($"/api/organizations/{data.OrganizationId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<OrganizationMemberDto>>>();
        var member = payload!.Data!.Single(item => item.UserId == data.MemberId);
        member.Projects.Should().ContainSingle(item => item.ProjectId == data.ProjectId && item.Role == "Member");
        member.WeeklyCapacityHours.Should().Be(32m);
        member.CapacityState.Should().Be("declared");
    }

    [Fact]
    public async Task CreateProjectInOrganization_IsVisibleInOrganizationActivity()
    {
        var data = await SeedAsync();
        using var client = CreateClient(data.OwnerId);
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/projects")
        {
            Content = JsonContent.Create(new CreateProjectDto(
                $"Organization audit project {Guid.NewGuid():N}",
                null,
                null,
                null,
                null,
                null,
                data.OrganizationId))
        };
        createRequest.Headers.Add("X-CSRF-TOKEN", csrf);

        (await client.SendAsync(createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = await client.GetFromJsonAsync<ApiEnvelope<PagedResult<ActivityDto>>>(
            $"/api/organizations/{data.OrganizationId}/activity?pageSize=100");

        activity!.Data!.Items.Should().Contain(item => item.Action == "Create" && item.EntityType == nameof(Project));
    }

    [Fact]
    public async Task OrganizationActivity_ReturnsProjectGroupMemberAndPolicyEventsWithoutOtherTenantData()
    {
        var data = await SeedAsync();
        var foreignEntityId = Guid.NewGuid().ToString();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.AuditLogs.AddRange(
                new AuditLog { Action = "UpdateMemberRole", EntityType = nameof(Organization), EntityId = data.OrganizationId.ToString(), UserId = data.OwnerId },
                new AuditLog { Action = "Create", EntityType = nameof(Project), EntityId = data.ProjectId.ToString(), UserId = data.OwnerId },
                new AuditLog { Action = "Update", EntityType = nameof(WorkGroup), EntityId = data.GroupId.ToString(), UserId = data.OwnerId },
                new AuditLog
                {
                    Action = "UpdateOrganizationSkill",
                    EntityType = nameof(OrganizationSkill),
                    EntityId = Guid.NewGuid().ToString(),
                    UserId = data.OwnerId,
                    ChangesJson = JsonSerializer.Serialize(new { organizationId = data.OrganizationId })
                },
                new AuditLog { Action = "Create", EntityType = nameof(Project), EntityId = foreignEntityId, UserId = data.OwnerId });
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(data.OwnerId);
        var activity = await client.GetFromJsonAsync<ApiEnvelope<PagedResult<ActivityDto>>>(
            $"/api/organizations/{data.OrganizationId}/activity?pageSize=100");

        activity!.Data!.Items.Should().Contain(item => item.Action == "UpdateMemberRole" && item.UserName == "Scope owner");
        activity.Data.Items.Should().Contain(item => item.EntityId == data.ProjectId.ToString());
        activity.Data.Items.Should().Contain(item => item.EntityId == data.GroupId.ToString());
        activity.Data.Items.Should().Contain(item => item.Action == "UpdateOrganizationSkill");
        activity.Data.Items.Should().NotContain(item => item.EntityId == foreignEntityId);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private async Task<SeededScope> SeedAsync(bool organizationActive = true)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        db.Users.AddRange(
            new User { Id = ownerId, FullName = "Scope owner", Email = $"owner-{ownerId:N}@qaly.test", Role = "User", IsActive = true },
            new User { Id = memberId, FullName = "Scope member", Email = $"member-{memberId:N}@qaly.test", Role = "User", IsActive = true },
            new User { Id = outsiderId, FullName = "Scope outsider", Email = $"outsider-{outsiderId:N}@qaly.test", Role = "User", IsActive = true });
        db.Organizations.AddRange(
            new Organization { Id = organizationId, Name = "Scoped organization", Code = $"scope-{organizationId:N}", OwnerId = ownerId, IsActive = organizationActive },
            new Organization { Id = otherOrganizationId, Name = "Other organization", Code = $"other-{otherOrganizationId:N}", OwnerId = ownerId, IsActive = true });
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organizationId, UserId = ownerId, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organizationId, UserId = memberId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = otherOrganizationId, UserId = ownerId, Role = OrganizationRoleRules.Owner });
        db.Projects.AddRange(
            new Project { Id = projectId, Name = "Scoped project", Code = $"PRJ-{projectId:N}", OwnerId = ownerId, OrganizationId = organizationId },
            new Project { Id = otherProjectId, Name = "Other project", Code = $"PRJ-{otherProjectId:N}", OwnerId = ownerId, OrganizationId = otherOrganizationId });
        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = "Owner" },
            new ProjectMember { ProjectId = projectId, UserId = memberId, Role = "Member" },
            new ProjectMember { ProjectId = otherProjectId, UserId = ownerId, Role = "Owner" });
        db.WorkGroups.AddRange(
            new WorkGroup { Id = groupId, Name = "Scoped group", OwnerId = ownerId, OrganizationId = organizationId },
            new WorkGroup { Id = otherGroupId, Name = "Other group", OwnerId = ownerId, OrganizationId = otherOrganizationId });
        db.WorkGroupMembers.AddRange(
            new WorkGroupMember { WorkGroupId = groupId, UserId = ownerId, Role = "Owner" },
            new WorkGroupMember { WorkGroupId = otherGroupId, UserId = ownerId, Role = "Owner" });
        db.OrganizationMemberCapacityProfiles.Add(new OrganizationMemberCapacityProfile
        {
            OrganizationId = organizationId,
            UserId = memberId,
            WeeklyCapacityHours = 32m,
            TimeZoneId = "Asia/Ho_Chi_Minh"
        });
        await db.SaveChangesAsync();
        return new SeededScope(ownerId, memberId, outsiderId, organizationId, projectId, otherProjectId, groupId, otherGroupId);
    }

    private sealed record SeededScope(
        Guid OwnerId,
        Guid MemberId,
        Guid OutsiderId,
        Guid OrganizationId,
        Guid ProjectId,
        Guid OtherProjectId,
        Guid GroupId,
        Guid OtherGroupId);

    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);
    private sealed record ActivityDto(long Id, string Action, string EntityType, string EntityId, string? UserName);
    private sealed record CsrfResponse(string Token);
}
