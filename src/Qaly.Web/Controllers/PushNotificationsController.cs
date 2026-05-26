using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/push")]
public class PushNotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;

    public PushNotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribePushRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _notificationService.SubscribePushAsync(
            userId.Value,
            request.Endpoint,
            request.P256dh,
            request.Auth);

        return StatusCode(result.StatusCode, result);
    }
}

public record SubscribePushRequest(string Endpoint, string P256dh, string Auth);
