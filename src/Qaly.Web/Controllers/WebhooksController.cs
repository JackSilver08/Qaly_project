using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Qaly.Application.DTOs.Webhook;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/webhooks")]
public class WebhooksController : BaseApiController
{
    private readonly IWebhookService _webhookService;

    public WebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpGet]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var result = await _webhookService.GetByProjectAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateWebhookRequest request, CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Webhook request payload is required." });
        }

        var dto = new CreateWebhookDto(projectId, request.PayloadUrl, request.Secret, request.Events);
        var result = await _webhookService.CreateAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid id, [FromBody] UpdateWebhookDto dto, CancellationToken ct)
    {
        if (dto == null)
        {
            return BadRequest(new { error = "Webhook request payload is required." });
        }

        var result = await _webhookService.UpdateAsync(projectId, id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id, CancellationToken ct)
    {
        var result = await _webhookService.DeleteAsync(projectId, id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TriggerTest(Guid projectId, Guid id, CancellationToken ct)
    {
        var result = await _webhookService.TriggerTestAsync(projectId, id, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record CreateWebhookRequest(string PayloadUrl, string Secret, string[] Events);
