using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/ai/native-actions")]
public sealed class AiNativeActionsController : BaseApiController
{
    private readonly IAiNativeActionService _service;

    public AiNativeActionsController(IAiNativeActionService service) => _service = service;

    [HttpGet("{draftId:guid}")]
    public async Task<IActionResult> Get(Guid draftId, CancellationToken ct)
    {
        var result = await _service.GetAsync(draftId, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{draftId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid draftId,
        UpdateAiNativeActionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(draftId, request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("{draftId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        Guid draftId,
        RejectAiNativeActionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.RejectAsync(draftId, request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("{draftId:guid}/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(
        Guid draftId,
        ConfirmAiNativeActionRequestDto request,
        CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString().Trim();
        var result = await _service.ConfirmAsync(draftId, request, idempotencyKey, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("tasks/{taskId:guid}/acceptance-checklist")]
    public async Task<IActionResult> GetChecklist(Guid taskId, CancellationToken ct)
    {
        var result = await _service.GetChecklistAsync(taskId, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPatch("acceptance-checklist/{itemId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateChecklistItem(
        Guid itemId,
        UpdateTaskAcceptanceChecklistItemRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateChecklistItemAsync(itemId, request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("projects/{projectId:guid}/digest-subscription")]
    public async Task<IActionResult> GetDigestSubscription(Guid projectId, CancellationToken ct)
    {
        var result = await _service.GetDigestSubscriptionAsync(projectId, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }
}
