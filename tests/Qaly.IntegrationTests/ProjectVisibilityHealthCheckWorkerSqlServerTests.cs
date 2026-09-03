using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.IntegrationTests;

public sealed class ProjectVisibilityHealthCheckWorkerSqlServerTests
{
    [Fact]
    public async Task TrashedProjectMembership_IsNotReportedAsOrphanBySqlServerQuery()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        var databaseName = $"QalyVisibilityWorkerTests_{Guid.NewGuid():N}";
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var builder = string.IsNullOrWhiteSpace(configured)
            ? new SqlConnectionStringBuilder
            {
                DataSource = "(localdb)\\MSSQLLocalDB",
                InitialCatalog = databaseName,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                MultipleActiveResultSets = true
            }
            : new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
        var connectionString = builder.ConnectionString;
        var services = new ServiceCollection()
            .AddDbContext<QalyDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure()))
            .BuildServiceProvider();

        try
        {
            await using (var setupScope = services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<QalyDbContext>();
                await db.Database.MigrateAsync();
                var ownerId = Guid.NewGuid();
                var projectId = Guid.NewGuid();
                db.Users.Add(new User
                {
                    Id = ownerId,
                    FullName = "SQL visibility owner",
                    Email = $"visibility-{ownerId:N}@qaly.test",
                    PasswordHash = "test"
                });
                db.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "SQL restorable Project",
                    Code = $"VIS-{projectId:N}"[..16],
                    OwnerId = ownerId,
                    IsDeleted = true,
                    DeletedAt = DateTimeOffset.UtcNow
                });
                db.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = ownerId,
                    Role = "Owner"
                });
                await db.SaveChangesAsync();
            }

            var worker = new ProjectVisibilityHealthCheckWorker(
                services,
                NullLogger<ProjectVisibilityHealthCheckWorker>.Instance);

            await worker.RunHealthCheckAsync(CancellationToken.None);

            await using var verifyScope = services.CreateAsyncScope();
            var verification = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await verification.AuditLogs.CountAsync(log => log.Action == "VisibilityHealthCheckAlert"))
                .Should().Be(0);
        }
        finally
        {
            await using (var cleanupScope = services.CreateAsyncScope())
            {
                var db = cleanupScope.ServiceProvider.GetRequiredService<QalyDbContext>();
                await db.Database.EnsureDeletedAsync();
            }

            await services.DisposeAsync();
        }
    }
}
