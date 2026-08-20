using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

/// <summary>
/// API Controller for AI-assisted Roadmap Management & Proposal Diff Workflow.
/// Supports Fast Actions, Simulation Sandbox, Executive Reporting, and Permission-Guarded Diff Approval.
/// </summary>
[ApiController]
[Authorize]
[Route("api/erumi-roadmap")]
public class ErumiRoadmapController : BaseApiController
{
    private readonly IErumiRoadmapAiService _erumiService;

    public ErumiRoadmapController(IErumiRoadmapAiService erumiService)
    {
        _erumiService = erumiService;
    }

    /// <summary>
    /// Free-form chat interaction with Erumi AI to discuss roadmap ideas and get proposal diffs.
    /// </summary>
    [HttpPost("chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChatAndPropose([FromBody] ErumiRoadmapChatRequestDto dto, CancellationToken ct)
    {
        var result = await _erumiService.ChatAndProposeRoadmapAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Triggers a 1-Click Fast Action (e.g. ExpandPhase, AuditRisks, AutoBalance, BreakdownWBS, Forecast).
    /// </summary>
    [HttpPost("fast-action")]
    public async Task<IActionResult> ExecuteFastAction([FromBody] ErumiRoadmapActionRequestDto dto, CancellationToken ct)
    {
        var result = await _erumiService.ExecuteFastActionAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Simulates a What-If scenario in an isolated sandbox without altering actual project data.
    /// </summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> SimulateScenario([FromBody] ErumiRoadmapActionRequestDto dto, CancellationToken ct)
    {
        var result = await _erumiService.SimulateScenarioAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Generates an executive brief with milestone highlights and markdown report for stakeholders.
    /// </summary>
    [HttpGet("executive-brief/{projectId:guid}")]
    public async Task<IActionResult> GetExecutiveBrief([FromRoute] Guid projectId, CancellationToken ct)
    {
        var result = await _erumiService.GenerateExecutiveBriefAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Approves and applies an AI proposal diff into the project roadmap.
    /// Strictly restricted to Project Owners and authorized Project Managers.
    /// </summary>
    [HttpPost("approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveProposal([FromBody] ApproveErumiRoadmapProposalDto dto, CancellationToken ct)
    {
        var result = await _erumiService.ApproveRoadmapProposalAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Rolls back an applied snapshot within the 72-hour retention window.
    /// Strictly restricted to Project Owners and authorized Project Managers.
    /// </summary>
    [HttpPost("rollback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollbackSnapshot([FromBody] RollbackErumiRoadmapSnapshotDto dto, CancellationToken ct)
    {
        var result = await _erumiService.RollbackRoadmapSnapshotAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }
}
