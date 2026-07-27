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
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_WikiPages_ProjectId'
                      AND object_id = OBJECT_ID(N'[WikiPages]')
                )
                BEGIN
                    DROP INDEX [IX_WikiPages_ProjectId] ON [WikiPages];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WikiPages]', N'IsPublic') IS NULL
                BEGIN
                    ALTER TABLE [WikiPages] ADD [IsPublic] bit NOT NULL CONSTRAINT [DF_WikiPages_IsPublic] DEFAULT 0;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WikiPages]', N'Visibility') IS NULL
                BEGIN
                    ALTER TABLE [WikiPages] ADD [Visibility] nvarchar(50) NOT NULL CONSTRAINT [DF_WikiPages_Visibility] DEFAULT N'internal';
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WebhookDeliveryLogs]', N'AttemptCount') IS NULL
                BEGIN
                    ALTER TABLE [WebhookDeliveryLogs] ADD [AttemptCount] int NOT NULL CONSTRAINT [DF_WebhookDeliveryLogs_AttemptCount] DEFAULT 1;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WebhookDeliveryLogs]', N'IdempotencyKey') IS NULL
                BEGIN
                    ALTER TABLE [WebhookDeliveryLogs] ADD [IdempotencyKey] nvarchar(200) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[TaskItems]', N'SprintId') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [SprintId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[Notifications]', N'IdempotencyKey') IS NULL
                BEGIN
                    ALTER TABLE [Notifications] ADD [IdempotencyKey] nvarchar(200) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[Notifications]', N'Tone') IS NULL
                BEGIN
                    ALTER TABLE [Notifications] ADD [Tone] nvarchar(max) NOT NULL CONSTRAINT [DF_Notifications_Tone] DEFAULT N'';
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Sprint]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Sprint] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_Sprint_Id] DEFAULT NEWID(),
                        [Name] nvarchar(200) NOT NULL,
                        [StartDate] datetimeoffset NOT NULL,
                        [EndDate] datetimeoffset NOT NULL,
                        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Sprint_Status] DEFAULT N'Planning',
                        [Goal] nvarchar(1000) NULL,
                        [ProjectId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Sprint_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL,
                        CONSTRAINT [PK_Sprint] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Sprint_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_WikiPages_ProjectId_Visibility'
                      AND object_id = OBJECT_ID(N'[WikiPages]')
                )
                BEGIN
                    CREATE INDEX [IX_WikiPages_ProjectId_Visibility]
                    ON [WikiPages] ([ProjectId], [Visibility]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess'
                      AND object_id = OBJECT_ID(N'[WebhookDeliveryLogs]')
                )
                BEGIN
                    CREATE INDEX [IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess]
                    ON [WebhookDeliveryLogs] ([WebhookId], [IdempotencyKey], [IsSuccess]);
                END
                """);

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

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Notifications_UserId_IdempotencyKey'
                      AND object_id = OBJECT_ID(N'[Notifications]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Notifications_UserId_IdempotencyKey]
                    ON [Notifications] ([UserId], [IdempotencyKey])
                    WHERE [IdempotencyKey] IS NOT NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Sprint_ProjectId'
                      AND object_id = OBJECT_ID(N'[Sprint]')
                )
                BEGIN
                    CREATE INDEX [IX_Sprint_ProjectId]
                    ON [Sprint] ([ProjectId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Sprint_ProjectId_Status'
                      AND object_id = OBJECT_ID(N'[Sprint]')
                )
                BEGIN
                    CREATE INDEX [IX_Sprint_ProjectId_Status]
                    ON [Sprint] ([ProjectId], [Status]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Sprint_Status'
                      AND object_id = OBJECT_ID(N'[Sprint]')
                )
                BEGIN
                    CREATE INDEX [IX_Sprint_Status]
                    ON [Sprint] ([Status]);
                END
                """);

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Sprint]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [Sprint];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_WikiPages_ProjectId_Visibility'
                      AND object_id = OBJECT_ID(N'[WikiPages]')
                )
                BEGIN
                    DROP INDEX [IX_WikiPages_ProjectId_Visibility] ON [WikiPages];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess'
                      AND object_id = OBJECT_ID(N'[WebhookDeliveryLogs]')
                )
                BEGIN
                    DROP INDEX [IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess] ON [WebhookDeliveryLogs];
                END
                """);

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

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Notifications_UserId_IdempotencyKey'
                      AND object_id = OBJECT_ID(N'[Notifications]')
                )
                BEGIN
                    DROP INDEX [IX_Notifications_UserId_IdempotencyKey] ON [Notifications];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WikiPages]', N'IsPublic') IS NOT NULL
                BEGIN
                    ALTER TABLE [WikiPages] DROP COLUMN [IsPublic];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WikiPages]', N'Visibility') IS NOT NULL
                BEGIN
                    ALTER TABLE [WikiPages] DROP COLUMN [Visibility];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WebhookDeliveryLogs]', N'AttemptCount') IS NOT NULL
                BEGIN
                    ALTER TABLE [WebhookDeliveryLogs] DROP COLUMN [AttemptCount];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[WebhookDeliveryLogs]', N'IdempotencyKey') IS NOT NULL
                BEGIN
                    ALTER TABLE [WebhookDeliveryLogs] DROP COLUMN [IdempotencyKey];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[TaskItems]', N'SprintId') IS NOT NULL
                BEGIN
                    ALTER TABLE [TaskItems] DROP COLUMN [SprintId];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[Notifications]', N'IdempotencyKey') IS NOT NULL
                BEGIN
                    ALTER TABLE [Notifications] DROP COLUMN [IdempotencyKey];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[Notifications]', N'Tone') IS NOT NULL
                BEGIN
                    ALTER TABLE [Notifications] DROP COLUMN [Tone];
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_WikiPages_ProjectId'
                      AND object_id = OBJECT_ID(N'[WikiPages]')
                )
                BEGIN
                    CREATE INDEX [IX_WikiPages_ProjectId]
                    ON [WikiPages] ([ProjectId]);
                END
                """);
        }
    }
}
