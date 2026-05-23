using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskItemRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TaskItems",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_Status_SortOrder",
                table: "TaskItems",
                columns: new[] { "ProjectId", "Status", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_Status_SortOrder",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TaskItems");
        }
    }
}
