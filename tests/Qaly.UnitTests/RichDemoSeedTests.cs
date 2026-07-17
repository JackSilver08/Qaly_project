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
}
