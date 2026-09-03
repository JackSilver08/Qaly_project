using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P021ProfessionalCapabilityProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessionalProfileDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsSystemSeed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalProfileDefinitions", x => x.Id);
                    table.CheckConstraint("CK_ProfessionalProfileDefinitions_KeyName", "LEN([Key]) >= 2 AND LEN([Name]) >= 2 AND LEN([Category]) >= 2");
                    table.ForeignKey(
                        name: "FK_ProfessionalProfileDefinitions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMemberProfessionalProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfessionalProfileDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Proficiency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMemberProfessionalProfiles", x => x.Id);
                    table.CheckConstraint("CK_OrganizationMemberProfessionalProfiles_EffectiveWindow", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                    table.CheckConstraint("CK_OrganizationMemberProfessionalProfiles_Proficiency", "[Proficiency] IN (N'Foundation', N'Practitioner', N'Proficient', N'Expert')");
                    table.CheckConstraint("CK_OrganizationMemberProfessionalProfiles_Source", "[Source] IN (N'MemberDeclared', N'ManagerConfirmed', N'Imported')");
                    table.CheckConstraint("CK_OrganizationMemberProfessionalProfiles_VerificationStatus", "[VerificationStatus] IN (N'Declared', N'Verified', N'Rejected')");
                    table.CheckConstraint("CK_OrganizationMemberProfessionalProfiles_Verifier", "([VerificationStatus] <> N'Verified') OR ([VerifiedByUserId] IS NOT NULL AND [VerifiedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_OrganizationMemberProfessionalProfiles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberProfessionalProfiles_ProfessionalProfileDefinitions_ProfessionalProfileDefinitionId",
                        column: x => x.ProfessionalProfileDefinitionId,
                        principalTable: "ProfessionalProfileDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberProfessionalProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberProfessionalProfiles_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberProfessionalProfiles_OrganizationId_UserId_ProfessionalProfileDefinitionId",
                table: "OrganizationMemberProfessionalProfiles",
                columns: new[] { "OrganizationId", "UserId", "ProfessionalProfileDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberProfessionalProfiles_OrganizationId_UserId_VerificationStatus",
                table: "OrganizationMemberProfessionalProfiles",
                columns: new[] { "OrganizationId", "UserId", "VerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberProfessionalProfiles_ProfessionalProfileDefinitionId",
                table: "OrganizationMemberProfessionalProfiles",
                column: "ProfessionalProfileDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberProfessionalProfiles_UserId",
                table: "OrganizationMemberProfessionalProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberProfessionalProfiles_VerifiedByUserId",
                table: "OrganizationMemberProfessionalProfiles",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalProfileDefinitions_OrganizationId_IsActive_Category",
                table: "ProfessionalProfileDefinitions",
                columns: new[] { "OrganizationId", "IsActive", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalProfileDefinitions_OrganizationId_Key",
                table: "ProfessionalProfileDefinitions",
                columns: new[] { "OrganizationId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationMemberProfessionalProfiles");

            migrationBuilder.DropTable(
                name: "ProfessionalProfileDefinitions");
        }
    }
}
