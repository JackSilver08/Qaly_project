using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetilyMeetingImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceProvider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MeetingStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TranscriptText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParticipantsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingImports_AiGeneratedDrafts_AiDraftId",
                        column: x => x.AiDraftId,
                        principalTable: "AiGeneratedDrafts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingImports_AiJobs_AiJobId",
                        column: x => x.AiJobId,
                        principalTable: "AiJobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingImports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingImports_Users_ImportedById",
                        column: x => x.ImportedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_AiDraftId",
                table: "MeetingImports",
                column: "AiDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_AiJobId",
                table: "MeetingImports",
                column: "AiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_ImportedById",
                table: "MeetingImports",
                column: "ImportedById");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_ProjectId_SourceProvider_SourceHash",
                table: "MeetingImports",
                columns: new[] { "ProjectId", "SourceProvider", "SourceHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingImports");
        }
    }
}
