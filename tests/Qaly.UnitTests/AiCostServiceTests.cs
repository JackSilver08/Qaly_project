using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiCostServiceTests : IDisposable
{
    private readonly QalyDbContext _db;

    public AiCostServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    [Fact]
    public async Task TenantPolicy_HardStopUsesUsageAcrossAllTenantProjects()
    {
        var tenantId = Guid.NewGuid();
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        _db.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = tenantId,
            DailyBudgetUsd = 5m,
            MonthlyBudgetUsd = 10m,
            HardStopEnabled = true
        });
        _db.AiUsageLedger.AddRange(
            Usage(tenantId, firstProjectId, 3m),
            Usage(tenantId, secondProjectId, 3m));
        await _db.SaveChangesAsync();

        var available = await new AiCostService(_db)
            .EnsureBudgetAvailableAsync(tenantId, firstProjectId);

        available.Should().BeFalse();
    }

    [Fact]
    public async Task ProjectPolicy_OverridesTenantPolicyAndUsesProjectUsageOnly()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _db.AiBudgetPolicies.AddRange(
            new AiBudgetPolicy
            {
                TenantId = tenantId,
                DailyBudgetUsd = 1m,
                MonthlyBudgetUsd = 1m,
                HardStopEnabled = true
            },
            new AiBudgetPolicy
            {
                TenantId = tenantId,
                ProjectId = projectId,
                DailyBudgetUsd = 10m,
                MonthlyBudgetUsd = 20m,
                HardStopEnabled = true
            });
        _db.AiUsageLedger.Add(Usage(tenantId, projectId, 2m));
        await _db.SaveChangesAsync();

        var available = await new AiCostService(_db)
            .EnsureBudgetAvailableAsync(tenantId, projectId);

        available.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static AiUsageLedger Usage(Guid tenantId, Guid projectId, decimal cost)
        => new()
        {
            TenantId = tenantId,
            ProjectId = projectId,
            JobType = "project_progress_summary",
            ProviderName = "test",
            ModelName = "test",
            EstimatedCostUsd = cost,
            Status = "succeeded",
            CreatedAt = DateTimeOffset.UtcNow
        };
}
