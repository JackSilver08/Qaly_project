using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Infrastructure.Data;
using StackExchange.Redis;

namespace Qaly.WebFeatureTests;

public sealed class FeatureTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"QalyWebFeatureTests-{Guid.NewGuid():N}";

    public Guid TestUserId { get; } = Guid.Parse("B0000000-0000-0000-0000-000000000000");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=QalyTests;Integrated Security=True;TrustServerCertificate=True",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Ai:SemanticEnabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            var redisMock = new Mock<IConnectionMultiplexer>();
            var dbMock = new Mock<IDatabase>();
            redisMock.Setup(redis => redis.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
            redisMock.Setup(redis => redis.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<System.Net.EndPoint>());

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(redisMock.Object);

            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<QalyDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<QalyDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Storage.IDatabaseProvider>();
            services.AddDbContext<QalyDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IHostedService>();
            services.RemoveAll<IAiIngestionService>();
            services.AddSingleton(new Mock<IAiIngestionService>().Object);
            services.RemoveAll<IAiService>();
            services.AddSingleton(new Mock<IAiService>().Object);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("X-Test-Auth", out var authMode))
            {
                var mode = authMode.ToString();
                if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                if (string.Equals(mode, "Invalid", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AuthenticateResult.Fail("Invalid test authentication."));
                }
            }

            var userId = Request.Headers.TryGetValue("X-Test-UserId", out var userIdHeader)
                ? userIdHeader.ToString()
                : "B0000000-0000-0000-0000-000000000000";
            var role = Request.Headers.TryGetValue("X-Test-Role", out var roleHeader)
                ? roleHeader.ToString()
                : "User";

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Feature Test User"),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
