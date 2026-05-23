using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260524090000_AddWikiVisibilityAndDeliveryIdempotency")]
    public partial class AddWikiVisibilityAndDeliveryIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "WebhookDeliveryLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "WebhookDeliveryLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "WikiPages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess",
                table: "WebhookDeliveryLogs",
                columns: new[] { "WebhookId", "IdempotencyKey", "IsSuccess" });

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_ProjectId_IsPublic",
                table: "WikiPages",
                columns: new[] { "ProjectId", "IsPublic" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IdempotencyKey",
                table: "Notifications",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropIndex(
                name: "IX_WikiPages_ProjectId_IsPublic",
                table: "WikiPages");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IdempotencyKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "WikiPages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Notifications");
        }
    }
}
