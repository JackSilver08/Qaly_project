using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Import;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    /// <summary>
    /// Bước 1: Upload file để parse preview + gợi ý mapping.
    /// </summary>
    [HttpPost("parse")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Parse(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn file." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File vượt quá giới hạn 5MB." });

        using var stream = file.OpenReadStream();
        var result = await _importService.ParseFileAsync(stream, file.FileName, ct);
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
            importRequest = System.Text.Json.JsonSerializer.Deserialize<ImportRequest>(request,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
}
