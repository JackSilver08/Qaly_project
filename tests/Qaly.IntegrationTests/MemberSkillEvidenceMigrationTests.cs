using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class MemberSkillEvidenceMigrationTests
{
    private const string PreviousMigration = "20260802063926_P007AssistantSessionTurnChain";
    private const string EvidenceMigration = "20260802114055_P008MemberSkillEvidence";

    [Fact]
    [Trait("TestId", "TEST-SKILL-EVIDENCE-06")]
    public void P008Script_IsAdditiveAndProtectsOneAttributionPerTaskContributor()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QalyScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new QalyDbContext(options);

        var script = context.GetService<IMigrator>().GenerateScript(
            PreviousMigration,
            EvidenceMigration,
            MigrationsSqlGenerationOptions.Idempotent);

        script.Should().Contain("CREATE TABLE [TaskCompletionAttributions]");
        script.Should().Contain("FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_TaskCompletionAttributions_TaskItemId_ContributorUserId]");
        script.Should().Contain("CREATE INDEX [IX_TaskCompletionAttributions_ContributorUserId_Status]");
        script.Should().NotContain("DROP TABLE");
        script.Should().NotContain("DROP COLUMN");
    }
}
