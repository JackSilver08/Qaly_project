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
            // No-op baseline migration: schema already exists in the database.
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
