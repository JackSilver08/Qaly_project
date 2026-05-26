using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Qaly.Domain.Entities;

namespace Qaly.Application.Services;

public interface IAiExportService
{
    Task<byte[]> ExportProjectToExcelAsync(Project project);
    Task<byte[]> ExportProjectToWordAsync(Project project);
}

public class AiExportService : IAiExportService
{
    private static readonly CultureInfo ExportCulture = CultureInfo.InvariantCulture;

    public Task<byte[]> ExportProjectToExcelAsync(Project project)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Project Summary");

        // Header
        worksheet.Cell(1, 1).Value = "Project Name:";
        worksheet.Cell(1, 2).Value = project.Name;
        worksheet.Cell(2, 1).Value = "Status:";
        worksheet.Cell(2, 2).Value = project.Status;

        // Tasks Table
        var currentRow = 4;
        worksheet.Cell(currentRow, 1).Value = "Task Title";
        worksheet.Cell(currentRow, 2).Value = "Status";
        worksheet.Cell(currentRow, 3).Value = "Priority";
        worksheet.Cell(currentRow, 4).Value = "Due Date";
        
        worksheet.Range(currentRow, 1, currentRow, 4).Style.Font.Bold = true;
        worksheet.Range(currentRow, 1, currentRow, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

        foreach (var task in project.Tasks)
        {
            currentRow++;
            worksheet.Cell(currentRow, 1).Value = task.Title;
            worksheet.Cell(currentRow, 2).Value = task.Status;
            worksheet.Cell(currentRow, 3).Value = task.Priority;
            worksheet.Cell(currentRow, 4).Value = task.DueDate?.ToString("dd/MM/yyyy", ExportCulture) ?? "N/A";
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    public Task<byte[]> ExportProjectToWordAsync(Project project)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var body = mainPart.Document.Body!;
            body.Append(
                Paragraph($"Project report: {project.Name}", bold: true, fontSize: "32"),
                Paragraph($"Status: {project.Status}", bold: true),
                Paragraph($"Description: {Normalize(project.Description, "No description")}"),
                Paragraph($"Generated at: {DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", ExportCulture)}"),
                Paragraph("Tasks", bold: true, fontSize: "24"),
                BuildTasksTable(project.Tasks),
                new SectionProperties(new PageSize { Width = 11906U, Height = 16838U }));

            mainPart.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
    }

    private static Table BuildTasksTable(IEnumerable<TaskItem> tasks)
    {
        var table = new Table(
            new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        table.Append(new TableRow(
            Cell("Title", bold: true),
            Cell("Status", bold: true),
            Cell("Priority", bold: true),
            Cell("Due date", bold: true)));

        var rows = tasks
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.DueDate)
            .ThenBy(task => task.Title)
            .ToList();

        if (rows.Count == 0)
        {
            table.Append(new TableRow(Cell("No tasks", gridSpan: 4)));
            return table;
        }

        foreach (var task in rows)
        {
            table.Append(new TableRow(
                Cell(task.Title),
                Cell(task.Status),
                Cell(task.Priority),
                Cell(task.DueDate?.ToString("dd/MM/yyyy", ExportCulture) ?? "N/A")));
        }

        return table;
    }

    private static Paragraph Paragraph(string text, bool bold = false, string fontSize = "22")
        => new(
            new ParagraphProperties(new SpacingBetweenLines { After = "160" }),
            new Run(
                RunProperties(bold, fontSize),
                new Text(text)));

    private static TableCell Cell(string text, bool bold = false, int gridSpan = 1)
    {
        var properties = new TableCellProperties();
        if (gridSpan > 1)
        {
            properties.Append(new GridSpan { Val = gridSpan });
        }

        return new TableCell(
            properties,
            new Paragraph(
                new Run(
                    RunProperties(bold),
                    new Text(text))));
    }

    private static RunProperties RunProperties(bool bold, string? fontSize = null)
    {
        var properties = new RunProperties();
        if (bold)
        {
            properties.Append(new Bold());
        }

        if (!string.IsNullOrWhiteSpace(fontSize))
        {
            properties.Append(new FontSize { Val = fontSize });
        }

        return properties;
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
