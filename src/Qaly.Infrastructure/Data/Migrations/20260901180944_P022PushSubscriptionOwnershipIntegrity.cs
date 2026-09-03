using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P022PushSubscriptionOwnershipIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [PushSubscriptions]
                WHERE [Endpoint] IS NULL
                   OR LEN(LTRIM(RTRIM([Endpoint]))) = 0
                   OR DATALENGTH(LTRIM(RTRIM([Endpoint]))) > 4096
                   OR LEFT(LTRIM(RTRIM([Endpoint])), 8) COLLATE Latin1_General_100_CI_AS <> N'https://'
                   OR [P256dh] IS NULL
                   OR LEN(LTRIM(RTRIM([P256dh]))) = 0
                   OR DATALENGTH(LTRIM(RTRIM([P256dh]))) > 2048
                   OR [Auth] IS NULL
                   OR LEN(LTRIM(RTRIM([Auth]))) = 0
                   OR DATALENGTH(LTRIM(RTRIM([Auth]))) > 2048;

                UPDATE [PushSubscriptions]
                SET [Endpoint] = LTRIM(RTRIM([Endpoint])),
                    [P256dh] = LTRIM(RTRIM([P256dh])),
                    [Auth] = LTRIM(RTRIM([Auth])),
                    [Device] = LEFT(COALESCE(NULLIF(LTRIM(RTRIM([Device])), N''), N'Unknown'), 200);

                ;WITH [RankedSubscriptions] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [Endpoint]
                               ORDER BY [LastUsedAt] DESC, [CreatedAt] DESC, [Id]
                           ) AS [DuplicateRank]
                    FROM [PushSubscriptions]
                )
                DELETE FROM [RankedSubscriptions]
                WHERE [DuplicateRank] > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "P256dh",
                table: "PushSubscriptions",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Endpoint",
                table: "PushSubscriptions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Device",
                table: "PushSubscriptions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Auth",
                table: "PushSubscriptions",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EndpointHash",
                table: "PushSubscriptions",
                type: "binary(32)",
                nullable: true,
                computedColumnSql: "CONVERT(binary(32), HASHBYTES('SHA2_256', [Endpoint]))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_EndpointHash",
                table: "PushSubscriptions",
                column: "EndpointHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_EndpointHash",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "EndpointHash",
                table: "PushSubscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "P256dh",
                table: "PushSubscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "Endpoint",
                table: "PushSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "Device",
                table: "PushSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Auth",
                table: "PushSubscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);
        }
    }
}
