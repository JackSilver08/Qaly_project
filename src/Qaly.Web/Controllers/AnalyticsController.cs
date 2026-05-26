using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnalyticsController : BaseApiController
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> GetProjectAnalytics(Guid projectId, CancellationToken ct)
    {
        var result = await _analyticsService.GetProjectAnalyticsAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("workspace")]
    public async Task<IActionResult> GetWorkspaceAnalytics(CancellationToken ct)
    {
        var result = await _analyticsService.GetWorkspaceAnalyticsAsync(ct);
        return StatusCode(result.StatusCode, result);
    }
}
