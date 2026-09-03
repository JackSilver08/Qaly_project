using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P018ProjectLaunchTaskTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectLaunchTaskTraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TaskItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLaunchBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ObjectiveMetricIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceRefsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLaunchTaskTraces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchTaskTraces_ProjectLaunchBriefs_ProjectLaunchBriefId",
                        column: x => x.ProjectLaunchBriefId,
                        principalTable: "ProjectLaunchBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchTaskTraces_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchTaskTraces_ProjectLaunchBriefId_FeatureId",
                table: "ProjectLaunchTaskTraces",
                columns: new[] { "ProjectLaunchBriefId", "FeatureId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchTaskTraces_TaskItemId",
                table: "ProjectLaunchTaskTraces",
                column: "TaskItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectLaunchTaskTraces");
        }
    }
}
