using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations;

[DbContext(typeof(QalyDbContext))]
[Migration("20260903120000_P025GitHubWebhookInboxLeaseReliability")]
public sealed class P025GitHubWebhookInboxLeaseReliability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GitHubWebhookInbox_Status",
            table: "GitHubWebhookInbox");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LeaseExpiresAt",
            table: "GitHubWebhookInbox",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LeaseOwner",
            table: "GitHubWebhookInbox",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextAttemptAt",
            table: "GitHubWebhookInbox",
            type: "datetimeoffset",
            nullable: false,
            defaultValueSql: "SYSDATETIMEOFFSET()");

        migrationBuilder.CreateIndex(
            name: "IX_GitHubWebhookInbox_Status_NextAttemptAt_LeaseExpiresAt_ReceivedAt",
            table: "GitHubWebhookInbox",
            columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "ReceivedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GitHubWebhookInbox_Status_NextAttemptAt_LeaseExpiresAt_ReceivedAt",
            table: "GitHubWebhookInbox");

        migrationBuilder.DropColumn(
            name: "LeaseExpiresAt",
            table: "GitHubWebhookInbox");

        migrationBuilder.DropColumn(
            name: "LeaseOwner",
            table: "GitHubWebhookInbox");

        migrationBuilder.DropColumn(
            name: "NextAttemptAt",
            table: "GitHubWebhookInbox");

        migrationBuilder.CreateIndex(
            name: "IX_GitHubWebhookInbox_Status",
            table: "GitHubWebhookInbox",
            column: "Status");
    }
}
