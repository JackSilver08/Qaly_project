using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiActionComposerMigrationTests
{
    private const string PreviousMigration = "20260726224810_P005TaskSkillTaxonomy";
    private const string ActionMigration = "20260801141320_P006AiActionComposerActivity";

    [Fact]
    [Trait("TestId", "TEST-ACTION-10")]
    public void P006Script_IsAdditiveAndEnforcesOrderedActivityPerJob()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QalyScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new QalyDbContext(options);

        var script = context.GetService<IMigrator>().GenerateScript(
            PreviousMigration,
            ActionMigration,
            MigrationsSqlGenerationOptions.Idempotent);

        script.Should().Contain("CREATE TABLE [AiJobActivityEvents]");
        script.Should().Contain("FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]) ON DELETE CASCADE");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_AiJobActivityEvents_AiJobId_Sequence]");
        script.Should().Contain("CREATE INDEX [IX_AiJobActivityEvents_AiJobId_CreatedAt]");
        script.Should().NotContain("DROP TABLE");
        script.Should().NotContain("DROP COLUMN");
    }
}
