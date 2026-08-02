using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class PortfolioScheduleMigrationTests
{
    private const string PreviousMigration = "20260802114055_P008MemberSkillEvidence";
    private const string CapacityMigration = "20260802124551_P009PortfolioCapacity";

    [Fact]
    [Trait("TestId", "TEST-PORTFOLIO-SCHEDULE-07")]
    public void P009Script_IsAdditiveAndConstrainsCapacityWindows()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QalyScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new QalyDbContext(options);

        var script = context.GetService<IMigrator>().GenerateScript(
            PreviousMigration,
            CapacityMigration,
            MigrationsSqlGenerationOptions.Idempotent);

        script.Should().Contain("CREATE TABLE [OrganizationMemberCapacityProfiles]");
        script.Should().Contain("CREATE TABLE [MemberAvailabilityWindows]");
        script.Should().Contain("CREATE UNIQUE INDEX [IX_OrganizationMemberCapacityProfiles_OrganizationId_UserId]");
        script.Should().Contain("FOREIGN KEY ([OrganizationMemberCapacityProfileId]) REFERENCES [OrganizationMemberCapacityProfiles] ([Id]) ON DELETE CASCADE");
        script.Should().NotContain("DROP TABLE");
        script.Should().NotContain("DROP COLUMN");
    }
}
