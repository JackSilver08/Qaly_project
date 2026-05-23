using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_DueDate",
                table: "TaskItems",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_AssigneeId_Status",
                table: "TaskItems",
                columns: new[] { "ProjectId", "AssigneeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_DueDate",
                table: "TaskItems",
                columns: new[] { "ProjectId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_StartDate",
                table: "TaskItems",
                column: "StartDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_DueDate",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_AssigneeId_Status",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_DueDate",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_StartDate",
                table: "TaskItems");
        }
    }
}
