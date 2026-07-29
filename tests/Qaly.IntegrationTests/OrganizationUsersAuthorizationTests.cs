using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Services;
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

    [Theory]
    [InlineData(ModeratorCapabilities.UsersInvite, "invite")]
    [InlineData(ModeratorCapabilities.UsersUpdateRole, "update")]
    [InlineData(ModeratorCapabilities.UsersRemove, "remove")]
    public async Task OrganizationUsers_AllowsOnlyTheDelegatedMutation(string capability, string operation)
    {
        var setup = await SeedModeratorScenarioAsync(
            capability,
            addTargetAsMember: !string.Equals(operation, "invite", StringComparison.Ordinal));
        using var client = CreateModeratorClient(setup.ModeratorId);

        HttpResponseMessage response = operation switch
        {
            "invite" => await client.PostAsJsonAsync(
                $"/api/organizations/{setup.OrganizationId}/users",
                new { email = setup.TargetEmail, role = OrganizationRoleRules.Member }),
            "update" => await client.PatchAsJsonAsync(
                $"/api/organizations/{setup.OrganizationId}/users/{setup.TargetId}",
                new { role = OrganizationRoleRules.BillingAdmin }),
            "remove" => await client.DeleteAsync(
                $"/api/organizations/{setup.OrganizationId}/users/{setup.TargetId}"),
            _ => throw new InvalidOperationException($"Unknown operation {operation}.")
        };

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
    }

    [Fact]
    public async Task OrganizationUsers_DoesNotInferInviteFromViewCapability()
    {
        var setup = await SeedModeratorScenarioAsync(ModeratorCapabilities.UsersView, addTargetAsMember: false);
        using var client = CreateModeratorClient(setup.ModeratorId);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{setup.OrganizationId}/users",
            new { email = setup.TargetEmail, role = OrganizationRoleRules.Member });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OrganizationUsers_RejectsExpiredOrRevokedCapability(bool expired, bool revoked)
    {
        var setup = await SeedModeratorScenarioAsync(
            ModeratorCapabilities.UsersView,
            expiresAt: expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1),
            revokedAt: revoked ? DateTimeOffset.UtcNow.AddMinutes(-1) : null);
        using var client = CreateModeratorClient(setup.ModeratorId);

        var response = await client.GetAsync($"/api/organizations/{setup.OrganizationId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrganizationUsers_RejectsMutationInAnotherOrganization()
    {
        var setup = await SeedModeratorScenarioAsync(ModeratorCapabilities.UsersInvite, addTargetAsMember: false);
        var otherOrganizationId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.Add(NewUser(otherOwnerId, $"other-owner-{Guid.NewGuid():N}@example.test"));
            db.Organizations.Add(new Organization
            {
                Id = otherOrganizationId,
                Name = "Unassigned tenant",
                Code = $"unassigned-{Guid.NewGuid():N}",
                OwnerId = otherOwnerId
            });
            await db.SaveChangesAsync();
        }

        using var client = CreateModeratorClient(setup.ModeratorId);
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{otherOrganizationId}/users",
            new { email = setup.TargetEmail, role = OrganizationRoleRules.Member });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<ModeratorScenario> SeedModeratorScenarioAsync(
        string capability,
        bool addTargetAsMember = true,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null)
    {
        var moderatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var targetEmail = $"target-{Guid.NewGuid():N}@example.test";
        var organization = new Organization
        {
            Name = "Delegated tenant",
            Code = $"delegated-{Guid.NewGuid():N}",
            OwnerId = ownerId
        };

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var moderator = NewUser(moderatorId, $"moderator-{Guid.NewGuid():N}@example.test");
        moderator.Role = SystemRoleRules.Moderator;
        var admin = NewUser(adminId, $"admin-{Guid.NewGuid():N}@example.test");
        admin.Role = SystemRoleRules.Admin;
        db.Users.AddRange(
            moderator,
            admin,
            NewUser(ownerId, $"owner-{Guid.NewGuid():N}@example.test"),
            NewUser(targetId, targetEmail));
        db.Organizations.Add(organization);
        if (addTargetAsMember)
        {
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = targetId,
                Role = OrganizationRoleRules.Member
            });
        }

        db.ModeratorAssignments.Add(new ModeratorAssignment
        {
            ModeratorUserId = moderatorId,
            OrganizationId = organization.Id,
            GrantedByUserId = adminId,
            Capability = capability,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            IsActive = !revokedAt.HasValue,
            RevokedAt = revokedAt
        });
        await db.SaveChangesAsync();
        return new ModeratorScenario(moderatorId, organization.Id, targetId, targetEmail);
    }

    private HttpClient CreateModeratorClient(Guid moderatorId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", moderatorId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", SystemRoleRules.Moderator);
        return client;
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

    private sealed record ModeratorScenario(
        Guid ModeratorId,
        Guid OrganizationId,
        Guid TargetId,
        string TargetEmail);
}
