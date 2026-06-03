using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Import;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ImportController : BaseApiController
{
    private readonly IImportService _importService;
    private readonly IFileImportService _fileImportService;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private static readonly JsonSerializerOptions ImportRequestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ImportController(IImportService importService, IFileImportService fileImportService)
    {
        _importService = importService;
        _fileImportService = fileImportService;
    }

    /// <summary>
    /// Bước 1: Upload file để parse preview + gợi ý mapping.
    /// </summary>
    [HttpPost("parse")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Parse([FromForm] IFormFile file, [FromForm] string? sheetName, [FromForm] bool firstRowIsHeader = true, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn file." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File vượt quá giới hạn 5MB." });

        using var stream = file.OpenReadStream();
        var result = await _importService.ParseFileAsync(stream, file.FileName, sheetName, firstRowIsHeader, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Bước 2: Thực hiện import với mapping đã confirm.
    /// File được gửi kèm JSON request trong multipart form.
    /// </summary>
    [HttpPost("execute")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Execute(
        [FromForm] IFormFile file,
        [FromForm] string request,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn file." });

        ImportRequest? importRequest;
        try
        {
            importRequest = JsonSerializer.Deserialize<ImportRequest>(request, ImportRequestJsonOptions);
        }
        catch
        {
            return BadRequest(new { error = "Dữ liệu request không hợp lệ." });
        }

        if (importRequest == null)
            return BadRequest(new { error = "Dữ liệu request không hợp lệ." });

        using var stream = file.OpenReadStream();
        var result = await _importService.ExecuteImportAsync(stream, file.FileName, importRequest, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("documents/preview")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> PreviewDocument([FromForm] IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui long chon file." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File vuot qua gioi han 5MB." });

        using var stream = file.OpenReadStream();
        var result = await _fileImportService.PreviewDocumentAsync(stream, file.FileName, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("documents/execute")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> ExecuteDocument(
        [FromForm] IFormFile file,
        [FromForm] Guid projectId,
        [FromForm] string? title,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui long chon file." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File vuot qua gioi han 5MB." });

        using var stream = file.OpenReadStream();
        var result = await _fileImportService.ImportDocumentAsync(projectId, stream, file.FileName, title, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("documents/zip/preview")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> PreviewZipBundle([FromForm] IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui long chon file." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File vuot qua gioi han 5MB." });

        using var stream = file.OpenReadStream();
        var result = await _fileImportService.PreviewZipBundleAsync(stream, file.FileName, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("documents/zip/execute")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> ExecuteZipBundle(
        [FromForm] IFormFile file,
        [FromForm] Guid projectId,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui long chon file." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File vuot qua gioi han 5MB." });

        using var stream = file.OpenReadStream();
        var result = await _fileImportService.ImportZipBundleAsync(projectId, stream, file.FileName, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Undo một import session (xóa tất cả task đã import).
    /// </summary>
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> UndoImport(Guid id, CancellationToken ct)
    {
        var result = await _importService.UndoImportAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Lấy lịch sử import sessions của 1 project.
    /// </summary>
    [HttpGet("sessions/{projectId:guid}")]
    public async Task<IActionResult> GetSessions(Guid projectId, CancellationToken ct)
    {
        var result = await _importService.GetSessionsAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("templates/tasks.csv")]
    public IActionResult DownloadTaskCsvTemplate()
    {
        const string csv = "Title,Description,Status,Priority,DueDate,EstimatedHours,Assignee,Labels\r\n"
            + "Viet API import,Mo ta ngan gon,Todo,Medium,2026-06-15,4,dev@qaly.local,Backend;Import\r\n";
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv))
            .ToArray();

        return File(bytes, "text/csv", "qaly-task-import-template.csv");
    }

    [HttpGet("templates/tasks.xlsx")]
    public IActionResult DownloadTaskXlsxTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Tasks");
        var headers = new[] { "Title", "Description", "Status", "Priority", "DueDate", "EstimatedHours", "Assignee", "Labels" };
        for (var i = 0; i < headers.Length; i++)
            worksheet.Cell(1, i + 1).Value = headers[i];

        worksheet.Cell(2, 1).Value = "Viet API import";
        worksheet.Cell(2, 2).Value = "Mo ta ngan gon";
        worksheet.Cell(2, 3).Value = "Todo";
        worksheet.Cell(2, 4).Value = "Medium";
        worksheet.Cell(2, 5).Value = "2026-06-15";
        worksheet.Cell(2, 6).Value = 4;
        worksheet.Cell(2, 7).Value = "dev@qaly.local";
        worksheet.Cell(2, 8).Value = "Backend;Import";

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF2FF");
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "qaly-task-import-template.xlsx");
    }
}
