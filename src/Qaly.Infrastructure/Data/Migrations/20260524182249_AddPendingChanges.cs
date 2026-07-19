using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WikiPages_ProjectId",
                table: "WikiPages");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "WikiPages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "WikiPages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "internal");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "WebhookDeliveryLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "WebhookDeliveryLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[TaskItems]', N'SprintId') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [SprintId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tone",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Sprint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Planning"),
                    Goal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprint_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_ProjectId_Visibility",
                table: "WikiPages",
                columns: new[] { "ProjectId", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess",
                table: "WebhookDeliveryLogs",
                columns: new[] { "WebhookId", "IdempotencyKey", "IsSuccess" });

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskItems_ProjectId_SprintId_Status'
                      AND object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskItems_ProjectId_SprintId_Status]
                    ON [TaskItems] ([ProjectId], [SprintId], [Status]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskItems_SprintId'
                      AND object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskItems_SprintId]
                    ON [TaskItems] ([SprintId]);
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IdempotencyKey",
                table: "Notifications",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_ProjectId",
                table: "Sprint",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_ProjectId_Status",
                table: "Sprint",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_Status",
                table: "Sprint",
                column: "Status");

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskItems_Sprint_SprintId'
                      AND parent_object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    ALTER TABLE [TaskItems]
                    ADD CONSTRAINT [FK_TaskItems_Sprint_SprintId]
                    FOREIGN KEY ([SprintId]) REFERENCES [Sprint] ([Id]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskItems_Sprint_SprintId'
                      AND parent_object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    ALTER TABLE [TaskItems] DROP CONSTRAINT [FK_TaskItems_Sprint_SprintId];
                END
                """);

            migrationBuilder.DropTable(
                name: "Sprint");

            migrationBuilder.DropIndex(
                name: "IX_WikiPages_ProjectId_Visibility",
                table: "WikiPages");

            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess",
                table: "WebhookDeliveryLogs");

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskItems_ProjectId_SprintId_Status'
                      AND object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    DROP INDEX [IX_TaskItems_ProjectId_SprintId_Status] ON [TaskItems];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskItems_SprintId'
                      AND object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    DROP INDEX [IX_TaskItems_SprintId] ON [TaskItems];
                END
                """);

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IdempotencyKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "WikiPages");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "WikiPages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "WebhookDeliveryLogs");

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[TaskItems]', N'SprintId') IS NOT NULL
                BEGIN
                    ALTER TABLE [TaskItems] DROP COLUMN [SprintId];
                END
                """);

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Tone",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_ProjectId",
                table: "WikiPages",
                column: "ProjectId");
        }
    }
}
