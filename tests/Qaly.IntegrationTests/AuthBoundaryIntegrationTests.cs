using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    [Trait("TestId", "TEST-RBAC-AI-GLOBAL-SYNC-01")]
    public async Task GlobalAiSync_WhenAuthenticatedAsMember_ReturnsForbidden()
    {
        await EnsureUserExists(_factory.TestUserId, "Member User", $"member-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai/sync");
        request.Headers.Add("X-Test-Role", "Member");
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-AI-GLOBAL-SYNC-02")]
    public async Task GlobalAiSync_WhenAuthenticatedAsSystemAdmin_IsAllowed()
    {
        await EnsureUserExists(_factory.TestUserId, "System Admin", $"admin-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai/sync");
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ViewAs_WhenAdminReadsIdentity_UsesTargetIdAndRoleBeforeAuthorization()
    {
        var targetId = Guid.NewGuid();
        await EnsureUserExists(targetId, "Simulated Member", $"sim-{Guid.NewGuid():N}@qaly.dev");

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("X-Test-Role", "Admin");
        request.Headers.Add("X-Simulate-User-Id", targetId.ToString());

        using var response = await client.SendAsync(request);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Simulation-Active").Should().ContainSingle("true");
        response.Headers.GetValues("X-Simulation-Read-Only").Should().ContainSingle("true");
        json.RootElement.GetProperty("data").GetProperty("id").GetGuid().Should().Be(targetId);
        json.RootElement.GetProperty("data").GetProperty("role").GetString().Should().Be("Member");
    }

    [Fact]
    public async Task ViewAs_WhenTargetIsMember_CannotReachAdminEndpoint()
    {
        var targetId = Guid.NewGuid();
        await EnsureUserExists(targetId, "Simulated Member", $"sim-{Guid.NewGuid():N}@qaly.dev");

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        request.Headers.Add("X-Test-Role", "Admin");
        request.Headers.Add("X-Simulate-User-Id", targetId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewAs_WhenRequestMutates_IsRejectedBeforeDomainExecution()
    {
        var targetId = Guid.NewGuid();
        await EnsureUserExists(targetId, "Simulated Member", $"sim-{Guid.NewGuid():N}@qaly.dev");

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects");
        request.Headers.Add("X-Test-Role", "Admin");
        request.Headers.Add("X-Simulate-User-Id", targetId.ToString());
        request.Content = JsonContent.Create(new { name = "Must not be created" });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("read-only");
    }

    [Fact]
    public async Task EffectiveSystemPermissions_ReturnServerDefaultsAndUserDeny()
    {
        await EnsureUserExists(_factory.TestUserId, "Member User", $"member-{Guid.NewGuid():N}@qaly.dev");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var oldRows = await db.SystemModulePermissions
                .Where(item => item.UserId == _factory.TestUserId && item.ModuleKey == "AiHub")
                .ToListAsync();
            db.SystemModulePermissions.RemoveRange(oldRows);
            db.SystemModulePermissions.Add(new SystemModulePermission
            {
                UserId = _factory.TestUserId,
                ModuleKey = "AiHub",
                IsAllowed = false,
                AiTier = "Full"
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/ProjectRoles/effective-system-permissions");
        request.Headers.Add("X-Test-Role", "Member");

        using var response = await client.SendAsync(request);
        var rows = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        rows.GetArrayLength().Should().BeGreaterThan(5);
        var aiHub = rows.EnumerateArray().Single(item => item.GetProperty("moduleKey").GetString() == "AiHub");
        aiHub.GetProperty("isAllowed").GetBoolean().Should().BeFalse();
        aiHub.GetProperty("aiTier").GetString().Should().Be("Restricted");
        aiHub.GetProperty("source").GetString().Should().Be("user_override");
        var userManagement = rows.EnumerateArray().Single(item => item.GetProperty("moduleKey").GetString() == "UserManagement");
        userManagement.GetProperty("isAllowed").GetBoolean().Should().BeFalse();
        userManagement.GetProperty("source").GetString().Should().Be("role_default");
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
        client.DefaultRequestHeaders.Add("X-Test-UserId", attackerId.ToString());
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/notifications/{notificationId}/read");
        request.Headers.Add("X-CSRF-TOKEN", csrf);

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

    private sealed record CsrfResponse(string Token);
}
#pragma warning restore CA1707
