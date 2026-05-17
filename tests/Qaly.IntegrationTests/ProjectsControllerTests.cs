using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class ProjectsControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectsControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new User { Id = userId, FullName = name, Email = email, IsActive = true });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsSuccess()
    {
        // Arrange
        await EnsureUserExists(_factory.TestUserId, "Test User", "test@qaly.dev");
        var dto = new CreateProjectDto("Integration Project", "INT-1", "Description", null, null, null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/projects", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ProjectDto>>();
        result!.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Integration Project");
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_UserCannotAccessOtherUserProject_ReturnsForbidden()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await EnsureUserExists(otherUserId, "Other User", "other@qaly.dev");
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project { Id = projectId, Name = "Secret Project", Code = $"secret-{Guid.NewGuid():N}", OwnerId = otherUserId });
            await db.SaveChangesAsync();
        }

        // Act - Calling with TestUserId (factory.TestUserId)
        var response = await _client.GetAsync($"/api/projects/{projectId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_WhenUserIsOwner_ReturnsSuccess()
    {
        // Arrange
        var userId = _factory.TestUserId;
        await EnsureUserExists(userId, "Test User", "test@qaly.dev");
        var projectId = Guid.NewGuid();
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project { Id = projectId, Name = "Old Name", Code = $"old-code-{Guid.NewGuid():N}", OwnerId = userId });
            await db.SaveChangesAsync();
        }

        var dto = new UpdateProjectDto("New Name", null, "Description", null, "Active", null, null);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ProjectDto>>();
        result!.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("New Name");
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);
}
#pragma warning restore CA1707
