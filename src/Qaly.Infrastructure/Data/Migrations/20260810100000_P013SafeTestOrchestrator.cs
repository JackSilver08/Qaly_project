using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P013SafeTestOrchestrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantTestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    EventsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SafeSummary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    SafeErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantTestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantTestRuns_AssistantSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AssistantSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantTestRuns_AssistantTurns_OriginTurnId",
                        column: x => x.OriginTurnId,
                        principalTable: "AssistantTurns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssistantTestRuns_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTestRuns_IdempotencyKey",
                table: "AssistantTestRuns",
                column: "IdempotencyKey",
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTestRuns_OriginTurnId",
                table: "AssistantTestRuns",
                column: "OriginTurnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTestRuns_OwnerUserId_CreatedAt",
                table: "AssistantTestRuns",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTestRuns_SessionId",
                table: "AssistantTestRuns",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantTestRuns");
        }
    }
}
