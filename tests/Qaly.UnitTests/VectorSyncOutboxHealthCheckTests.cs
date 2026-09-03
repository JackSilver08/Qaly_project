using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class VectorSyncOutboxHealthCheckTests
{
    [Fact]
    public async Task SemanticSearchDisabled_ReportsHealthyAndDoesNotTreatDormantRowsAsBacklog()
    {
        await using var db = new QalyDbContext(NewOptions());
        db.VectorSyncOutbox.Add(NewMessage(sequence: 1, createdAt: DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        var result = await NewHealthCheck(db, semanticEnabled: false)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["enabled"].Should().Be(false);
        result.Data.Should().NotContainKey("pending");
    }

    [Fact]
    public async Task DeadLetter_IsExcludedFromPendingButSurfacesAsDegradedOperatorSignal()
    {
        await using var db = new QalyDbContext(NewOptions());
        var deadLetter = NewMessage(sequence: 1, createdAt: DateTimeOffset.UtcNow.AddMinutes(-30));
        deadLetter.DeadLetteredAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        db.VectorSyncOutbox.Add(deadLetter);
        await db.SaveChangesAsync();

        var result = await NewHealthCheck(db, semanticEnabled: true)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["pending"].Should().Be(0);
        result.Data["deadLettered"].Should().Be(1);
        result.Data["oldestPendingAgeMinutes"].Should().Be(0d);
    }

    [Fact]
    public async Task OldestUnprocessedSequenceBeyondAgeThreshold_IsDegraded()
    {
        await using var db = new QalyDbContext(NewOptions());
        db.VectorSyncOutbox.AddRange(
            NewMessage(sequence: 2, createdAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
            NewMessage(sequence: 1, createdAt: DateTimeOffset.UtcNow.AddMinutes(-20)));
        await db.SaveChangesAsync();

        var result = await NewHealthCheck(db, semanticEnabled: true)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["pending"].Should().Be(2);
        result.Data["deadLettered"].Should().Be(0);
        result.Data["oldestPendingAgeMinutes"].Should().BeOfType<double>()
            .Which.Should().BeGreaterThanOrEqualTo(19);
    }

    private static OutboxHealthCheck NewHealthCheck(QalyDbContext db, bool semanticEnabled)
        => new(
            new GenericRepository<VectorSyncOutbox>(db),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ai:SemanticEnabled"] = semanticEnabled.ToString(),
                    ["OperationalHealth:VectorOutboxPendingThreshold"] = "200",
                    ["OperationalHealth:VectorOutboxMaxPendingAgeMinutes"] = "15"
                })
                .Build());

    private static DbContextOptions<QalyDbContext> NewOptions()
        => new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase($"vector-outbox-health-{Guid.NewGuid():N}")
            .Options;

    private static VectorSyncOutbox NewMessage(long sequence, DateTimeOffset createdAt)
        => new()
        {
            SequenceNumber = sequence,
            EventType = VectorSyncEventTypes.TaskUpdated,
            AggregateType = VectorSyncAggregateTypes.Task,
            AggregateId = Guid.NewGuid(),
            Payload = "{}",
            NextAttemptAt = createdAt,
            CreatedAt = createdAt
        };
}
