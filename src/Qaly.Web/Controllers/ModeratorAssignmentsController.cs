using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/moderator-assignments")]
public sealed class ModeratorAssignmentsController : BaseApiController
{
    private readonly QalyDbContext _db;
    private readonly IAuditLogService _auditLog;

    public ModeratorAssignmentsController(QalyDbContext db, IAuditLogService auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? moderatorUserId, [FromQuery] Guid? organizationId, CancellationToken ct)
    {
        var query = _db.ModeratorAssignments.AsNoTracking();
        if (moderatorUserId.HasValue) query = query.Where(item => item.ModeratorUserId == moderatorUserId.Value);
        if (organizationId.HasValue) query = query.Where(item => item.OrganizationId == organizationId.Value);

        var items = await query.OrderBy(item => item.Organization.Name).ThenBy(item => item.Capability)
            .Select(item => new ModeratorAssignmentDto(
                item.Id, item.ModeratorUserId, item.ModeratorUser.FullName, item.ModeratorUser.Email,
                item.OrganizationId, item.Organization.Name, item.Capability, item.ExpiresAt,
                item.IsActive, item.RevokedAt, item.GrantedByUserId, item.CreatedAt))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grant(GrantModeratorAssignmentsRequest request, CancellationToken ct)
    {
        var capabilities = request.Capabilities.Distinct(StringComparer.Ordinal).ToArray();
        if (capabilities.Length == 0 || capabilities.Any(capability => !ModeratorCapabilities.All.Contains(capability)))
            return BadRequest("One or more moderator capabilities are invalid.");
        if (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTimeOffset.UtcNow)
            return BadRequest("Expiration must be in the future.");

        var moderator = await _db.Users.SingleOrDefaultAsync(user => user.Id == request.ModeratorUserId, ct);
        if (moderator == null || !moderator.IsActive || !SystemRoleRules.IsModerator(moderator.Role))
            return BadRequest("The target must be an active Moderator.");
        if (!await _db.Organizations.AnyAsync(item => item.Id == request.OrganizationId && item.IsActive, ct))
            return NotFound("Organization was not found.");

        var existing = await _db.ModeratorAssignments.Where(item =>
            item.ModeratorUserId == request.ModeratorUserId && item.OrganizationId == request.OrganizationId
            && capabilities.Contains(item.Capability)).ToListAsync(ct);

        foreach (var capability in capabilities)
        {
            var assignment = existing.FirstOrDefault(item => item.Capability == capability);
            if (assignment == null)
            {
                _db.ModeratorAssignments.Add(new ModeratorAssignment
                {
                    ModeratorUserId = request.ModeratorUserId,
                    OrganizationId = request.OrganizationId,
                    Capability = capability,
                    GrantedByUserId = CurrentUserId,
                    ExpiresAt = request.ExpiresAt
                });
            }
            else
            {
                assignment.IsActive = true;
                assignment.RevokedAt = null;
                assignment.ExpiresAt = request.ExpiresAt;
                assignment.GrantedByUserId = CurrentUserId;
            }
        }

        await _auditLog.StageAsync("GrantModeratorScope", nameof(ModeratorAssignment), request.ModeratorUserId.ToString(),
            new { request.OrganizationId, Capabilities = capabilities, request.ExpiresAt }, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(new { granted = capabilities.Length });
    }

    [HttpDelete("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var assignment = await _db.ModeratorAssignments.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (assignment == null) return NotFound();
        assignment.IsActive = false;
        assignment.RevokedAt = DateTimeOffset.UtcNow;
        await _auditLog.StageAsync("RevokeModeratorScope", nameof(ModeratorAssignment), id.ToString(),
            new { assignment.ModeratorUserId, assignment.OrganizationId, assignment.Capability }, ct);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record ModeratorAssignmentDto(
    Guid Id, Guid ModeratorUserId, string ModeratorName, string ModeratorEmail,
    Guid OrganizationId, string OrganizationName, string Capability, DateTimeOffset? ExpiresAt,
    bool IsActive, DateTimeOffset? RevokedAt, Guid GrantedByUserId, DateTimeOffset CreatedAt);

public sealed record GrantModeratorAssignmentsRequest(
    Guid ModeratorUserId, Guid OrganizationId, IReadOnlyList<string> Capabilities, DateTimeOffset? ExpiresAt);
