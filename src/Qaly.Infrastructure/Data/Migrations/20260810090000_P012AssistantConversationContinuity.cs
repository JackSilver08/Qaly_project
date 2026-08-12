using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations;

[DbContext(typeof(QalyDbContext))]
[Migration("20260810090000_P012AssistantConversationContinuity")]
public sealed class P012AssistantConversationContinuity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClarificationDraftJson",
            table: "AssistantSessions",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClarificationDraftJson",
            table: "AssistantSessions");
    }
}
