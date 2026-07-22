using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class AuthBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AuthBoundaryIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProjects_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        request.Headers.Add("X-Test-Auth", "None");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProjects_WhenInvalidAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        request.Headers.Add("X-Test-Auth", "Invalid");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProjectById_WhenUserIsOutsideProject_ReturnsForbiddenWithoutLeakingProjectName()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectName = $"Secret Project {Guid.NewGuid():N}";
        await EnsureUserExists(_factory.TestUserId, "Test User", $"test-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(ownerId, "Other Owner", $"owner-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = projectName,
                Code = $"secret-{Guid.NewGuid():N}",
                OwnerId = ownerId
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/projects/{projectId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().NotContain(projectName);
    }

    [Fact]
    public async Task GetAdminUsers_WhenAuthenticatedAsMember_ReturnsForbidden()
    {
        await EnsureUserExists(_factory.TestUserId, "Member User", $"member-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        request.Headers.Add("X-Test-Role", "User");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuditLogByEntity_WhenAuthenticatedAsMember_ReturnsForbidden()
    {
        await EnsureUserExists(_factory.TestUserId, "Member User", $"member-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit-logs/entity/Project/{Guid.NewGuid()}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkNotificationAsRead_WhenUserDoesNotOwnNotification_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        await EnsureUserExists(ownerId, "Owner User", $"owner-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(attackerId, "Attacker User", $"attacker-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Notifications.Add(new Notification
            {
                Id = notificationId,
                UserId = ownerId,
                Message = "Owner-only notification",
                Type = "Info",
                Tone = "info",
                IsRead = false
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/notifications/{notificationId}/read");
        request.Headers.Add("X-Test-UserId", attackerId.ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var stored = await verifyDb.Notifications.SingleAsync(item => item.Id == notificationId);
        stored.IsRead.Should().BeFalse();
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(user => user.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = name,
                Email = email,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }
}
#pragma warning restore CA1707
