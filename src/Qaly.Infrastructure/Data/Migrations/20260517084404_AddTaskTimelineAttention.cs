using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTimelineAttention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[ImportSessions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ImportSessions] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ImportSessions] PRIMARY KEY DEFAULT NEWID(),
                        [ProjectId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [FileName] nvarchar(256) NOT NULL,
                        [TotalRows] int NOT NULL CONSTRAINT [DF_ImportSessions_TotalRows] DEFAULT 0,
                        [ImportedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_ImportedCount] DEFAULT 0,
                        [SkippedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_SkippedCount] DEFAULT 0,
                        [IsUndone] bit NOT NULL CONSTRAINT [DF_ImportSessions_IsUndone] DEFAULT 0,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ImportSessions_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF OBJECT_ID(N'[TaskAssignments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TaskAssignments] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskAssignments] PRIMARY KEY DEFAULT NEWID(),
                        [TaskItemId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF COL_LENGTH(N'[TaskItems]', N'ImportSessionId') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [ImportSessionId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[TaskAssignments]', N'AssignedAt') IS NULL
                BEGIN
                    ALTER TABLE [TaskAssignments] ADD [AssignedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_AssignedAt] DEFAULT SYSDATETIMEOFFSET();
                END;

                IF COL_LENGTH(N'[TaskAssignments]', N'AssignedByUserId') IS NULL
                BEGIN
                    ALTER TABLE [TaskAssignments] ADD [AssignedByUserId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[ProjectMembers]', N'CanNudgeAssignee') IS NULL
                BEGIN
                    ALTER TABLE [ProjectMembers] ADD [CanNudgeAssignee] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanNudgeAssignee] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[ProjectMembers]', N'CanViewProjectTimeline') IS NULL
                BEGIN
                    ALTER TABLE [ProjectMembers] ADD [CanViewProjectTimeline] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewProjectTimeline] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[ProjectMembers]', N'CanViewTaskRisk') IS NULL
                BEGIN
                    ALTER TABLE [ProjectMembers] ADD [CanViewTaskRisk] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewTaskRisk] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[ProjectMembers]', N'CanViewUnseenTaskSignal') IS NULL
                BEGIN
                    ALTER TABLE [ProjectMembers] ADD [CanViewUnseenTaskSignal] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewUnseenTaskSignal] DEFAULT 0;
                END;

                IF OBJECT_ID(N'[TaskViewEvents]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TaskViewEvents] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskViewEvents] PRIMARY KEY DEFAULT NEWID(),
                        [TaskItemId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [ViewedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskViewEvents_ViewedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [ViewCount] int NOT NULL CONSTRAINT [DF_TaskViewEvents_ViewCount] DEFAULT 1,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskViewEvents_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
                      AND parent_object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    ALTER TABLE [TaskItems] DROP CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId];
                END
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAssignments_AssignedAt'
                      AND object_id = OBJECT_ID(N'[TaskAssignments]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskAssignments_AssignedAt] ON [TaskAssignments]([AssignedAt]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAssignments_AssignedByUserId'
                      AND object_id = OBJECT_ID(N'[TaskAssignments]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskAssignments_AssignedByUserId] ON [TaskAssignments]([AssignedByUserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskViewEvents_TaskItemId_UserId'
                      AND object_id = OBJECT_ID(N'[TaskViewEvents]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_TaskViewEvents_TaskItemId_UserId]
                    ON [TaskViewEvents]([TaskItemId], [UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskViewEvents_UserId'
                      AND object_id = OBJECT_ID(N'[TaskViewEvents]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskViewEvents_UserId] ON [TaskViewEvents]([UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskViewEvents_ViewedAt'
                      AND object_id = OBJECT_ID(N'[TaskViewEvents]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskViewEvents_ViewedAt] ON [TaskViewEvents]([ViewedAt]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskAssignments_Users_AssignedByUserId'
                )
                BEGIN
                    ALTER TABLE [TaskAssignments]
                        ADD CONSTRAINT [FK_TaskAssignments_Users_AssignedByUserId]
                        FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users]([Id]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskViewEvents_TaskItems_TaskItemId'
                )
                BEGIN
                    ALTER TABLE [TaskViewEvents]
                        ADD CONSTRAINT [FK_TaskViewEvents_TaskItems_TaskItemId]
                        FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskViewEvents_Users_UserId'
                )
                BEGIN
                    ALTER TABLE [TaskViewEvents]
                        ADD CONSTRAINT [FK_TaskViewEvents_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
                      AND parent_object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    ALTER TABLE [TaskItems]
                        ADD CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId]
                        FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions]([Id]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Users_AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
                      AND parent_object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    ALTER TABLE [TaskItems] DROP CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId];
                END
                """);

            migrationBuilder.DropTable(
                name: "TaskViewEvents");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AssignedAt",
                table: "TaskAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "CanNudgeAssignee",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "CanViewProjectTimeline",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "CanViewTaskRisk",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "CanViewUnseenTaskSignal",
                table: "ProjectMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_ImportSessions_ImportSessionId",
                table: "TaskItems",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
