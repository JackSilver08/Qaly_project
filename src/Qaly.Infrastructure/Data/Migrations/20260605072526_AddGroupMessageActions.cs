using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupMessageActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "GroupMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinnedAt",
                table: "GroupMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedByUserId",
                table: "GroupMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupMessageUserStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    GroupMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HiddenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMessageUserStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMessageUserStates_GroupMessages_GroupMessageId",
                        column: x => x.GroupMessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMessageUserStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_WorkGroupId_IsPinned",
                table: "GroupMessages",
                columns: new[] { "WorkGroupId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageUserStates_GroupMessageId_UserId",
                table: "GroupMessageUserStates",
                columns: new[] { "GroupMessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageUserStates_UserId_HiddenAt",
                table: "GroupMessageUserStates",
                columns: new[] { "UserId", "HiddenAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupMessageUserStates");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_WorkGroupId_IsPinned",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "PinnedByUserId",
                table: "GroupMessages");
        }
    }
}
