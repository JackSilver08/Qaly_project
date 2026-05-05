using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _notificationService.GetByUserAsync(userId.Value, unreadOnly, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _notificationService.GetUnreadCountAsync(userId.Value, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkAsReadAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _notificationService.MarkAllAsReadAsync(userId.Value, ct);
        return StatusCode(result.StatusCode, result);
    }
}
