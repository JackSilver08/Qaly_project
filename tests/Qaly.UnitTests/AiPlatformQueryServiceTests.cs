using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class AiPlatformQueryServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IOptionsMonitor<AiJobPlatformOptions>> _options = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public AiPlatformQueryServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _currentUser.SetupGet(service => service.UserId).Returns(_userId);
        _currentUser.SetupGet(service => service.Role).Returns(ProjectRoleRules.SystemAdmin);
        _options.SetupGet(monitor => monitor.CurrentValue).Returns(new AiJobPlatformOptions
        {
            Enabled = true,
            WorkerEnabled = true
        });
    }

    [Fact]
    public async Task HealthUsageAndBudget_ReturnVisibleOperationalSnapshots()
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = _userId,
            FullName = "AI Admin",
            Email = "ai-admin@qaly.test",
            PasswordHash = "test"
        };
        var project = new Project
        {
            Id = _projectId,
            OrganizationId = _tenantId,
            OwnerId = _userId,
            Owner = user,
            Name = "AI Project",
            Code = "AIP"
        };
        var running = Job(project, AiJobStatuses.Running, now);
        var retrying = Job(project, AiJobStatuses.Retrying, now);
        var failed = Job(project, AiJobStatuses.Failed, now);
        failed.FinishedAt = now.AddHours(-1);
        _db.AddRange(user, project, running, retrying, failed);
        _db.AiJobDispatches.Add(new AiJobDispatch
        {
            AiJobId = running.Id,
            AiJob = running,
            AvailableAt = now.AddMinutes(-5),
            LeaseExpiresAt = now.AddMinutes(-1)
        });
        _db.AiUsageLedger.AddRange(
            Usage(running.Id, now.AddHours(-1), "success", 1.25m, cacheHit: true),
            Usage(retrying.Id, now.AddDays(-2), "failed", 2.75m, cacheHit: false));
        _db.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            DailyBudgetUsd = 2m,
            MonthlyBudgetUsd = 3m,
            WarnAtPercent = 50,
            HardStopEnabled = true
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var health = await service.GetHealthAsync();
        var healthSnapshot = health.Data!;
        healthSnapshot.Status.Should().Be("degraded");
        healthSnapshot.DegradedReason.Should().Be("expired_leases");
        healthSnapshot.QueueDepth.Should().Be(1);
        healthSnapshot.RunningCount.Should().Be(1);
        healthSnapshot.RetryCount.Should().Be(1);
        healthSnapshot.FailedLast24Hours.Should().Be(1);

        var usage = await service.GetUsageAsync(_projectId, now.AddDays(-7), now.AddMinutes(1));
        var usageSnapshot = usage.Data!;
        usageSnapshot.AttemptCount.Should().Be(2);
        usageSnapshot.SucceededCount.Should().Be(1);
        usageSnapshot.FailedCount.Should().Be(1);
        usageSnapshot.EstimatedCostUsd.Should().Be(4m);
        usageSnapshot.CacheHitCount.Should().Be(1);

        var budget = await service.GetBudgetAsync(_projectId);
        var budgetSnapshot = budget.Data!;
        budgetSnapshot.DailyUsageUsd.Should().Be(1.25m);
        budgetSnapshot.MonthlyUsageUsd.Should().Be(4m);
        budgetSnapshot.WarningActive.Should().BeTrue();
        budgetSnapshot.HardStopActive.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidRangeMissingProjectAndAnonymousUser_ReturnStructuredFailures()
    {
        var service = CreateService();
        var now = DateTimeOffset.UtcNow;

        var range = await service.GetUsageAsync(null, now, now.AddMinutes(-1));
        range.StatusCode.Should().Be(400);
        range.ErrorCode.Should().Be(AiErrorCodes.InvalidRequest);

        var missing = await service.GetBudgetAsync(Guid.NewGuid());
        missing.StatusCode.Should().Be(404);
        missing.ErrorCode.Should().Be(AiErrorCodes.PermissionDenied);

        _currentUser.SetupGet(current => current.UserId).Returns((Guid?)null);
        (await service.GetHealthAsync()).StatusCode.Should().Be(403);
        (await service.GetUsageAsync(null, null, null)).StatusCode.Should().Be(403);
        (await service.GetBudgetAsync(Guid.NewGuid())).StatusCode.Should().Be(403);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private AiPlatformQueryService CreateService()
        => new(
            new GenericRepository<AiJob>(_db),
            new GenericRepository<AiJobDispatch>(_db),
            new GenericRepository<AiUsageLedger>(_db),
            new GenericRepository<AiBudgetPolicy>(_db),
            new GenericRepository<Project>(_db),
            new GenericRepository<ProjectMember>(_db),
            new GenericRepository<OrganizationMember>(_db),
            _currentUser.Object,
            _options.Object);

    private AiJob Job(Project project, string status, DateTimeOffset now)
        => new()
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            Project = project,
            RequestedById = _userId,
            JobType = "analysis",
            SourceType = "project",
            SchemaId = "analysis-v4",
            RequestHash = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = status,
            AvailableAt = now
        };

    private AiUsageLedger Usage(Guid jobId, DateTimeOffset createdAt, string status, decimal cost, bool cacheHit)
        => new()
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            UserId = _userId,
            AiJobId = jobId,
            JobType = "analysis",
            ProviderName = "test",
            ModelName = "test-model",
            InputTokens = 100,
            OutputTokens = 50,
            EstimatedCostUsd = cost,
            Status = status,
            CacheHit = cacheHit,
            CreatedAt = createdAt
        };
}
