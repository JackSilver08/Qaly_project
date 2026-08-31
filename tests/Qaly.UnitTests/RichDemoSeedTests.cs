using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Seeds;

namespace Qaly.UnitTests;

public sealed class RichDemoSeedTests
{
    [Fact]
    public async Task SeedAsync_CreatesOnlyCanonicalAiJobLifecycleData()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Seed:UseRichDemoSeed"] = "true",
                ["Seed:AdminPassword"] = "QalyDemoAdmin!2026",
                ["Seed:DefaultUserPassword"] = "QalyDemoUser!2026"
            })
            .Build();
        var seeder = new DataSeeder(db, configuration, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();

        var jobs = await db.AiJobs
            .Include(job => job.Dispatch)
            .Include(job => job.Sources)
            .OrderBy(job => job.CreatedAt)
            .ToListAsync();
        var legacyQueue = await db.AiJobQueue.ToListAsync();

        jobs.Should().HaveCount(2);
        legacyQueue.Should().BeEmpty();
        jobs.Should().OnlyContain(job =>
            !string.IsNullOrWhiteSpace(job.JobType) &&
            !string.IsNullOrWhiteSpace(job.SchemaId) &&
            !string.IsNullOrWhiteSpace(job.SchemaVersion) &&
            !string.IsNullOrWhiteSpace(job.RequestHash) &&
            !string.IsNullOrWhiteSpace(job.IdempotencyKey) &&
            !string.IsNullOrWhiteSpace(job.CacheKey));
        jobs.Should().OnlyContain(job => job.Dispatch != null && job.Sources.Count > 0);
        jobs.Should().ContainSingle(job =>
            job.Status == AiJobStatuses.Succeeded &&
            job.Dispatch!.CompletedAt != null);
        jobs.Should().ContainSingle(job =>
            job.Status == AiJobStatuses.Queued &&
            job.Dispatch!.CompletedAt == null);
    }

    [Fact]
    public async Task SeedAsync_CreatesIdempotentAiNativeEvidenceForAllFiveActiveProjects()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Seed:UseRichDemoSeed"] = "true",
                ["Seed:AdminPassword"] = "QalyDemoAdmin!2026",
                ["Seed:DefaultUserPassword"] = "QalyDemoUser!2026"
            })
            .Build();
        var seeder = new DataSeeder(db, configuration, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();

        var organization = await db.Organizations.SingleAsync(item => item.Code == "qaly-demo-2026");
        var activeProjectIds = await db.Projects
            .Where(item => item.OrganizationId == organization.Id && item.Status == "Active")
            .Select(item => item.Id)
            .ToListAsync();
        var evidenceProjectIds = await db.TaskCompletionAttributions
            .Where(item => item.Status == TaskCompletionAttribution.Confirmed)
            .Select(item => item.TaskItem.ProjectId)
            .Distinct()
            .ToListAsync();
        var requirementProjectIds = await db.TaskSkillRequirements
            .Select(item => item.TaskItem.ProjectId)
            .Distinct()
            .ToListAsync();

        activeProjectIds.Should().HaveCount(5);
        evidenceProjectIds.Should().BeEquivalentTo(activeProjectIds);
        requirementProjectIds.Should().Contain(activeProjectIds);
        var skillCatalog = await db.OrganizationSkills
            .Where(item => item.OrganizationId == organization.Id)
            .ToListAsync();
        skillCatalog.Should().HaveCount(17);
        skillCatalog.Should().OnlyContain(item => item.IsSystemSeed &&
            !string.IsNullOrWhiteSpace(item.Category) &&
            !string.IsNullOrWhiteSpace(item.DefaultRequiredLevel) &&
            item.AliasesJson != "[]");
        skillCatalog.Should().Contain(item => item.NormalizedName == "project-product-management" && item.Category == "Quản lý sản phẩm và dự án");
        skillCatalog.Should().Contain(item => item.NormalizedName == "security-auth-privacy" && item.Category == "Bảo mật và tuân thủ");
        skillCatalog.Should().Contain(item => item.NormalizedName == "communication-leadership" && item.Category == "Giao tiếp và lãnh đạo");
        (await db.TaskSkillRequirements.CountAsync()).Should().BeGreaterThanOrEqualTo(59);
        (await db.TaskCompletionAttributions.CountAsync(item => item.Status == TaskCompletionAttribution.Confirmed)).Should().BeGreaterThanOrEqualTo(18);
        var activeMemberIds = await db.OrganizationMembers
            .Where(item => item.OrganizationId == organization.Id)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync();
        var contributorsWithConfirmedEvidence = await db.TaskCompletionAttributions
            .Where(item => item.Status == TaskCompletionAttribution.Confirmed &&
                           item.TaskItem.Project.OrganizationId == organization.Id &&
                           item.TaskItem.SkillRequirements.Any())
            .Select(item => item.ContributorUserId)
            .Distinct()
            .ToListAsync();
        contributorsWithConfirmedEvidence.Should().Contain(activeMemberIds,
            "mọi thành viên demo cần ít nhất một Task canonical đã hoàn thành để Project Launch có thể kiểm tra skill evidence");
        (await db.TaskItems.CountAsync(item => item.Title.StartsWith("Evidence:") && item.Status == "Done"))
            .Should().Be(11);
        (await db.OrganizationMemberCapacityProfiles.CountAsync(item => item.OrganizationId == organization.Id)).Should().Be(12);
        (await db.MemberAvailabilityWindows.CountAsync()).Should().Be(5);
        (await db.OrganizationWorkRuleSets.CountAsync(item =>
            item.OrganizationId == organization.Id && item.Status == "active")).Should().Be(1);
        (await db.TaskItems.CountAsync(item => item.Title == "Xác nhận dữ liệu POS Wave 1" && item.Status == "Done")).Should().Be(1);
        (await db.WikiPages.CountAsync(item => item.Project.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(3);
        (await db.GroupPolls.CountAsync(item => item.Group.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.GroupMeetingSessions.CountAsync(item => item.WorkGroup.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.MeetingImports.CountAsync(item => item.Project.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.GitHubPullRequests.CountAsync(item => item.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(2);
        (await db.GitHubReleases.CountAsync(item => item.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.ProjectDigestSubscriptions.CountAsync(item => item.Project.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);

        var countsBeforeSecondSeed = new
        {
            Skills = await db.OrganizationSkills.CountAsync(),
            Requirements = await db.TaskSkillRequirements.CountAsync(),
            Attributions = await db.TaskCompletionAttributions.CountAsync(),
            Profiles = await db.OrganizationMemberCapacityProfiles.CountAsync(),
            Availability = await db.MemberAvailabilityWindows.CountAsync(),
            Tasks = await db.TaskItems.CountAsync()
        };

        await seeder.SeedAsync();

        var countsAfterSecondSeed = new
        {
            Skills = await db.OrganizationSkills.CountAsync(),
            Requirements = await db.TaskSkillRequirements.CountAsync(),
            Attributions = await db.TaskCompletionAttributions.CountAsync(),
            Profiles = await db.OrganizationMemberCapacityProfiles.CountAsync(),
            Availability = await db.MemberAvailabilityWindows.CountAsync(),
            Tasks = await db.TaskItems.CountAsync()
        };
        countsAfterSecondSeed.Should().BeEquivalentTo(countsBeforeSecondSeed);
    }
}
