using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
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
        var response = await SendWithCsrfAsync(HttpMethod.Post, "/api/projects", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ProjectDto>>();
        result!.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Integration Project");
    }

    [Fact]
    public async Task Create_WithoutCsrf_IsRejectedBeforeAnyProjectMutation()
    {
        await EnsureUserExists(_factory.TestUserId, "Test User", "test@qaly.dev");
        var code = $"NO-CSRF-{Guid.NewGuid():N}";

        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectDto("Must not be created", code, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.AnyAsync(item => item.Code == code)).Should().BeFalse();
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
        var response = await SendWithCsrfAsync(HttpMethod.Put, $"/api/projects/{projectId}", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ProjectDto>>();
        result!.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task OrganizationAdminGetsPortfolioProjectWhileOrdinaryOrganizationMemberIsDenied()
    {
        var ownerId = Guid.NewGuid();
        var organizationAdminId = Guid.NewGuid();
        var organizationMemberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await EnsureUserExists(ownerId, "Owner", $"owner-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(organizationAdminId, "Organization admin", $"admin-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(organizationMemberId, "Organization member", $"member-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Portfolio tenant",
                Code = $"portfolio-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.AddRange(
                new OrganizationMember { OrganizationId = organizationId, UserId = ownerId, Role = OrganizationRoleRules.Owner },
                new OrganizationMember { OrganizationId = organizationId, UserId = organizationAdminId, Role = OrganizationRoleRules.OrganizationAdmin },
                new OrganizationMember { OrganizationId = organizationId, UserId = organizationMemberId, Role = OrganizationRoleRules.Member });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Portfolio project",
                Code = $"project-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            await db.SaveChangesAsync();
        }

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-UserId", organizationAdminId.ToString());
        using var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add("X-Test-UserId", organizationMemberId.ToString());

        var adminResponse = await adminClient.GetAsync($"/api/projects/{projectId}");
        var memberResponse = await memberClient.GetAsync($"/api/projects/{projectId}");

        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await adminResponse.Content.ReadFromJsonAsync<ApiResult<ProjectDto>>();
        payload!.Data!.Permissions!.CanManageProject.Should().BeTrue();
        payload.Data.Permissions.Role.Should().Be(OrganizationRoleRules.OrganizationAdmin);
        memberResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);

    private async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpMethod method, string url, T body)
    {
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await _client.SendAsync(request);
    }

    private sealed record CsrfResponse(string Token);
}
#pragma warning restore CA1707
