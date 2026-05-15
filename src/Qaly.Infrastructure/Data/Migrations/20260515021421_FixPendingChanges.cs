using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_Status",
                table: "TaskItems");

            migrationBuilder.AddColumn<bool>(
                name: "ContributesToProgress",
                table: "TaskItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DownvoteCount",
                table: "TaskItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportSessionId",
                table: "TaskItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "TaskItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UpvoteCount",
                table: "TaskItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DownvoteCount",
                table: "TaskComments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "TaskComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpvoteCount",
                table: "TaskComments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskItemId",
                table: "TaskAttachments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CommentId",
                table: "TaskAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "TaskAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "TaskAttachments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Task");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Projects",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Projects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    IsUndone = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportSessions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "#64748B"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLabels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TaskItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TargetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TaskItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLabelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskLabels_ProjectLabels_ProjectLabelId",
                        column: x => x.ProjectLabelId,
                        principalTable: "ProjectLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskLabels_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ImportSessionId",
                table: "TaskItems",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_IsPinned",
                table: "TaskItems",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_Status",
                table: "TaskItems",
                columns: new[] { "ProjectId", "Status" })
                .Annotation("SqlServer:Include", new[] { "Title", "Priority", "AssigneeId", "DueDate", "IsPinned", "ContributesToProgress" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_ParentCommentId",
                table: "TaskComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_CommentId",
                table: "TaskAttachments",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_ProjectId",
                table: "TaskAttachments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_Scope_CommentId",
                table: "TaskAttachments",
                columns: new[] { "Scope", "CommentId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_Scope_ProjectId",
                table: "TaskAttachments",
                columns: new[] { "Scope", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_Scope_TaskItemId",
                table: "TaskAttachments",
                columns: new[] { "Scope", "TaskItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_ProjectId",
                table: "ImportSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_UserId",
                table: "ImportSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLabels_ProjectId_Name",
                table: "ProjectLabels",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_TaskItemId_UserId",
                table: "TaskAssignments",
                columns: new[] { "TaskItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_UserId",
                table: "TaskAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskLabels_ProjectLabelId",
                table: "TaskLabels",
                column: "ProjectLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskLabels_TaskItemId_ProjectLabelId",
                table: "TaskLabels",
                columns: new[] { "TaskItemId", "ProjectLabelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_TargetType_TargetId",
                table: "Votes",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_TargetType_TargetId_UserId",
                table: "Votes",
                columns: new[] { "TargetType", "TargetId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId",
                table: "Votes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAttachments_Projects_ProjectId",
                table: "TaskAttachments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAttachments_TaskComments_CommentId",
                table: "TaskAttachments",
                column: "CommentId",
                principalTable: "TaskComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_TaskComments_ParentCommentId",
                table: "TaskComments",
                column: "ParentCommentId",
                principalTable: "TaskComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_ImportSessions_ImportSessionId",
                table: "TaskItems",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
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
