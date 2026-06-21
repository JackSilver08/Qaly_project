using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalFileForCAS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableInReview",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableOnHold",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireEvidenceToDone",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictTransitionsToAdmin",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AllowedEmailDomains",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceCover",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceIcon",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhysicalFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ContentHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalFiles", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "PhysicalFileId",
                table: "TaskAttachments",
                type: "uniqueidentifier",
                nullable: true);

            // Copy distinct files to PhysicalFiles using a hash of the filepath
            migrationBuilder.Sql(@"
                INSERT INTO [PhysicalFiles] (Id, FilePath, FileSize, ContentHash, ReferenceCount, CreatedAt)
                SELECT 
                    NEWID(), 
                    FilePath, 
                    MAX(FileSize), 
                    LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', FilePath), 2)), 
                    COUNT(*), 
                    SYSDATETIMEOFFSET()
                FROM [TaskAttachments]
                WHERE FilePath IS NOT NULL AND FilePath <> ''
                GROUP BY FilePath;
            ");

            // Update TaskAttachments to point to the newly inserted PhysicalFiles
            migrationBuilder.Sql(@"
                UPDATE ta
                SET ta.PhysicalFileId = pf.Id
                FROM [TaskAttachments] ta
                INNER JOIN [PhysicalFiles] pf ON ta.FilePath = pf.FilePath;
            ");

            // Safe fallback for any orphaned rows
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [TaskAttachments] WHERE PhysicalFileId IS NULL)
                BEGIN
                    DECLARE @DummyId UNIQUEIDENTIFIER = NEWID();
                    INSERT INTO [PhysicalFiles] (Id, FilePath, FileSize, ContentHash, ReferenceCount, CreatedAt)
                    VALUES (@DummyId, 'dummy_path', 0, 'dummy_hash', 1, SYSDATETIMEOFFSET());

                    UPDATE [TaskAttachments]
                    SET PhysicalFileId = @DummyId
                    WHERE PhysicalFileId IS NULL;
                END
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "PhysicalFileId",
                table: "TaskAttachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "TaskAttachments");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_PhysicalFileId",
                table: "TaskAttachments",
                column: "PhysicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalFiles_ContentHash",
                table: "PhysicalFiles",
                column: "ContentHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAttachments_PhysicalFiles_PhysicalFileId",
                table: "TaskAttachments",
                column: "PhysicalFileId",
                principalTable: "PhysicalFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAttachments_PhysicalFiles_PhysicalFileId",
                table: "TaskAttachments");

            migrationBuilder.DropTable(
                name: "PhysicalFiles");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_PhysicalFileId",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "PhysicalFileId",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "EnableInReview",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "EnableOnHold",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequireEvidenceToDone",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RestrictTransitionsToAdmin",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AllowedEmailDomains",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "WorkspaceCover",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "WorkspaceIcon",
                table: "Organizations");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "TaskAttachments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "TaskAttachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
