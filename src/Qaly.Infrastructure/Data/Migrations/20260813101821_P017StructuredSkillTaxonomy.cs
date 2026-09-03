using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P017StructuredSkillTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AliasesJson",
                table: "OrganizationSkills",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "OrganizationSkills",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Chuyên môn");

            migrationBuilder.AddColumn<string>(
                name: "DefaultRequiredLevel",
                table: "OrganizationSkills",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Intermediate");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemSeed",
                table: "OrganizationSkills",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSkills_OrganizationId_Category",
                table: "OrganizationSkills",
                columns: new[] { "OrganizationId", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationSkills_OrganizationId_Category",
                table: "OrganizationSkills");

            migrationBuilder.DropColumn(
                name: "AliasesJson",
                table: "OrganizationSkills");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "OrganizationSkills");

            migrationBuilder.DropColumn(
                name: "DefaultRequiredLevel",
                table: "OrganizationSkills");

            migrationBuilder.DropColumn(
                name: "IsSystemSeed",
                table: "OrganizationSkills");
        }
    }
}
