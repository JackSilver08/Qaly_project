using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/integrations/github/setup")]
public sealed class GitHubSetupController : ControllerBase
{
    private readonly IGitHubInstallationService _service;
    public GitHubSetupController(IGitHubInstallationService service) => _service = service;

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery(Name = "installation_id")] long installationId,
        [FromQuery] string state,
        CancellationToken ct)
    {
        if (!Guid.TryParseExact(state, "N", out var projectId) || installationId <= 0)
            return Redirect("/projects?github=invalid-callback");

        var result = await _service.CompleteAsync(projectId, installationId, ct);
        return result.IsSuccess
            ? Redirect($"/projects/{projectId}?tab=github&github=connected")
            : Redirect($"/projects/{projectId}?tab=github&github=error");
    }
}
