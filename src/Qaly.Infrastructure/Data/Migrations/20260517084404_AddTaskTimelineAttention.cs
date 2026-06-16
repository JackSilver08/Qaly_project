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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssignedAt",
                table: "TaskAssignments",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedByUserId",
                table: "TaskAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanNudgeAssignee",
                table: "ProjectMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewProjectTimeline",
                table: "ProjectMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewTaskRisk",
                table: "ProjectMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewUnseenTaskSignal",
                table: "ProjectMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TaskViewEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TaskItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    ViewCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskViewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskViewEvents_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskViewEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedAt",
                table: "TaskAssignments",
                column: "AssignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedByUserId",
                table: "TaskAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskViewEvents_TaskItemId_UserId",
                table: "TaskViewEvents",
                columns: new[] { "TaskItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskViewEvents_UserId",
                table: "TaskViewEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskViewEvents_ViewedAt",
                table: "TaskViewEvents",
                column: "ViewedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Users_AssignedByUserId",
                table: "TaskAssignments",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
