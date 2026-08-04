using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/github")]
public sealed class ProjectGitHubInstallationController : BaseApiController
{
    private readonly IGitHubInstallationService _service;
    public ProjectGitHubInstallationController(IGitHubInstallationService service) => _service = service;

    [HttpGet("install-url")]
    public async Task<IActionResult> GetInstallUrl(Guid projectId, CancellationToken ct)
    {
        var result = await _service.GetInstallUrlAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("installations")]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct)
    {
        var result = await _service.ListAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("installations/{installationId:long}/complete")]
    public async Task<IActionResult> Complete(Guid projectId, long installationId, CancellationToken ct)
    {
        var result = await _service.CompleteAsync(projectId, installationId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("installations/{installationId:guid}/repositories")]
    public async Task<IActionResult> Repositories(Guid projectId, Guid installationId, CancellationToken ct)
    {
        var result = await _service.GetRepositoriesAsync(projectId, installationId, ct);
        return StatusCode(result.StatusCode, result);
    }
}
