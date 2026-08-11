using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/ai/project-launch")]
public sealed class ProjectLaunchOrchestrationController : BaseApiController
{
    private readonly IProjectLaunchOrchestratorService _service;

    public ProjectLaunchOrchestrationController(IProjectLaunchOrchestratorService service)
        => _service = service;

    [HttpGet("plans/{planId:guid}")]
    public async Task<IActionResult> GetPlan(Guid planId, CancellationToken ct)
    {
        var result = await _service.GetPlanAsync(planId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("plans/{planId:guid}/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(
        Guid planId,
        ConfirmProjectLaunchPlanRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.ConfirmAsync(
            planId,
            request,
            Request.Headers["Idempotency-Key"].ToString(),
            ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("executions/{executionId:guid}/rollback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rollback(
        Guid executionId,
        RollbackProjectLaunchExecutionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.RollbackAsync(
            executionId,
            request,
            Request.Headers["Idempotency-Key"].ToString(),
            ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("executions/{executionId:guid}/monitor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Monitor(
        Guid executionId,
        MonitorProjectLaunchExecutionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.MonitorAsync(executionId, request, ct);
        return StatusCode(result.StatusCode, result);
    }
}
