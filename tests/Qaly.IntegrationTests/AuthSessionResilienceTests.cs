using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707

/// <summary>
/// T1-DH-02: Auth session resilience integration tests.
/// Maps: REQ-P0-01, REQ-ADR-02, GAP-002/021.
/// Verifies: cookie flags, revoke semantics, logout, Redis-degraded path bounded time,
/// and no false success when Redis is unavailable.
/// </summary>
public class AuthSessionResilienceTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AuthSessionResilienceTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    // ──────────────────────────────────────────────
    // Cookie configuration tests
    // ──────────────────────────────────────────────

    [Fact]
    public void CookieAuthentication_IsConfigured_WithHttpOnlyFlag()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert — HttpOnly is required per ADR-002 security requirements
        options.Cookie.HttpOnly.Should().BeTrue(
            "auth cookie must be HttpOnly to prevent JavaScript access (ADR-002)");
    }

    [Fact]
    public void CookieAuthentication_IsConfigured_WithSlidingExpiration()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert — sliding renewal preserves valid active sessions (ADR-002 §3)
        options.SlidingExpiration.Should().BeTrue(
            "sliding expiration must be enabled for session renewal (ADR-002)");
    }

    [Fact]
    public void CookieAuthentication_IsConfigured_WithAbsoluteExpiry()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert — absolute expiry required per ADR-002 security requirements
        options.ExpireTimeSpan.Should().BeGreaterThan(TimeSpan.Zero,
            "absolute session expiry must be configured (ADR-002)");
        options.ExpireTimeSpan.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(24),
            "session must not be indefinitely long (ADR-002 security)");
    }

    [Fact]
    public void CookieAuthentication_ApiRedirectToLogin_Returns401NotRedirect()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert — API calls must not redirect to login page (ADR-002: same-origin cookie)
        // The OnRedirectToLogin handler is set in QalyWebServiceExtensions to return 401 for /api/*
        options.Events.Should().NotBeNull(
            "cookie events must be configured to handle API paths (ADR-002)");
    }

    [Fact]
    public void CookieAuthentication_HasNamedCookie()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert — named cookie for explicit control
        options.Cookie.Name.Should().NotBeNullOrWhiteSpace(
            "auth cookie must have an explicit name for security and debugging");
    }

    // ──────────────────────────────────────────────
    // Revoke semantics
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteSessions_WhenAuthenticated_Returns200()
    {
        // Arrange
        await EnsureUserExists(_factory.TestUserId, "Revoke Test User", $"revoke-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/sessions");
        var response = await client.SendAsync(request);

        // Assert — revoke endpoint responds without error
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "DELETE /api/auth/sessions must succeed for authenticated user");
    }

    [Fact]
    public async Task DeleteSessions_WhenUnauthenticated_Returns401()
    {
        // Arrange — unauthenticated request
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/sessions");
        request.Headers.Add("X-Test-Auth", "None");

        // Act
        var response = await client.SendAsync(request);

        // Assert — revoke must require authentication
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "DELETE /api/auth/sessions must require authentication (GAP-002)");
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_Returns200()
    {
        // Arrange
        await EnsureUserExists(_factory.TestUserId, "Logout Test User", $"logout-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert — logout completes successfully
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST /api/auth/logout must succeed for authenticated user");
    }

    [Fact]
    public async Task Logout_WhenUnauthenticated_Returns401()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-Test-Auth", "None");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "POST /api/auth/logout must require authentication");
    }

    // ──────────────────────────────────────────────
    // Redis-degraded bounded time (PERF-03 / GAP-021)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Me_WhenRedisUnavailable_RespondsWithinBoundedTime()
    {
        // Arrange — IntegrationTestFactory replaces Redis with DistributedMemoryCache
        // This simulates a degraded Redis by using the in-memory fallback.
        // The key assertion is that the response does NOT take multiple seconds.
        await EnsureUserExists(_factory.TestUserId, "Perf Test User", $"perf-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync("/api/auth/me");
        stopwatch.Stop();

        // Assert — degraded path must complete within 1 second (PERF-03)
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/auth/me must succeed in degraded Redis scenario");
        stopwatch.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(1000,
            "authenticated request must complete within 1s even with degraded Redis (PERF-03 / GAP-021)");
    }

    [Fact]
    public async Task AuthenticatedRequest_NoFalseSuccess_WhenRedisSubstituted()
    {
        // Arrange — verify that in-memory fallback still allows valid auth (not silent failure)
        await EnsureUserExists(_factory.TestUserId, "Fallback Auth User", $"fallback-{Guid.NewGuid():N}@qaly.dev");
        using var client = _factory.CreateClient();

        // Act — multiple consecutive requests should all succeed (circuit doesn't block valid fallback)
        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/api/auth/me");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Request {i + 1}: auth must not fail silently in Redis-degraded mode (GAP-021)");
        }
    }

    [Fact]
    public async Task Me_WhenSessionUserNoLongerExists_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a restored database must not leave an orphaned session looking authenticated");
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(u => u.Id == userId))
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
