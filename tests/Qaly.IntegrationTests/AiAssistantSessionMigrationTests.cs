using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiAssistantSessionMigrationTests
{
    private const string PreviousMigration = "20260801141320_P006AiActionComposerActivity";
    private const string SessionMigration = "20260802063926_P007AssistantSessionTurnChain";

    [Fact]
    [Trait("TestId", "TEST-AS-06")]
    public void P007Script_IsAdditiveAndEnforcesSessionTurnOrderingAndIdempotency()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QalyScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new QalyDbContext(options);

        var script = context.GetService<IMigrator>().GenerateScript(
            PreviousMigration,
            SessionMigration,
            MigrationsSqlGenerationOptions.Idempotent);

        script.Should().Contain("CREATE TABLE [AssistantSessions]");
        script.Should().Contain("CREATE TABLE [AssistantTurns]");
        script.Should().Contain("CREATE TABLE [AssistantProcessEvents]");
        script.Should().Contain("CREATE TABLE [AssistantArtifactRefs]");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_AssistantTurns_SessionId_Sequence]");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_AssistantTurns_SessionId_ClientTurnId]");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_AssistantTurns_SessionId_IdempotencyKey]");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_AssistantProcessEvents_TurnId_Sequence]");
        script.Should().NotContain("DROP TABLE");
        script.Should().NotContain("DROP COLUMN");
    }
}
