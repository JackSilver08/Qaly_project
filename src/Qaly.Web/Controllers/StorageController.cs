using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/storage")]
public class StorageController : BaseApiController
{
    private readonly IAttachmentService _attachmentService;

    public StorageController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet("duplicates")]
    public async Task<IActionResult> GetDuplicates(CancellationToken ct)
    {
        var result = await _attachmentService.GetDuplicatesAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("deduplicate")]
    public async Task<IActionResult> Deduplicate(CancellationToken ct)
    {
        var result = await _attachmentService.DeduplicateAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStorageStats(CancellationToken ct)
    {
        var result = await _attachmentService.GetStorageStatsAsync(ct);
        return StatusCode(result.StatusCode, result);
    }
}
