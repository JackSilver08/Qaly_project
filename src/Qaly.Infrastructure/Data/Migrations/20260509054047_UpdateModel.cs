using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskComments_TaskItemId",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_Status",
                table: "TaskItems",
                columns: new[] { "ProjectId", "Status" })
                .Annotation("SqlServer:Include", new[] { "Title", "Priority", "AssigneeId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskItemId_CreatedAt",
                table: "TaskComments",
                columns: new[] { "TaskItemId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" },
                filter: "[IsRead] = 0")
                .Annotation("SqlServer:Include", new[] { "CreatedAt", "Message" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_Status",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskComments_TaskItemId_CreatedAt",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskItemId",
                table: "TaskComments",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });
        }
    }
}
