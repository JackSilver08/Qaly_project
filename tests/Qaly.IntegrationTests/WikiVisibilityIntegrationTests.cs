using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Services;
using Qaly.Infrastructure.Data;
using Qaly.Domain.Entities;
using Xunit;

namespace Qaly.IntegrationTests;

public class WikiVisibilityIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public WikiVisibilityIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomerUser_Sees_Public_And_CustomerSafe_Pages()
    {
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var customerId = _factory.TestUserId; // test auth maps to this user

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

            db.Users.Add(new Qaly.Domain.Entities.User { Id = ownerId, FullName = "Owner", Email = "owner@local" });
            db.Users.Add(new Qaly.Domain.Entities.User { Id = customerId, FullName = "Customer", Email = "customer@local" });

            db.Projects.Add(new Project { Id = projectId, Name = "P", OwnerId = ownerId });
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = customerId, Role = ProjectRoleRules.Customer });

            db.WikiPages.AddRange(
                new WikiPage { Id = Guid.NewGuid(), ProjectId = projectId, AuthorId = ownerId, Title = "Public Page", Content = "x", Visibility = "public", UpdatedAt = DateTimeOffset.UtcNow },
                new WikiPage { Id = Guid.NewGuid(), ProjectId = projectId, AuthorId = ownerId, Title = "Internal Page", Content = "x", Visibility = "internal", UpdatedAt = DateTimeOffset.UtcNow },
                new WikiPage { Id = Guid.NewGuid(), ProjectId = projectId, AuthorId = ownerId, Title = "Customer Page", Content = "x", Visibility = "customer_safe", UpdatedAt = DateTimeOffset.UtcNow }
            );

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var resp = await client.GetFromJsonAsync<ApiResult<List<WikiPageResult>>>($"/api/projects/{projectId}/wiki");

        resp.Should().NotBeNull();
        resp!.IsSuccess.Should().BeTrue();
        resp.Data.Should().HaveCount(2);
        resp.Data!.Should().Contain(d => d.Title == "Public Page");
        resp.Data!.Should().Contain(d => d.Title == "Customer Page");
    }
}

public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }
}

public class WikiPageResult
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string Visibility { get; set; }
    public string AuthorName { get; set; }
    public string UpdatedAt { get; set; }
}
