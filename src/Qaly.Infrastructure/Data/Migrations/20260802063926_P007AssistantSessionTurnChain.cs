using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P007AssistantSessionTurnChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastSequence = table.Column<int>(type: "int", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantSessions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssistantSessions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ClientTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserMessage = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    RequestContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Intent = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ExecutionPolicy = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AssistantResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceRefsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelProfile = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ActualProvider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ActualModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SafeErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantTurns_AssistantSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AssistantSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantArtifactRefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RendererId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantArtifactRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantArtifactRefs_AssistantTurns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "AssistantTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantProcessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PublicLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SafeDetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Retryable = table.Column<bool>(type: "bit", nullable: false),
                    SafeErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantProcessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantProcessEvents_AssistantTurns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "AssistantTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantArtifactRefs_AiJobId",
                table: "AssistantArtifactRefs",
                column: "AiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantArtifactRefs_DraftId",
                table: "AssistantArtifactRefs",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantArtifactRefs_TurnId_SchemaId",
                table: "AssistantArtifactRefs",
                columns: new[] { "TurnId", "SchemaId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantProcessEvents_TurnId_Sequence",
                table: "AssistantProcessEvents",
                columns: new[] { "TurnId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantSessions_OwnerUserId_Status_UpdatedAt",
                table: "AssistantSessions",
                columns: new[] { "OwnerUserId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantSessions_ProjectId",
                table: "AssistantSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantSessions_TenantId_OwnerUserId",
                table: "AssistantSessions",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurns_SessionId_ClientTurnId",
                table: "AssistantTurns",
                columns: new[] { "SessionId", "ClientTurnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurns_SessionId_CreatedAt",
                table: "AssistantTurns",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurns_SessionId_IdempotencyKey",
                table: "AssistantTurns",
                columns: new[] { "SessionId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurns_SessionId_Sequence",
                table: "AssistantTurns",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantArtifactRefs");

            migrationBuilder.DropTable(
                name: "AssistantProcessEvents");

            migrationBuilder.DropTable(
                name: "AssistantTurns");

            migrationBuilder.DropTable(
                name: "AssistantSessions");
        }
    }
}
