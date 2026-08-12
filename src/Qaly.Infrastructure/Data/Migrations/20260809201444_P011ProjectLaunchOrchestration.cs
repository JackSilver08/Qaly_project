using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P011ProjectLaunchOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectLaunchPlanArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLaunchBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssistantSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssistantTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StaffingScenariosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryPlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockingReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceVersionHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SelectedScenarioId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ScoringVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActualProvider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActualModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLaunchPlanArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchPlanArtifacts_AssistantSessions_AssistantSessionId",
                        column: x => x.AssistantSessionId,
                        principalTable: "AssistantSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchPlanArtifacts_AssistantTurns_AssistantTurnId",
                        column: x => x.AssistantTurnId,
                        principalTable: "AssistantTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchPlanArtifacts_OrganizationWorkRuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "OrganizationWorkRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchPlanArtifacts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchPlanArtifacts_ProjectLaunchBriefs_ProjectLaunchBriefId",
                        column: x => x.ProjectLaunchBriefId,
                        principalTable: "ProjectLaunchBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLaunchExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLaunchPlanArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReceiptJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MonitoringEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NextMonitorAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastMonitoredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RolledBackAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RollbackReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RollbackIdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RollbackPayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLaunchExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchExecutions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchExecutions_ProjectLaunchPlanArtifacts_ProjectLaunchPlanArtifactId",
                        column: x => x.ProjectLaunchPlanArtifactId,
                        principalTable: "ProjectLaunchPlanArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLaunchExecutions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReplanProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLaunchPlanArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLaunchExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TriggerCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProposalJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaselineHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CurrentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReplanProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReplanProposals_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectReplanProposals_ProjectLaunchExecutions_ProjectLaunchExecutionId",
                        column: x => x.ProjectLaunchExecutionId,
                        principalTable: "ProjectLaunchExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectReplanProposals_ProjectLaunchPlanArtifacts_ProjectLaunchPlanArtifactId",
                        column: x => x.ProjectLaunchPlanArtifactId,
                        principalTable: "ProjectLaunchPlanArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectReplanProposals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchExecutions_IdempotencyKey",
                table: "ProjectLaunchExecutions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchExecutions_MonitoringEnabled_NextMonitorAt",
                table: "ProjectLaunchExecutions",
                columns: new[] { "MonitoringEnabled", "NextMonitorAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchExecutions_OrganizationId",
                table: "ProjectLaunchExecutions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchExecutions_ProjectId",
                table: "ProjectLaunchExecutions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchExecutions_ProjectLaunchPlanArtifactId",
                table: "ProjectLaunchExecutions",
                column: "ProjectLaunchPlanArtifactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchExecutions_RollbackIdempotencyKey",
                table: "ProjectLaunchExecutions",
                column: "RollbackIdempotencyKey",
                unique: true,
                filter: "[RollbackIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchPlanArtifacts_AssistantSessionId",
                table: "ProjectLaunchPlanArtifacts",
                column: "AssistantSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchPlanArtifacts_AssistantTurnId",
                table: "ProjectLaunchPlanArtifacts",
                column: "AssistantTurnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchPlanArtifacts_OrganizationId_CreatedAt",
                table: "ProjectLaunchPlanArtifacts",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchPlanArtifacts_ProjectLaunchBriefId_Revision",
                table: "ProjectLaunchPlanArtifacts",
                columns: new[] { "ProjectLaunchBriefId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLaunchPlanArtifacts_RuleSetId",
                table: "ProjectLaunchPlanArtifacts",
                column: "RuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReplanProposals_OrganizationId",
                table: "ProjectReplanProposals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReplanProposals_ProjectId",
                table: "ProjectReplanProposals",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReplanProposals_ProjectLaunchExecutionId_CreatedAt",
                table: "ProjectReplanProposals",
                columns: new[] { "ProjectLaunchExecutionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReplanProposals_ProjectLaunchPlanArtifactId_Revision",
                table: "ProjectReplanProposals",
                columns: new[] { "ProjectLaunchPlanArtifactId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectReplanProposals");

            migrationBuilder.DropTable(
                name: "ProjectLaunchExecutions");

            migrationBuilder.DropTable(
                name: "ProjectLaunchPlanArtifacts");
        }
    }
}
