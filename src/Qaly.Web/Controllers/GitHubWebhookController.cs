using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.Web.Controllers;

[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/integrations/github/webhook")]
public sealed class GitHubWebhookController : ControllerBase
{
    private const int MaxPayloadBytes = 5 * 1024 * 1024;
    private readonly IGitHubWebhookReceiver _receiver;

    public GitHubWebhookController(IGitHubWebhookReceiver receiver) => _receiver = receiver;

    [HttpPost]
    [RequestSizeLimit(MaxPayloadBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var result = await _receiver.ReceiveAsync(
            Request.Headers["X-GitHub-Delivery"].FirstOrDefault(),
            Request.Headers["X-GitHub-Event"].FirstOrDefault(),
            Request.Headers["X-Hub-Signature-256"].FirstOrDefault(),
            payload,
            ct);

        return result.Status switch
        {
            GitHubWebhookReceiveStatus.Accepted => Accepted(),
            GitHubWebhookReceiveStatus.Duplicate => Accepted(),
            GitHubWebhookReceiveStatus.InvalidSignature => Unauthorized(),
            GitHubWebhookReceiveStatus.InvalidRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = result.Error })
        };
    }
}
