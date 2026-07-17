using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Qaly.Infrastructure.Data;
using StackExchange.Redis;
using Moq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Qaly.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Qaly.IntegrationTests;

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"QalyIntegrationTests-{Guid.NewGuid()}";
    public Guid TestUserId { get; } = Guid.Parse("B0000000-0000-0000-0000-000000000000");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Redis:ConnectionString"] = "localhost:6379", // Just to satisfy Program.cs
                ["AiJobsV4:Enabled"] = "true",
                ["AiJobsV4:WorkerEnabled"] = "false",
                ["PrivacyV4:Enabled"] = "true",
                ["PrivacyV4:WorkerEnabled"] = "false",
                ["PrivacyV4:EnforceSensitiveIngestion"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Mock Redis
            var redisMock = new Mock<IConnectionMultiplexer>();
            var dbMock = new Mock<IDatabase>();
            redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
            redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<System.Net.EndPoint>());
            services.AddSingleton(redisMock.Object);
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<QalyDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<QalyDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Storage.IDatabaseProvider>();
            services.AddDbContext<QalyDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Mock other heavy infrastructure
            services.AddSingleton(new Mock<IAiIngestionService>().Object);
            services.AddSingleton(new Mock<IAiService>().Object);

            // Test Auth
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
        });
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
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
                new Claim(ClaimTypes.Name, "Test User"),
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
