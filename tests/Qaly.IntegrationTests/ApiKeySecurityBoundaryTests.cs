using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Web.Auth;

namespace Qaly.IntegrationTests;

public sealed class ApiKeySecurityBoundaryTests
{
    [Fact]
    public async Task ScopeMiddleware_AllowsExactScopeAndRejectsMissingOrUnpublishedRoute()
    {
        var called = false;
        var middleware = new ApiKeyScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var allowed = Context("/api/tasks", "POST", "tasks:write");
        await middleware.InvokeAsync(allowed);
        called.Should().BeTrue();
        allowed.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        called = false;
        var missing = Context("/api/tasks", "POST", "tasks:read");
        await middleware.InvokeAsync(missing);
        called.Should().BeFalse();
        missing.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var unpublished = Context("/api/admin/users", "GET", "projects:read");
        await middleware.InvokeAsync(unpublished);
        unpublished.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ScopeMiddleware_UsesSpecificNestedScopesAndRejectsViewAs()
    {
        var called = false;
        var middleware = new ApiKeyScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var webhook = Context("/api/projects/11111111-1111-1111-1111-111111111111/webhooks", "GET", "projects:read");
        await middleware.InvokeAsync(webhook);
        webhook.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var sprint = Context("/api/projects/11111111-1111-1111-1111-111111111111/sprints", "GET", "tasks:read");
        await middleware.InvokeAsync(sprint);
        called.Should().BeTrue();

        called = false;
        var simulated = Context("/api/projects", "GET", "projects:read");
        simulated.Request.Headers[Qaly.Web.Middlewares.HeaderSimulationMiddleware.HeaderName] = Guid.NewGuid().ToString();
        await middleware.InvokeAsync(simulated);
        called.Should().BeFalse();
        simulated.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AuthenticationHandler_AuthenticatesCanonicalKeyAndPersistsLastUsedAt()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var rawKey = "qaly_sk_12345678901234567890123456789012";
        db.Users.Add(new User
        {
            Id = userId,
            FullName = "Key owner",
            Email = $"owner-{userId:N}@qaly.test",
            PasswordHash = "test",
            Role = "Admin",
            IsActive = true
        });
        var key = new ApiKey
        {
            UserId = userId,
            Name = "Build",
            Prefix = rawKey[..16],
            KeyHash = ApiKeyService.HashKey(rawKey),
            Scopes = "[\"tasks:read\"]"
        };
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {rawKey}";
        var handler = new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<ApiKeyAuthenticationOptions>(new ApiKeyAuthenticationOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new GenericRepository<ApiKey>(db),
            new UnitOfWork(db));
        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyDefaults.AuthenticationScheme, null, typeof(ApiKeyAuthenticationHandler)),
            context);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        var principal = result.Principal!;
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(userId.ToString());
        principal.FindAll("scope").Select(claim => claim.Value).Should().Equal("tasks:read");
        db.ChangeTracker.Clear();
        (await db.ApiKeys.SingleAsync()).LastUsedAt.Should().NotBeNull();
    }

    private static DefaultHttpContext Context(string path, string method, params string[] scopes)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Admin")
        };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiKeyDefaults.AuthenticationScheme))
        };
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static QalyDbContext NewDb()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
