using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class DurableWorkQueueHealthCheckTests
{
    [Fact]
    public async Task AiJobsDisabled_ReportsHealthyWithoutTreatingDormantRowsAsBacklog()
    {
        await using var db = new QalyDbContext(NewOptions());
        db.AiJobDispatches.Add(new AiJobDispatch
        {
            AiJobId = Guid.NewGuid(),
            AvailableAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var result = await new AiJobQueueHealthCheck(db, Configuration(aiEnabled: false))
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["enabled"].Should().Be(false);
        result.Data.Should().NotContainKey("pending");
    }

    [Fact]
    public async Task AiJobExpiredLease_ReportsDegraded()
    {
        await using var db = new QalyDbContext(NewOptions());
        db.AiJobDispatches.Add(new AiJobDispatch
        {
            AiJobId = Guid.NewGuid(),
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LeaseOwner = "stale-worker",
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1)
        });
        await db.SaveChangesAsync();

        var result = await new AiJobQueueHealthCheck(db, Configuration(aiEnabled: true))
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["expiredLeases"].Should().Be(1);
    }

    [Fact]
    public async Task PrivacyDisabled_ReportsHealthyWithoutTreatingDormantRowsAsBacklog()
    {
        await using var db = new QalyDbContext(NewOptions());
        db.DataSubjectRequests.Add(NewDsar(deadlineAt: DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        var result = await new PrivacyWorkQueueHealthCheck(db, Configuration(privacyEnabled: false))
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["enabled"].Should().Be(false);
        result.Data.Should().NotContainKey("pendingDsar");
    }

    [Fact]
    public async Task OverdueDsar_ReportsDegradedLegalDeadlineSignal()
    {
        await using var db = new QalyDbContext(NewOptions());
        db.DataSubjectRequests.Add(NewDsar(deadlineAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var result = await new PrivacyWorkQueueHealthCheck(db, Configuration(privacyEnabled: true))
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["pendingDsar"].Should().Be(1);
        result.Data["overdueDsar"].Should().Be(1);
    }

    private static IConfiguration Configuration(bool aiEnabled = false, bool privacyEnabled = false)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiJobsV4:Enabled"] = aiEnabled.ToString(),
                ["AiJobsV4:WorkerEnabled"] = "true",
                ["PrivacyV4:Enabled"] = privacyEnabled.ToString(),
                ["PrivacyV4:WorkerEnabled"] = "true",
                ["OperationalHealth:AiJobPendingThreshold"] = "200",
                ["OperationalHealth:AiJobMaxPendingAgeMinutes"] = "15",
                ["OperationalHealth:PrivacyWorkPendingThreshold"] = "200",
                ["OperationalHealth:PrivacyWorkMaxPendingAgeMinutes"] = "15"
            })
            .Build();

    private static DbContextOptions<QalyDbContext> NewOptions()
        => new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase($"durable-queue-health-{Guid.NewGuid():N}")
            .Options;

    private static DataSubjectRequest NewDsar(DateTimeOffset deadlineAt)
        => new()
        {
            TenantId = Guid.NewGuid(),
            RequesterUserId = Guid.NewGuid(),
            SubjectUserId = Guid.NewGuid(),
            RequestType = DataSubjectRequestTypes.Export,
            ScopeJson = "{}",
            Status = DataSubjectRequestStatuses.Accepted,
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            DeadlineAt = deadlineAt
        };
}
