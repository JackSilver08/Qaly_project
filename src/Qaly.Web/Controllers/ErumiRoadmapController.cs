using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

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

    [HttpPost("chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChatAndPropose([FromBody] ErumiRoadmapChatRequestDto dto, CancellationToken ct)
    {
        var result = await _erumiService.ChatAndProposeRoadmapAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveProposal([FromBody] ApproveErumiRoadmapProposalDto dto, CancellationToken ct)
    {
        var result = await _erumiService.ApproveRoadmapProposalAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("rollback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollbackSnapshot([FromBody] RollbackErumiRoadmapSnapshotDto dto, CancellationToken ct)
    {
        var result = await _erumiService.RollbackRoadmapSnapshotAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }
}
