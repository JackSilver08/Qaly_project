using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class G401_AddGroupPollModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupPollOptions_GroupPolls_GroupPollId",
                table: "GroupPollOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPolls_WorkGroups_WorkGroupId",
                table: "GroupPolls");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPollVotes_GroupPollOptions_GroupPollOptionId",
                table: "GroupPollVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPollVotes_GroupPolls_GroupPollId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPollVotes_GroupPollId_GroupPollOptionId_UserId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPollVotes_GroupPollId_UserId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPolls_WorkGroupId_Status_CreatedAt",
                table: "GroupPolls");

            migrationBuilder.RenameColumn(
                name: "GroupPollId",
                table: "GroupPollVotes",
                newName: "PollId");

            migrationBuilder.RenameColumn(
                name: "GroupPollOptionId",
                table: "GroupPollVotes",
                newName: "OptionId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPollVotes_GroupPollOptionId",
                table: "GroupPollVotes",
                newName: "IX_GroupPollVotes_OptionId");

            migrationBuilder.RenameColumn(
                name: "WorkGroupId",
                table: "GroupPolls",
                newName: "GroupId");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "GroupPolls",
                newName: "ExpiredAt");

            migrationBuilder.RenameColumn(
                name: "Text",
                table: "GroupPollOptions",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "GroupPollId",
                table: "GroupPollOptions",
                newName: "PollId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPollOptions_GroupPollId_SortOrder",
                table: "GroupPollOptions",
                newName: "IX_GroupPollOptions_PollId_SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPollVotes_PollId_OptionId_UserId",
                table: "GroupPollVotes",
                columns: new[] { "PollId", "OptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupPollVotes_PollId_UserId",
                table: "GroupPollVotes",
                columns: new[] { "PollId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPolls_GroupId",
                table: "GroupPolls",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPollOptions_PollId",
                table: "GroupPollOptions",
                column: "PollId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPollOptions_GroupPolls_PollId",
                table: "GroupPollOptions",
                column: "PollId",
                principalTable: "GroupPolls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPolls_WorkGroups_GroupId",
                table: "GroupPolls",
                column: "GroupId",
                principalTable: "WorkGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPollVotes_GroupPollOptions_OptionId",
                table: "GroupPollVotes",
                column: "OptionId",
                principalTable: "GroupPollOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPollVotes_GroupPolls_PollId",
                table: "GroupPollVotes",
                column: "PollId",
                principalTable: "GroupPolls",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupPollOptions_GroupPolls_PollId",
                table: "GroupPollOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPolls_WorkGroups_GroupId",
                table: "GroupPolls");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPollVotes_GroupPollOptions_OptionId",
                table: "GroupPollVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPollVotes_GroupPolls_PollId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPollVotes_OptionId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPollVotes_PollId_OptionId_UserId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPollVotes_PollId_UserId",
                table: "GroupPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GroupPolls_GroupId",
                table: "GroupPolls");

            migrationBuilder.DropIndex(
                name: "IX_GroupPollOptions_PollId",
                table: "GroupPollOptions");

            migrationBuilder.RenameColumn(
                name: "OptionId",
                table: "GroupPollVotes",
                newName: "GroupPollOptionId");

            migrationBuilder.RenameColumn(
                name: "PollId",
                table: "GroupPollVotes",
                newName: "GroupPollId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPollVotes_OptionId",
                table: "GroupPollVotes",
                newName: "IX_GroupPollVotes_GroupPollOptionId");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "GroupPolls",
                newName: "WorkGroupId");

            migrationBuilder.RenameColumn(
                name: "ExpiredAt",
                table: "GroupPolls",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "PollId",
                table: "GroupPollOptions",
                newName: "GroupPollId");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "GroupPollOptions",
                newName: "Text");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPollOptions_PollId_SortOrder",
                table: "GroupPollOptions",
                newName: "IX_GroupPollOptions_GroupPollId_SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPollVotes_GroupPollId_GroupPollOptionId_UserId",
                table: "GroupPollVotes",
                columns: new[] { "GroupPollId", "GroupPollOptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupPollVotes_GroupPollId_UserId",
                table: "GroupPollVotes",
                columns: new[] { "GroupPollId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPolls_WorkGroupId_Status_CreatedAt",
                table: "GroupPolls",
                columns: new[] { "WorkGroupId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPollOptions_GroupPolls_GroupPollId",
                table: "GroupPollOptions",
                column: "GroupPollId",
                principalTable: "GroupPolls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPolls_WorkGroups_WorkGroupId",
                table: "GroupPolls",
                column: "WorkGroupId",
                principalTable: "WorkGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPollVotes_GroupPollOptions_GroupPollOptionId",
                table: "GroupPollVotes",
                column: "GroupPollOptionId",
                principalTable: "GroupPollOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPollVotes_GroupPolls_GroupPollId",
                table: "GroupPollVotes",
                column: "GroupPollId",
                principalTable: "GroupPolls",
                principalColumn: "Id");
        }
    }
}
