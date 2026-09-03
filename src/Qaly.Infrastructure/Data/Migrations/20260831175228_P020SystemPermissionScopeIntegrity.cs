using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P020SystemPermissionScopeIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemModulePermissions_SystemRole_ModuleKey",
                table: "SystemModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_SystemModulePermissions_UserId_ModuleKey",
                table: "SystemModulePermissions");

            // Repair records that the previous OR-based update contract could persist before
            // adding the one-scope and uniqueness constraints. Where duplicates exist, retain the
            // most restrictive record so migration can never widen access accidentally.
            migrationBuilder.Sql("""
                DELETE FROM [SystemModulePermissions]
                WHERE [UserId] IS NULL AND [SystemRole] IS NULL;

                UPDATE [SystemModulePermissions]
                SET [SystemRole] = NULL
                WHERE [UserId] IS NOT NULL AND [SystemRole] IS NOT NULL;

                UPDATE [SystemModulePermissions]
                SET [AiTier] = N'Restricted'
                WHERE [IsAllowed] = 0
                   OR [AiTier] NOT IN (N'Full', N'SummaryOnly', N'Restricted');

                ;WITH [RankedUserPermissions] AS (
                    SELECT [Id], ROW_NUMBER() OVER (
                        PARTITION BY [UserId], [ModuleKey]
                        ORDER BY [IsAllowed] ASC,
                            CASE [AiTier] WHEN N'Restricted' THEN 0 WHEN N'SummaryOnly' THEN 1 ELSE 2 END ASC,
                            [CreatedAt] DESC,
                            [Id] DESC) AS [RowNumber]
                    FROM [SystemModulePermissions]
                    WHERE [UserId] IS NOT NULL AND [SystemRole] IS NULL
                )
                DELETE FROM [RankedUserPermissions] WHERE [RowNumber] > 1;

                ;WITH [RankedRolePermissions] AS (
                    SELECT [Id], ROW_NUMBER() OVER (
                        PARTITION BY [SystemRole], [ModuleKey]
                        ORDER BY [IsAllowed] ASC,
                            CASE [AiTier] WHEN N'Restricted' THEN 0 WHEN N'SummaryOnly' THEN 1 ELSE 2 END ASC,
                            [CreatedAt] DESC,
                            [Id] DESC) AS [RowNumber]
                    FROM [SystemModulePermissions]
                    WHERE [UserId] IS NULL AND [SystemRole] IS NOT NULL
                )
                DELETE FROM [RankedRolePermissions] WHERE [RowNumber] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SystemModulePermissions_SystemRole_ModuleKey",
                table: "SystemModulePermissions",
                columns: new[] { "SystemRole", "ModuleKey" },
                unique: true,
                filter: "[SystemRole] IS NOT NULL AND [UserId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemModulePermissions_UserId_ModuleKey",
                table: "SystemModulePermissions",
                columns: new[] { "UserId", "ModuleKey" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [SystemRole] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SystemModulePermissions_ExactlyOneScope",
                table: "SystemModulePermissions",
                sql: "([UserId] IS NOT NULL AND [SystemRole] IS NULL) OR ([UserId] IS NULL AND [SystemRole] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemModulePermissions_SystemRole_ModuleKey",
                table: "SystemModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_SystemModulePermissions_UserId_ModuleKey",
                table: "SystemModulePermissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SystemModulePermissions_ExactlyOneScope",
                table: "SystemModulePermissions");

            migrationBuilder.CreateIndex(
                name: "IX_SystemModulePermissions_SystemRole_ModuleKey",
                table: "SystemModulePermissions",
                columns: new[] { "SystemRole", "ModuleKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemModulePermissions_UserId_ModuleKey",
                table: "SystemModulePermissions",
                columns: new[] { "UserId", "ModuleKey" });
        }
    }
}
