using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P019ProjectLaunchSemanticIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "TaskItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TaskAcceptanceChecklistItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "acceptance");

            migrationBuilder.AddColumn<string>(
                name: "SprintClientId",
                table: "ProjectLaunchTaskTraces",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskClientId",
                table: "ProjectLaunchTaskTraces",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [ProjectLaunchTaskTraces] " +
                "SET [TaskClientId] = CONVERT(nvarchar(36), [TaskItemId]), " +
                "[SprintClientId] = CONCAT('legacy-', CONVERT(nvarchar(36), [TaskItemId])) " +
                "WHERE [TaskClientId] = '';");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ReviewerId",
                table: "TaskItems",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAcceptanceChecklistItems_TaskId_Kind",
                table: "TaskAcceptanceChecklistItems",
                columns: new[] { "TaskId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchTaskTraces_ProjectLaunchBriefId_TaskClientId",
                table: "ProjectLaunchTaskTraces",
                columns: new[] { "ProjectLaunchBriefId", "TaskClientId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_Users_ReviewerId",
                table: "TaskItems",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_Users_ReviewerId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ReviewerId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskAcceptanceChecklistItems_TaskId_Kind",
                table: "TaskAcceptanceChecklistItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectLaunchTaskTraces_ProjectLaunchBriefId_TaskClientId",
                table: "ProjectLaunchTaskTraces");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TaskAcceptanceChecklistItems");

            migrationBuilder.DropColumn(
                name: "SprintClientId",
                table: "ProjectLaunchTaskTraces");

            migrationBuilder.DropColumn(
                name: "TaskClientId",
                table: "ProjectLaunchTaskTraces");
        }
    }
}
