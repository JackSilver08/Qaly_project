using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskKeyNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "TaskItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaskSequence",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill existing tasks with a stable, per-project sequential number
            // (ordered by creation) BEFORE the unique index is created, otherwise
            // every existing row would collide on Number = 0.
            // Includes soft-deleted rows so numbers stay unique across the table.
            migrationBuilder.Sql(@"
;WITH numbered AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ProjectId ORDER BY CreatedAt, Id) AS rn
    FROM TaskItems
)
UPDATE t SET t.Number = n.rn
FROM TaskItems t
INNER JOIN numbered n ON t.Id = n.Id;");

            // Seed each project's counter to its current max task number.
            migrationBuilder.Sql(@"
UPDATE p SET p.TaskSequence = agg.MaxNumber
FROM Projects p
INNER JOIN (
    SELECT ProjectId, MAX(Number) AS MaxNumber
    FROM TaskItems
    GROUP BY ProjectId
) agg ON agg.ProjectId = p.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_Number",
                table: "TaskItems",
                columns: new[] { "ProjectId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_Number",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "TaskSequence",
                table: "Projects");
        }
    }
}
