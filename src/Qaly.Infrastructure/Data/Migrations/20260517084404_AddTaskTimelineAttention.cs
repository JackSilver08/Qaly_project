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
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_ImportSessions_ImportSessionId",
                table: "TaskItems");

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

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_ImportSessions_ImportSessionId",
                table: "TaskItems",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Users_AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_ImportSessions_ImportSessionId",
                table: "TaskItems");

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
