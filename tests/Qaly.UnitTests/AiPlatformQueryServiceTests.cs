using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
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
            WorkerEnabled = true,
            BudgetUiEnabled = true
        });
        _unitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _db.SaveChangesAsync(ct));
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
        var earlierThisMonth = now.AddDays(-1);
        var expectedDailyUsage = 1.25m;
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
            Usage(running.Id, now, "success", 1.25m, cacheHit: true),
            Usage(retrying.Id, earlierThisMonth, "failed", 2.75m, cacheHit: false));
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

        var usage = await service.GetUsageAsync(_tenantId, _projectId, now.AddDays(-7), now.AddMinutes(1));
        var usageSnapshot = usage.Data!;
        usageSnapshot.AttemptCount.Should().Be(2);
        usageSnapshot.SucceededCount.Should().Be(1);
        usageSnapshot.FailedCount.Should().Be(1);
        usageSnapshot.EstimatedCostUsd.Should().Be(4m);
        usageSnapshot.CacheHitCount.Should().Be(1);
        usageSnapshot.ByProvider.Should().ContainSingle(item => item.Key == "test");
        usageSnapshot.ByFunction.Should().ContainSingle(item => item.Key == "analysis");
        usageSnapshot.ByCache.Should().HaveCount(2);

        var budget = await service.GetBudgetAsync(_tenantId, _projectId);
        var budgetSnapshot = budget.Data!;
        budgetSnapshot.DailyUsageUsd.Should().Be(expectedDailyUsage);
        budgetSnapshot.MonthlyUsageUsd.Should().Be(4m);
        budgetSnapshot.WarningActive.Should().BeTrue();
        budgetSnapshot.HardStopActive.Should().BeTrue();

        var originalVersion = budgetSnapshot.Version;
        var updated = await service.UpdateBudgetAsync(_tenantId, _projectId, new UpdateAiBudgetPolicyDto(
            5m,
            50m,
            75,
            true,
            false,
            originalVersion,
            Confirmed: true));
        updated.IsSuccess.Should().BeTrue(updated.Error);
        updated.Data!.DailyBudgetUsd.Should().Be(5m);
        updated.Data.MonthlyBudgetUsd.Should().Be(50m);
        updated.Data.Version.Should().NotBe(originalVersion);
        _auditLog.Verify(audit => audit.LogAsync(
            "UpdateAiBudgetPolicy",
            nameof(AiBudgetPolicy),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var stale = await service.UpdateBudgetAsync(_tenantId, _projectId, new UpdateAiBudgetPolicyDto(
            6m,
            60m,
            80,
            true,
            false,
            originalVersion,
            Confirmed: true));
        stale.StatusCode.Should().Be(409);
        stale.ErrorCode.Should().Be(AiErrorCodes.BudgetPolicyConflict);
    }

    [Fact]
    public async Task InvalidRangeMissingProjectAndAnonymousUser_ReturnStructuredFailures()
    {
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            OrganizationId = _tenantId,
            OwnerId = _userId,
            Name = "Range Project",
            Code = "RANGE"
        });
        await _db.SaveChangesAsync();
        var service = CreateService();
        var now = DateTimeOffset.UtcNow;

        var range = await service.GetUsageAsync(_tenantId, _projectId, now, now.AddMinutes(-1));
        range.StatusCode.Should().Be(400);
        range.ErrorCode.Should().Be(AiErrorCodes.InvalidRequest);

        var missing = await service.GetBudgetAsync(null, Guid.NewGuid());
        missing.StatusCode.Should().Be(404);
        missing.ErrorCode.Should().Be(AiErrorCodes.PermissionDenied);

        _currentUser.SetupGet(current => current.UserId).Returns((Guid?)null);
        (await service.GetHealthAsync()).StatusCode.Should().Be(403);
        (await service.GetUsageAsync(null, null, null, null)).StatusCode.Should().Be(403);
        (await service.GetBudgetAsync(null, Guid.NewGuid())).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task BillingAdmin_SeesTenantAggregationAndMustExplicitlyConfirmPolicyChange()
    {
        _currentUser.SetupGet(current => current.Role).Returns("User");
        var ownerId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var currentUser = new User
        {
            Id = _userId,
            FullName = "Billing Admin",
            Email = "billing@qaly.test",
            PasswordHash = "test"
        };
        var owner = new User
        {
            Id = ownerId,
            FullName = "Organization Owner",
            Email = "owner@qaly.test",
            PasswordHash = "test"
        };
        var organization = new Organization
        {
            Id = _tenantId,
            Name = "Billing Organization",
            Code = "BILL",
            OwnerId = ownerId,
            Owner = owner
        };
        var firstProject = new Project
        {
            Id = _projectId,
            OrganizationId = _tenantId,
            OwnerId = ownerId,
            Owner = owner,
            Name = "First Project",
            Code = "BILL-1"
        };
        var secondProject = new Project
        {
            Id = secondProjectId,
            OrganizationId = _tenantId,
            OwnerId = ownerId,
            Owner = owner,
            Name = "Second Project",
            Code = "BILL-2"
        };
        _db.AddRange(currentUser, owner, organization, firstProject, secondProject);
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = _tenantId,
            Organization = organization,
            UserId = _userId,
            User = currentUser,
            Role = OrganizationRoleRules.BillingAdmin
        });
        _db.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = _tenantId,
            DailyBudgetUsd = 10m,
            MonthlyBudgetUsd = 20m,
            WarnAtPercent = 60,
            HardStopEnabled = true
        });
        _db.AiUsageLedger.AddRange(
            Usage(Guid.NewGuid(), DateTimeOffset.UtcNow, "succeeded", 3m, cacheHit: false),
            new AiUsageLedger
            {
                TenantId = _tenantId,
                ProjectId = secondProjectId,
                UserId = _userId,
                JobType = "sprint_progress_summary",
                ProviderName = "secondary",
                ModelName = "model",
                EstimatedCostUsd = 4m,
                Status = "succeeded",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var scopes = await service.GetBudgetScopesAsync();
        scopes.Data.Should().Contain(item => item.ScopeType == "organization" && item.ScopeId == _tenantId);
        scopes.Data.Should().Contain(item => item.ScopeType == "project" && item.ScopeId == secondProjectId);

        var usage = await service.GetUsageAsync(_tenantId, null, null, null);
        usage.Data!.AttemptCount.Should().Be(2);
        usage.Data.EffectiveCostUsd.Should().Be(7m);

        var inherited = await service.GetBudgetAsync(_tenantId, _projectId);
        inherited.Data!.IsInherited.Should().BeTrue();
        inherited.Data.PolicySource.Should().Be("organization");
        inherited.Data.DailyUsageUsd.Should().Be(7m);

        var withoutConfirmation = await service.UpdateBudgetAsync(
            _tenantId,
            null,
            new UpdateAiBudgetPolicyDto(12m, 24m, 70, true, false, inherited.Data.EffectiveVersion));
        withoutConfirmation.ErrorCode.Should().Be(AiErrorCodes.BudgetConfirmationRequired);

        var organizationBudget = await service.GetBudgetAsync(_tenantId, null);
        var updated = await service.UpdateBudgetAsync(
            _tenantId,
            null,
            new UpdateAiBudgetPolicyDto(
                12m,
                24m,
                70,
                true,
                false,
                organizationBudget.Data!.Version,
                Confirmed: true));
        updated.IsSuccess.Should().BeTrue(updated.Error);
        updated.Data!.DailyBudgetUsd.Should().Be(12m);
        updated.Data.MonthlyUsageUsd.Should().Be(7m);
    }

    [Fact]
    public async Task BudgetEditingFeatureDisabled_PreservesReadOnlySnapshotAndRejectsMutation()
    {
        _options.SetupGet(monitor => monitor.CurrentValue).Returns(new AiJobPlatformOptions
        {
            Enabled = true,
            WorkerEnabled = true,
            BudgetUiEnabled = false
        });
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            OrganizationId = _tenantId,
            OwnerId = _userId,
            Name = "Read-only Budget",
            Code = "READONLY"
        });
        _db.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            DailyBudgetUsd = 2m,
            MonthlyBudgetUsd = 20m
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var snapshot = await service.GetBudgetAsync(_tenantId, _projectId);
        snapshot.Data!.EditingEnabled.Should().BeFalse();

        var update = await service.UpdateBudgetAsync(
            _tenantId,
            _projectId,
            new UpdateAiBudgetPolicyDto(
                3m,
                30m,
                80,
                true,
                false,
                snapshot.Data.Version,
                Confirmed: true));
        update.StatusCode.Should().Be(503);
        update.ErrorCode.Should().Be(AiErrorCodes.PlatformDisabled);
        (await _db.AiBudgetPolicies.SingleAsync(item => item.ProjectId == _projectId))
            .DailyBudgetUsd.Should().Be(2m);
        _auditLog.Verify(audit => audit.LogAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            new GenericRepository<Organization>(_db),
            new GenericRepository<Project>(_db),
            new GenericRepository<ProjectMember>(_db),
            new GenericRepository<OrganizationMember>(_db),
            _currentUser.Object,
            _options.Object,
            _unitOfWork.Object,
            _auditLog.Object);

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
