using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P010AiNativeReadLoopConversationLaunchBrief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAt",
                table: "AssistantTurns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPayloadJson",
                table: "AssistantTurns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResumedFromTurnId",
                table: "AssistantTurns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationWorkRuleSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EffectiveUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationWorkRuleSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationWorkRuleSets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLaunchBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssistantSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssistantTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BriefJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActualProvider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActualModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RowRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLaunchBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchBriefs_AssistantSessions_AssistantSessionId",
                        column: x => x.AssistantSessionId,
                        principalTable: "AssistantSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchBriefs_AssistantTurns_AssistantTurnId",
                        column: x => x.AssistantTurnId,
                        principalTable: "AssistantTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchBriefs_OrganizationWorkRuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "OrganizationWorkRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchBriefs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationWorkRuleDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLaunchBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleSetVersion = table.Column<int>(type: "int", nullable: true),
                    RuleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DeterministicFactsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionEligible = table.Column<bool>(type: "bit", nullable: false),
                    SourceFreshness = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationWorkRuleDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationWorkRuleDecisions_ProjectLaunchBriefs_ProjectLaunchBriefId",
                        column: x => x.ProjectLaunchBriefId,
                        principalTable: "ProjectLaunchBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkRuleDecisions_ProjectLaunchBriefId_RuleKey",
                table: "OrganizationWorkRuleDecisions",
                columns: new[] { "ProjectLaunchBriefId", "RuleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkRuleSets_OrganizationId_Status_EffectiveFrom",
                table: "OrganizationWorkRuleSets",
                columns: new[] { "OrganizationId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWorkRuleSets_OrganizationId_Version",
                table: "OrganizationWorkRuleSets",
                columns: new[] { "OrganizationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchBriefs_AssistantSessionId",
                table: "ProjectLaunchBriefs",
                column: "AssistantSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchBriefs_AssistantTurnId",
                table: "ProjectLaunchBriefs",
                column: "AssistantTurnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchBriefs_OrganizationId_CreatedAt",
                table: "ProjectLaunchBriefs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchBriefs_RuleSetId",
                table: "ProjectLaunchBriefs",
                column: "RuleSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationWorkRuleDecisions");

            migrationBuilder.DropTable(
                name: "ProjectLaunchBriefs");

            migrationBuilder.DropTable(
                name: "OrganizationWorkRuleSets");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "RequestPayloadJson",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "ResumedFromTurnId",
                table: "AssistantTurns");
        }
    }
}
