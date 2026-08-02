using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
