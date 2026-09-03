using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class ProjectOperationMonitorRecoveryTests
{
    [Fact]
    public async Task EvaluateDueAsync_InvalidPersistedPlan_DefersPoisonedExecutionInsteadOfFailingBatch()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase($"project-monitor-{Guid.NewGuid():N}")
            .Options);
        var plan = new ProjectLaunchPlanArtifact
        {
            ProjectLaunchBriefId = Guid.NewGuid(),
            AssistantSessionId = Guid.NewGuid(),
            AssistantTurnId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Revision = 1,
            State = "confirmed",
            StaffingScenariosJson = "[]",
            DeliveryPlanJson = "not-json",
            BlockingReasonsJson = "[]",
            WarningsJson = "[]",
            SourceSnapshotJson = "[]",
            ScoringVersion = "test",
            PromptVersion = "test",
            ActualProvider = "test",
            ActualModel = "test"
        };
        var execution = new ProjectLaunchExecution
        {
            ProjectLaunchPlanArtifactId = plan.Id,
            OrganizationId = plan.OrganizationId,
            ProjectId = Guid.NewGuid(),
            ExecutedByUserId = Guid.NewGuid(),
            Status = "executed",
            IdempotencyKey = $"test:{Guid.NewGuid():N}",
            PayloadHash = "test",
            ReceiptJson = "{}",
            MonitoringEnabled = true,
            NextMonitorAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            ProjectLaunchPlanArtifact = plan
        };
        db.AddRange(plan, execution);
        await db.SaveChangesAsync();
        var currentUser = new Mock<ICurrentUserService>();
        var service = new ProjectLaunchOrchestratorService(
            db,
            currentUser.Object,
            Mock.Of<IAiGateway>(),
            Options.Create(new AiJobPlatformOptions { ProjectOperationMonitoringEnabled = true }),
            NullLogger<ProjectLaunchOrchestratorService>.Instance);
        var before = DateTimeOffset.UtcNow;

        await service.EvaluateDueAsync();

        db.ChangeTracker.Clear();
        var deferred = await db.ProjectLaunchExecutions.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        deferred.NextMonitorAt.Should().BeAfter(before.AddMinutes(29));
        deferred.RowRevision.Should().Be(2);
        deferred.LastMonitoredAt.Should().BeNull();
    }
}
