using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create baseline tables if they do not exist
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
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[ProjectLabels]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ProjectLabels] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ProjectLabels] PRIMARY KEY DEFAULT NEWID(),
                        [Color] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectLabels_Color] DEFAULT N'#64748B',
                        [Name] nvarchar(80) NOT NULL,
                        [ProjectId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ProjectLabels_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;
                """);

            migrationBuilder.Sql(
                """
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
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[TaskLabels]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TaskLabels] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskLabels] PRIMARY KEY DEFAULT NEWID(),
                        [ProjectLabelId] uniqueidentifier NOT NULL,
                        [TaskItemId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskLabels_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Votes]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Votes] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Votes] PRIMARY KEY DEFAULT NEWID(),
                        [TargetId] uniqueidentifier NOT NULL,
                        [TargetType] nvarchar(20) NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [Value] int NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Votes_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;
                """);

            // 2. Add baseline columns to existing tables
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[Projects]', N'Code') IS NULL
                BEGIN
                    ALTER TABLE [Projects] ADD [Code] nvarchar(80) NULL;
                END;

                IF COL_LENGTH(N'[Projects]', N'LogoUrl') IS NULL
                BEGIN
                    ALTER TABLE [Projects] ADD [LogoUrl] nvarchar(1000) NULL;
                END;
                """);

            // 3. Populate default Code in separate batch
            migrationBuilder.Sql(
                """
                UPDATE [Projects]
                SET [Code] = CONCAT(N'project-', REPLACE(CONVERT(nvarchar(36), [Id]), N'-', N''))
                WHERE [Code] IS NULL OR LTRIM(RTRIM([Code])) = N'';
                """);

            // 4. Alter column and create indexes/FKs for Projects
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[Projects]')
                      AND name = N'Code'
                      AND is_nullable = 1
                )
                BEGIN
                    ALTER TABLE [Projects] ALTER COLUMN [Code] nvarchar(80) NOT NULL;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Projects_Code'
                      AND object_id = OBJECT_ID(N'[Projects]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects]([Code]);
                END;
                """);

            // 5. Add columns to TaskItems, TaskComments, TaskAttachments
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[TaskItems]', N'ImportSessionId') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [ImportSessionId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[TaskItems]', N'IsPinned') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [IsPinned] bit NOT NULL CONSTRAINT [DF_TaskItems_IsPinned] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[TaskItems]', N'ContributesToProgress') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [ContributesToProgress] bit NOT NULL CONSTRAINT [DF_TaskItems_ContributesToProgress] DEFAULT 1;
                END;

                IF COL_LENGTH(N'[TaskItems]', N'UpvoteCount') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [UpvoteCount] int NOT NULL CONSTRAINT [DF_TaskItems_UpvoteCount] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[TaskItems]', N'DownvoteCount') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [DownvoteCount] int NOT NULL CONSTRAINT [DF_TaskItems_DownvoteCount] DEFAULT 0;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[TaskComments]', N'ParentCommentId') IS NULL
                BEGIN
                    ALTER TABLE [TaskComments] ADD [ParentCommentId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[TaskComments]', N'UpvoteCount') IS NULL
                BEGIN
                    ALTER TABLE [TaskComments] ADD [UpvoteCount] int NOT NULL CONSTRAINT [DF_TaskComments_UpvoteCount] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[TaskComments]', N'DownvoteCount') IS NULL
                BEGIN
                    ALTER TABLE [TaskComments] ADD [DownvoteCount] int NOT NULL CONSTRAINT [DF_TaskComments_DownvoteCount] DEFAULT 0;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[TaskAttachments]', N'ProjectId') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [ProjectId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'CommentId') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [CommentId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'Scope') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [Scope] nvarchar(20) NOT NULL CONSTRAINT [DF_TaskAttachments_Scope] DEFAULT N'Task';
                END;
                """);

            // 6. Create Indexes & Foreign Keys
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_ProjectLabels_ProjectId_Name'
                      AND object_id = OBJECT_ID(N'[ProjectLabels]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_ProjectLabels_ProjectId_Name] ON [ProjectLabels]([ProjectId], [Name]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_ProjectLabels_Projects_ProjectId'
                )
                BEGIN
                    ALTER TABLE [ProjectLabels] ADD CONSTRAINT [FK_ProjectLabels_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAssignments_UserId'
                      AND object_id = OBJECT_ID(N'[TaskAssignments]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskAssignments_UserId] ON [TaskAssignments]([UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAssignments_TaskItemId_UserId'
                      AND object_id = OBJECT_ID(N'[TaskAssignments]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_TaskAssignments_TaskItemId_UserId] ON [TaskAssignments]([TaskItemId], [UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskAssignments_TaskItems_TaskItemId'
                )
                BEGIN
                    ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskAssignments_Users_UserId'
                )
                BEGIN
                    ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskLabels_ProjectLabelId'
                      AND object_id = OBJECT_ID(N'[TaskLabels]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskLabels_ProjectLabelId] ON [TaskLabels]([ProjectLabelId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskLabels_TaskItemId_ProjectLabelId'
                      AND object_id = OBJECT_ID(N'[TaskLabels]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_TaskLabels_TaskItemId_ProjectLabelId] ON [TaskLabels]([TaskItemId], [ProjectLabelId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskLabels_ProjectLabels_ProjectLabelId'
                )
                BEGIN
                    ALTER TABLE [TaskLabels] ADD CONSTRAINT [FK_TaskLabels_ProjectLabels_ProjectLabelId] FOREIGN KEY ([ProjectLabelId]) REFERENCES [ProjectLabels]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskLabels_TaskItems_TaskItemId'
                )
                BEGIN
                    ALTER TABLE [TaskLabels] ADD CONSTRAINT [FK_TaskLabels_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Votes_UserId'
                      AND object_id = OBJECT_ID(N'[Votes]')
                )
                BEGIN
                    CREATE INDEX [IX_Votes_UserId] ON [Votes]([UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Votes_TargetType_TargetId'
                      AND object_id = OBJECT_ID(N'[Votes]')
                )
                BEGIN
                    CREATE INDEX [IX_Votes_TargetType_TargetId] ON [Votes]([TargetType], [TargetId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Votes_TargetType_TargetId_UserId'
                      AND object_id = OBJECT_ID(N'[Votes]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Votes_TargetType_TargetId_UserId] ON [Votes]([TargetType], [TargetId], [UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Votes_Users_UserId'
                )
                BEGIN
                    ALTER TABLE [Votes] ADD CONSTRAINT [FK_Votes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_ImportSessions_Projects_ProjectId'
                )
                BEGIN
                    ALTER TABLE [ImportSessions]
                        ADD CONSTRAINT [FK_ImportSessions_Projects_ProjectId]
                        FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_ImportSessions_Users_UserId'
                )
                BEGIN
                    ALTER TABLE [ImportSessions]
                        ADD CONSTRAINT [FK_ImportSessions_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
                )
                BEGIN
                    ALTER TABLE [TaskItems]
                        ADD CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId]
                        FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_ImportSessions_ProjectId'
                      AND object_id = OBJECT_ID(N'[ImportSessions]')
                )
                BEGIN
                    CREATE INDEX [IX_ImportSessions_ProjectId] ON [ImportSessions]([ProjectId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_ImportSessions_UserId'
                      AND object_id = OBJECT_ID(N'[ImportSessions]')
                )
                BEGIN
                    CREATE INDEX [IX_ImportSessions_UserId] ON [ImportSessions]([UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskItems_ImportSessionId'
                      AND object_id = OBJECT_ID(N'[TaskItems]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskItems_ImportSessionId] ON [TaskItems]([ImportSessionId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Projects_Code'
                      AND object_id = OBJECT_ID(N'[Projects]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects]([Code]);
                END;
                """);
        }

        /// <inheritdoc />        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAttachments_Projects_ProjectId",
                table: "TaskAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAttachments_TaskComments_CommentId",
                table: "TaskAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_TaskComments_ParentCommentId",
                table: "TaskComments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_ImportSessions_ImportSessionId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "ImportSessions");

            migrationBuilder.DropTable(
                name: "TaskAssignments");

            migrationBuilder.DropTable(
                name: "TaskLabels");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "ProjectLabels");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ImportSessionId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_IsPinned",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_Status",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskComments_ParentCommentId",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_CommentId",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_ProjectId",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_Scope_CommentId",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_Scope_ProjectId",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_Scope_TaskItemId",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Code",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ContributesToProgress",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "DownvoteCount",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ImportSessionId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "UpvoteCount",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "DownvoteCount",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "UpvoteCount",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "CommentId",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Projects");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskItemId",
                table: "TaskAttachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_Status",
                table: "TaskItems",
                columns: new[] { "ProjectId", "Status" })
                .Annotation("SqlServer:Include", new[] { "Title", "Priority", "AssigneeId", "DueDate" });
        }
    }
}
