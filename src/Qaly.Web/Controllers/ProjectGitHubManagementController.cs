using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.Web.Controllers;

[ApiController, Authorize]
[Route("api/projects/{projectId:guid}/github/management")]
public sealed class ProjectGitHubManagementController : BaseApiController
{
    private readonly IGitHubProjectManagementService _service;
    public ProjectGitHubManagementController(IGitHubProjectManagementService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    { var result = await _service.GetAsync(projectId, ct); return StatusCode(result.StatusCode, result); }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(Guid projectId, CancellationToken ct)
    { var result = await _service.SyncAsync(projectId, ct); return StatusCode(result.StatusCode, result); }
}
