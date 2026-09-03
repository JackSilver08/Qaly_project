using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public class RcSafetyRegressionTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public RcSafetyRegressionTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ModeratorCapability_IsRejected_WhenExpiredOrRevoked()
    {
        var moderatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                NewUser(moderatorId, "moderator-expired@example.test", role: "Moderator"),
                NewUser(adminId, "admin-expired@example.test", role: "Admin"),
                NewUser(ownerId, "owner-expired@example.test", role: "Member"));
            db.Organizations.Add(new Organization { Id = organizationId, Name = "Expired Org", Code = $"exp-{Guid.NewGuid():N}", OwnerId = ownerId, IsActive = true });
            db.ModeratorAssignments.Add(new ModeratorAssignment
            {
                ModeratorUserId = moderatorId,
                OrganizationId = organizationId,
                GrantedByUserId = adminId,
                Capability = "organization.users.view",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                IsActive = false,
                RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", moderatorId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Moderator");

        var response = await client.GetAsync($"/api/organizations/{organizationId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MemberRemoval_RevokesAccessAndBlocksSubsequentAction()
    {
        var memberId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                NewUser(memberId, "member-remove@example.test", role: "Member"),
                NewUser(ownerId, "owner-remove@example.test", role: "Member"));
            db.Organizations.Add(new Organization { Id = organizationId, Name = "Member Org", Code = $"member-{Guid.NewGuid():N}", OwnerId = ownerId, IsActive = true });
            db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = organizationId, UserId = memberId, Role = "Member" });
            db.Projects.Add(new Project { Id = projectId, Name = "Member Project", Code = $"proj-{Guid.NewGuid():N}", OwnerId = ownerId, OrganizationId = organizationId });
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = memberId, Role = "Member" });
            await db.SaveChangesAsync();

            var staleMembership = await db.ProjectMembers.SingleAsync(item => item.ProjectId == projectId && item.UserId == memberId);
            db.ProjectMembers.Remove(staleMembership);
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", memberId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var response = await client.GetAsync($"/api/projects/{projectId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrganizationRevocation_BlocksStaleProjectGroupWikiAiAnalyticsAndMeetingAccess()
    {
        var memberId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                NewUser(memberId, $"member-revoked-{Guid.NewGuid():N}@example.test", "Member"),
                NewUser(ownerId, $"owner-revoked-{Guid.NewGuid():N}@example.test", "Member"));
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Revocation tenant",
                Code = $"revoke-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = memberId,
                Role = OrganizationRoleRules.Member
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Revocation project",
                Code = $"project-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = memberId,
                Role = ProjectRoleRules.Member
            });
            db.TaskItems.Add(new TaskItem
            {
                Id = taskId,
                ProjectId = projectId,
                ReporterId = ownerId,
                Title = "Revoked task"
            });
            db.WikiPages.Add(new WikiPage
            {
                ProjectId = projectId,
                AuthorId = ownerId,
                Title = "Internal",
                Content = "tenant data",
                Visibility = "internal"
            });
            db.WorkGroups.Add(new WorkGroup
            {
                Id = groupId,
                Name = "Revoked group",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            db.WorkGroupMembers.Add(new WorkGroupMember
            {
                WorkGroupId = groupId,
                UserId = memberId,
                Role = "Member"
            });
            db.GroupMeetingSessions.Add(new GroupMeetingSession
            {
                Id = meetingId,
                WorkGroupId = groupId,
                StartedByUserId = ownerId,
                RoomId = "revoked-room"
            });
            await db.SaveChangesAsync();
        }

        using var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("X-Test-UserId", ownerId.ToString());
        ownerClient.DefaultRequestHeaders.Add("X-Test-Role", "Member");
        var csrf = (await ownerClient.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/organizations/{organizationId}/members/{memberId}");
        revokeRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        var revoke = await ownerClient.SendAsync(revokeRequest);
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);

        using var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add("X-Test-UserId", memberId.ToString());
        memberClient.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var responses = await Task.WhenAll(
            memberClient.GetAsync($"/api/projects/{projectId}"),
            memberClient.GetAsync($"/api/tasks/{taskId}"),
            memberClient.GetAsync($"/api/projects/{projectId}/wiki"),
            memberClient.GetAsync($"/api/groups/{groupId}"),
            memberClient.GetAsync($"/api/groups/{groupId}/meetings/active"),
            memberClient.GetAsync($"/api/ai/projects/{projectId}/summary"),
            memberClient.GetAsync($"/api/analytics/projects/{projectId}"));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Forbidden);
    }

    private sealed record CsrfResponse(string Token);

    [Fact]
    public async Task InactiveUser_CannotAccessProtectedEndpoints()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.Add(new User { Id = userId, FullName = "Inactive User", Email = "inactive@example.test", IsActive = false, PasswordHash = "test" });
            db.Projects.Add(new Project { Id = projectId, Name = "Inactive Access Project", Code = $"inactive-{Guid.NewGuid():N}", OwnerId = userId });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var response = await client.GetAsync($"/api/projects/{projectId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static User NewUser(Guid id, string email, string role) => new()
    {
        Id = id,
        FullName = email,
        Email = email,
        PasswordHash = "test",
        Role = role,
        IsActive = true
    };
}
