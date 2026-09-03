using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class SearchBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public SearchBoundaryIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-SEARCH-PRIVATE-TASK-01")]
    public async Task Search_UsesCanonicalTaskVisibilityPolicy()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var visiblePublicId = Guid.NewGuid();
        var visibleAssignedId = Guid.NewGuid();
        var hiddenPrivateId = Guid.NewGuid();
        var hiddenTenantId = Guid.NewGuid();
        var needle = $"search-boundary-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                User(actorId, "Search actor"),
                User(ownerId, "Search owner"),
                User(outsiderId, "Search outsider"));
            db.Projects.AddRange(
                new Project { Id = projectId, Name = "Accessible search project", Code = $"SEA-{Guid.NewGuid():N}", OwnerId = ownerId },
                new Project { Id = otherProjectId, Name = "Other tenant project", Code = $"OTH-{Guid.NewGuid():N}", OwnerId = outsiderId });
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = "Member" });
            db.TaskItems.AddRange(
                Task(visiblePublicId, projectId, ownerId, $"{needle} public", isPrivate: false),
                Task(visibleAssignedId, projectId, ownerId, $"{needle} assigned", isPrivate: true, assigneeId: actorId),
                Task(hiddenPrivateId, projectId, ownerId, $"{needle} hidden private", isPrivate: true),
                Task(hiddenTenantId, otherProjectId, outsiderId, $"{needle} hidden tenant", isPrivate: false));
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(actorId, "Member");
        using var response = await client.GetAsync($"/api/search?query={needle}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var taskIds = document.RootElement.EnumerateArray()
            .Where(row => row.GetProperty("type").GetString() == "Task")
            .Select(row => row.GetProperty("id").GetGuid())
            .ToArray();
        taskIds.Should().BeEquivalentTo([visiblePublicId, visibleAssignedId]);
        taskIds.Should().NotContain([hiddenPrivateId, hiddenTenantId]);
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-SEARCH-CUSTOMER-WIKI-01")]
    public async Task Search_CustomerRole_OnlyReturnsCustomerSafeWikiContent()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var safeId = Guid.NewGuid();
        var publicId = Guid.NewGuid();
        var internalId = Guid.NewGuid();
        var needle = $"wiki-boundary-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(User(actorId, "Customer search actor"), User(ownerId, "Wiki owner"));
            db.Projects.Add(new Project { Id = projectId, Name = "Customer wiki project", Code = $"WIK-{Guid.NewGuid():N}", OwnerId = ownerId });
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = "Customer" });
            db.WikiPages.AddRange(
                Wiki(safeId, projectId, ownerId, $"{needle} safe", "customer_safe"),
                Wiki(publicId, projectId, ownerId, $"{needle} public", "public"),
                Wiki(internalId, projectId, ownerId, $"{needle} internal", "internal"));
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(actorId, "Member");
        using var response = await client.GetAsync($"/api/search?query={needle}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var wikiIds = document.RootElement.EnumerateArray()
            .Where(row => row.GetProperty("type").GetString() == "Wiki")
            .Select(row => row.GetProperty("id").GetGuid())
            .ToArray();
        wikiIds.Should().BeEquivalentTo([safeId, publicId]);
        wikiIds.Should().NotContain(internalId);
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-SEARCH-PRIVATE-WIKI-01")]
    public async Task Search_Member_SeesOwnPrivateWikiButNotAnotherAuthorsPrivateWiki()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var ownPrivateId = Guid.NewGuid();
        var ownerPrivateId = Guid.NewGuid();
        var internalId = Guid.NewGuid();
        var needle = $"private-wiki-boundary-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(User(actorId, "Wiki member"), User(ownerId, "Wiki owner"));
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Private wiki project",
                Code = $"PWI-{Guid.NewGuid():N}",
                OwnerId = ownerId
            });
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = "Member" });
            db.WikiPages.AddRange(
                Wiki(ownPrivateId, projectId, actorId, $"{needle} mine", "private"),
                Wiki(ownerPrivateId, projectId, ownerId, $"{needle} owner only", "private"),
                Wiki(internalId, projectId, ownerId, $"{needle} internal", "internal"));
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(actorId, "Member");
        using var response = await client.GetAsync($"/api/search?query={needle}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var wikiIds = document.RootElement.EnumerateArray()
            .Where(row => row.GetProperty("type").GetString() == "Wiki")
            .Select(row => row.GetProperty("id").GetGuid())
            .ToArray();
        wikiIds.Should().BeEquivalentTo([ownPrivateId, internalId]);
        wikiIds.Should().NotContain(ownerPrivateId);
    }

    private HttpClient CreateClient(Guid userId, string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private static User User(Guid id, string name)
        => new()
        {
            Id = id,
            FullName = name,
            Email = $"{id:N}@search.qaly.test",
            PasswordHash = "integration-only",
            Role = "Member",
            IsActive = true
        };

    private static TaskItem Task(
        Guid id,
        Guid projectId,
        Guid reporterId,
        string title,
        bool isPrivate,
        Guid? assigneeId = null)
        => new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            AssigneeId = assigneeId,
            Title = title,
            Status = "Todo",
            IsPrivate = isPrivate
        };

    private static WikiPage Wiki(Guid id, Guid projectId, Guid authorId, string title, string visibility)
        => new()
        {
            Id = id,
            ProjectId = projectId,
            AuthorId = authorId,
            Title = title,
            Content = title,
            Visibility = visibility
        };
}
