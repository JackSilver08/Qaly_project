using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:guid}/work-rulebook")]
public sealed class OrganizationRulebookController : BaseApiController
{
    private readonly IOrganizationWorkRulebookService _service;

    public OrganizationRulebookController(IOrganizationWorkRulebookService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct)
    {
        var result = await _service.ListAsync(organizationId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("effective")]
    public async Task<IActionResult> Effective(Guid organizationId, CancellationToken ct)
    {
        var result = await _service.GetEffectiveAsync(organizationId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft(
        Guid organizationId,
        CreateOrganizationWorkRuleSetRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.CreateDraftAsync(organizationId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{ruleSetId:guid}/activate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        Guid organizationId,
        Guid ruleSetId,
        ActivateOrganizationWorkRuleSetRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.ActivateAsync(organizationId, ruleSetId, request, ct);
        return StatusCode(result.StatusCode, result);
    }
}
