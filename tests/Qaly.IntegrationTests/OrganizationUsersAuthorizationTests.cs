using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public class OrganizationUsersAuthorizationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public OrganizationUsersAuthorizationTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task OrganizationUsers_RejectsMemberFromAnotherOrganization()
    {
        var actorId = Guid.NewGuid();
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        var organizationA = new Organization { Name = "Tenant A", Code = $"tenant-a-{Guid.NewGuid():N}", OwnerId = ownerAId };
        var organizationB = new Organization { Name = "Tenant B", Code = $"tenant-b-{Guid.NewGuid():N}", OwnerId = ownerBId };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                NewUser(actorId, "actor@example.test"),
                NewUser(ownerAId, "owner-a@example.test"),
                NewUser(ownerBId, "owner-b@example.test"));
            db.Organizations.AddRange(organizationA, organizationB);
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationA.Id,
                UserId = actorId,
                Role = "Member"
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", actorId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var response = await client.GetAsync($"/api/organizations/{organizationB.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrganizationUsers_AllowsModeratorWithActiveViewCapabilityOnlyForAssignedOrganization()
    {
        var moderatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assignedOrganization = new Organization { Name = "Assigned tenant", Code = $"assigned-{Guid.NewGuid():N}", OwnerId = ownerId };
        var otherOrganization = new Organization { Name = "Other tenant", Code = $"other-{Guid.NewGuid():N}", OwnerId = ownerId };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var moderator = NewUser(moderatorId, $"moderator-{Guid.NewGuid():N}@example.test");
            moderator.Role = "Moderator";
            var admin = NewUser(adminId, $"admin-{Guid.NewGuid():N}@example.test");
            admin.Role = "Admin";
            db.Users.AddRange(moderator, admin, NewUser(ownerId, $"owner-{Guid.NewGuid():N}@example.test"));
            db.Organizations.AddRange(assignedOrganization, otherOrganization);
            db.ModeratorAssignments.Add(new ModeratorAssignment
            {
                ModeratorUserId = moderatorId,
                OrganizationId = assignedOrganization.Id,
                GrantedByUserId = adminId,
                Capability = "organization.users.view",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", moderatorId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Moderator");

        var assignedResponse = await client.GetAsync($"/api/organizations/{assignedOrganization.Id}/users");
        var otherResponse = await client.GetAsync($"/api/organizations/{otherOrganization.Id}/users");

        assignedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        otherResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static User NewUser(Guid id, string email) => new()
    {
        Id = id,
        FullName = email,
        Email = email,
        PasswordHash = "test",
        Role = "Member",
        IsActive = true
    };
}
