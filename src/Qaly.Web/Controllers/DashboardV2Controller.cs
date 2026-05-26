using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard/v2/projects")]
public class DashboardV2Controller : BaseApiController
{
    private readonly IDashboardSummaryService _dashboardSummaryService;

    public DashboardV2Controller(IDashboardSummaryService dashboardSummaryService)
    {
        _dashboardSummaryService = dashboardSummaryService;
    }

    [HttpGet("{projectId:guid}/summary")]
    public async Task<IActionResult> GetProjectSummary(
        Guid projectId,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery(Name = "to")] DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _dashboardSummaryService.GetProjectSummaryAsync(projectId, from, toDate, ct);
        return StatusCode(result.StatusCode, result);
    }
}
