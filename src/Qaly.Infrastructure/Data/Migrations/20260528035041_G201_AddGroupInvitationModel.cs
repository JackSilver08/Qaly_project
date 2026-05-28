using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class G201AddGroupInvitationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvitations_Users_InvitedByUserId",
                table: "GroupInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvitations_Users_InvitedUserId",
                table: "GroupInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvitations_WorkGroups_WorkGroupId",
                table: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_InvitedByUserId",
                table: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_InvitedUserId",
                table: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_WorkGroupId_Email_Status",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "InvitedUserId",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "GroupInvitations");

            migrationBuilder.RenameColumn(
                name: "WorkGroupId",
                table: "GroupInvitations",
                newName: "GroupId");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "GroupInvitations",
                newName: "ExpiredAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_GroupId",
                table: "GroupInvitations",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvitations_WorkGroups_GroupId",
                table: "GroupInvitations",
                column: "GroupId",
                principalTable: "WorkGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvitations_WorkGroups_GroupId",
                table: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvitations_GroupId",
                table: "GroupInvitations");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "GroupInvitations",
                newName: "WorkGroupId");

            migrationBuilder.RenameColumn(
                name: "ExpiredAt",
                table: "GroupInvitations",
                newName: "ExpiresAt");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "GroupInvitations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "GroupInvitations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedUserId",
                table: "GroupInvitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAt",
                table: "GroupInvitations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "GroupInvitations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InvitedByUserId",
                table: "GroupInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_InvitedUserId",
                table: "GroupInvitations",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_WorkGroupId_Email_Status",
                table: "GroupInvitations",
                columns: new[] { "WorkGroupId", "Email", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvitations_Users_InvitedByUserId",
                table: "GroupInvitations",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvitations_Users_InvitedUserId",
                table: "GroupInvitations",
                column: "InvitedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvitations_WorkGroups_WorkGroupId",
                table: "GroupInvitations",
                column: "WorkGroupId",
                principalTable: "WorkGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
