using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> GetByEntity(string entityType, string entityId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _auditLogService.GetByEntityAsync(entityType, entityId, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("user/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetByUser(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _auditLogService.GetByUserAsync(userId, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _auditLogService.GetByUserAsync(userId.Value, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var result = await _auditLogService.GetRecentWorkspaceActivityAsync(limit, ct);
        return StatusCode(result.StatusCode, result);
    }
}
