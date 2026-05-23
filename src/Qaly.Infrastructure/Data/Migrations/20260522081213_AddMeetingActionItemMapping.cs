using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingActionItemMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'TaskItems', N'RowVersion') IS NULL
                BEGIN
                    ALTER TABLE [TaskItems] ADD [RowVersion] rowversion NOT NULL;
                END
                """);

            migrationBuilder.CreateTable(
                name: "MeetingActionItemMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    MeetingImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionItemIndex = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourcePriority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceDueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceQuote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingActionItemMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingActionItemMappings_MeetingImports_MeetingImportId",
                        column: x => x.MeetingImportId,
                        principalTable: "MeetingImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingActionItemMappings_TaskItems_TaskId",
                        column: x => x.TaskId,
                        principalTable: "TaskItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingActionItemMappings_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActionItemMappings_CreatedById",
                table: "MeetingActionItemMappings",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActionItemMappings_MeetingImportId_ActionItemIndex",
                table: "MeetingActionItemMappings",
                columns: new[] { "MeetingImportId", "ActionItemIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActionItemMappings_TaskId",
                table: "MeetingActionItemMappings",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingActionItemMappings");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "TaskItems",
                type: "rowversion",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);
        }
    }
}
