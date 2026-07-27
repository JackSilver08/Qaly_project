using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P004AiBudgetPolicyIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AiBudgetPolicies_ProjectId",
                table: "AiBudgetPolicies",
                column: "ProjectId",
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiBudgetPolicies_TenantId",
                table: "AiBudgetPolicies",
                column: "TenantId",
                unique: true,
                filter: "[ProjectId] IS NULL AND [TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiBudgetPolicies_ProjectId",
                table: "AiBudgetPolicies");

            migrationBuilder.DropIndex(
                name: "IX_AiBudgetPolicies_TenantId",
                table: "AiBudgetPolicies");
        }
    }
}
