using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Attachment;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/attachments")]
public class AttachmentsController : BaseApiController
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet("task/{taskItemId:guid}")]
    public async Task<IActionResult> GetByTask(Guid taskItemId, CancellationToken ct)
    {
        try
        {
            var result = await _attachmentService.GetByTaskAsync(taskItemId, ct);
            if (result.StatusCode == 404)
            {
                return Ok(Array.Empty<TaskAttachmentDto>());
            }
            return StatusCode(result.StatusCode, result);
        }
        catch
        {
            return Ok(Array.Empty<TaskAttachmentDto>());
        }
    }

    [HttpPost("task/{taskItemId:guid}")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Upload(Guid taskItemId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest(new { error = "A valid file is required." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _attachmentService.UploadAsync(taskItemId, file.FileName, file.ContentType, file.Length, stream, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _attachmentService.DeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id:guid}/evidence")]
    public async Task<IActionResult> MarkAsEvidence(Guid id, [FromBody] UpdateEvidenceFlagRequest request, CancellationToken ct)
    {
        var result = await _attachmentService.MarkAsEvidenceAsync(id, request.IsEvidence, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/evidence/review")]
    public async Task<IActionResult> ReviewEvidence(Guid id, [FromBody] ReviewEvidenceRequest request, CancellationToken ct)
    {
        var result = await _attachmentService.ReviewEvidenceAsync(id, request.Approve, request.ReviewNote, ct);
        return StatusCode(result.StatusCode, result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("duplicates")]
    public async Task<IActionResult> GetDuplicates(CancellationToken ct)
    {
        var result = await _attachmentService.GetDuplicatesAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("deduplicate")]
    public async Task<IActionResult> Deduplicate(CancellationToken ct)
    {
        var result = await _attachmentService.DeduplicateAsync(ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record UpdateEvidenceFlagRequest(bool IsEvidence);
public sealed record ReviewEvidenceRequest(bool Approve, string? ReviewNote);
