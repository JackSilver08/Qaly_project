using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubWorkflowRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitHubWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunExternalId = table.Column<long>(type: "bigint", nullable: false),
                    WorkflowName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Conclusion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubWorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubWorkflowRuns_GitHubRepositoryConnections_RepositoryConnectionId",
                        column: x => x.RepositoryConnectionId,
                        principalTable: "GitHubRepositoryConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubWorkflowRuns_OrganizationId",
                table: "GitHubWorkflowRuns",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubWorkflowRuns_RepositoryConnectionId_RunExternalId",
                table: "GitHubWorkflowRuns",
                columns: new[] { "RepositoryConnectionId", "RunExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubWorkflowRuns_Status_Conclusion",
                table: "GitHubWorkflowRuns",
                columns: new[] { "Status", "Conclusion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubWorkflowRuns");
        }
    }
}
