using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class UserDirectoryBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public UserDirectoryBoundaryIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-USER-DIRECTORY-TENANT-01")]
    public async Task MemberDirectory_OnlyReturnsRealCollaborators_AndHidesSystemRole()
    {
        var data = await SeedDirectoryBoundaryAsync();
        using var client = CreateClient(data.ActorId, "Member");

        using var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = document.RootElement.GetProperty("data").EnumerateArray().ToArray();
        var ids = rows.Select(row => row.GetProperty("id").GetGuid()).ToArray();

        ids.Should().Contain([data.ActorId, data.OrganizationPeerId, data.ProjectPeerId, data.GroupPeerId]);
        ids.Should().NotContain(data.OutsiderId);
        rows.Should().OnlyContain(row => row.GetProperty("systemRole").ValueKind == JsonValueKind.Null);

        using var hiddenProfile = await client.GetAsync($"/api/users/{data.OutsiderId:D}");
        hiddenProfile.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-USER-DIRECTORY-ADMIN-01")]
    public async Task SystemAdminDirectory_ReturnsActiveAccounts_WithSystemRoleForViewAs()
    {
        var data = await SeedDirectoryBoundaryAsync();
        using var client = CreateClient(data.ActorId, "Admin");

        using var response = await client.GetAsync($"/api/users/{data.OutsiderId:D}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = document.RootElement.GetProperty("data");
        row.GetProperty("id").GetGuid().Should().Be(data.OutsiderId);
        row.GetProperty("systemRole").GetString().Should().Be("Moderator");
    }

    private HttpClient CreateClient(Guid userId, string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private async Task<DirectoryBoundaryData> SeedDirectoryBoundaryAsync()
    {
        var actorId = Guid.NewGuid();
        var organizationPeerId = Guid.NewGuid();
        var projectPeerId = Guid.NewGuid();
        var groupPeerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        db.Users.AddRange(
            User(actorId, "Actor", "Member"),
            User(organizationPeerId, "Organization peer", "Admin"),
            User(projectPeerId, "Project peer", "Member"),
            User(groupPeerId, "Group peer", "Member"),
            User(outsiderId, "Unrelated tenant user", "Moderator"));
        db.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Directory boundary organization",
            Code = $"DIR-{Guid.NewGuid():N}",
            OwnerId = actorId,
            IsActive = true
        });
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organizationId, UserId = actorId, Role = "Owner" },
            new OrganizationMember { OrganizationId = organizationId, UserId = organizationPeerId, Role = "Member" });
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Standalone shared project",
            Code = $"DIRP-{Guid.NewGuid():N}",
            OwnerId = projectPeerId
        });
        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = actorId,
            Role = "Member"
        });
        db.WorkGroups.Add(new WorkGroup
        {
            Id = groupId,
            Name = "Shared directory group",
            OwnerId = actorId
        });
        db.WorkGroupMembers.Add(new WorkGroupMember
        {
            WorkGroupId = groupId,
            UserId = groupPeerId,
            Role = "Member"
        });
        await db.SaveChangesAsync();

        return new DirectoryBoundaryData(actorId, organizationPeerId, projectPeerId, groupPeerId, outsiderId);
    }

    private static User User(Guid id, string name, string role)
        => new()
        {
            Id = id,
            FullName = name,
            Email = $"{id:N}@directory.qaly.test",
            PasswordHash = "integration-only",
            Role = role,
            IsActive = true
        };

    private sealed record DirectoryBoundaryData(
        Guid ActorId,
        Guid OrganizationPeerId,
        Guid ProjectPeerId,
        Guid GroupPeerId,
        Guid OutsiderId);
}
