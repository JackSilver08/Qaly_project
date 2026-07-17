using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed partial class PrivacyService
{
    public async Task<Result<IReadOnlyList<PrivacyLegalHoldDto>>> ListLegalHoldsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue || !await CanManageTenantAsync(tenantId, null, userId.Value, ct))
        {
            return Result.Forbidden<IReadOnlyList<PrivacyLegalHoldDto>>();
        }

        var holds = await _db.PrivacyLegalHolds
            .AsNoTracking()
            .Where(hold => hold.TenantId == tenantId)
            .OrderByDescending(hold => hold.Status == PrivacyLegalHoldStatuses.Active)
            .ThenByDescending(hold => hold.HeldAt)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<PrivacyLegalHoldDto>>(holds.Select(ToLegalHoldDto).ToList());
    }

    public async Task<Result<PrivacyLegalHoldDto>> CreateLegalHoldAsync(
        PrivacyLegalHoldCreateRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue || !await CanManageTenantAsync(request.TenantId, request.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<PrivacyLegalHoldDto>();
        }

        var hasEntityScope = request.EntityId.HasValue && !string.IsNullOrWhiteSpace(request.EntityType);
        var invalidEntityScope = request.EntityId.HasValue != !string.IsNullOrWhiteSpace(request.EntityType);
        if (request.TenantId == Guid.Empty ||
            (!request.SubjectUserId.HasValue && !hasEntityScope) ||
            invalidEntityScope ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000 ||
            (request.ProjectId.HasValue &&
                !await ProjectBelongsToTenantAsync(request.ProjectId.Value, request.TenantId, ct)) ||
            (request.SubjectUserId.HasValue &&
                !await SubjectBelongsToTenantAsync(request.SubjectUserId.Value, request.TenantId, request.ProjectId, ct)))
        {
            return Result.Failure<PrivacyLegalHoldDto>(
                "The legal-hold scope, subject, project, or reason is invalid.",
                400,
                PrivacyErrorCodes.LegalHold);
        }

        var entityType = LimitNullable(request.EntityType, 80);
        var existing = await _db.PrivacyLegalHolds
            .FirstOrDefaultAsync(hold => hold.TenantId == request.TenantId &&
                hold.ProjectId == request.ProjectId &&
                hold.SubjectUserId == request.SubjectUserId &&
                hold.EntityType == entityType &&
                hold.EntityId == request.EntityId &&
                hold.Status == PrivacyLegalHoldStatuses.Active, ct);
        if (existing != null)
        {
            return Result.Success(ToLegalHoldDto(existing));
        }

        var hold = new PrivacyLegalHold
        {
            TenantId = request.TenantId,
            ProjectId = request.ProjectId,
            SubjectUserId = request.SubjectUserId,
            EntityType = entityType,
            EntityId = request.EntityId,
            Status = PrivacyLegalHoldStatuses.Active,
            Reason = request.Reason.Trim(),
            HeldAt = DateTimeOffset.UtcNow,
            HeldById = userId.Value
        };
        _db.PrivacyLegalHolds.Add(hold);
        AddAudit(
            hold.TenantId,
            hold.ProjectId,
            userId,
            "PRIVACY_LEGAL_HOLD_CREATED",
            nameof(PrivacyLegalHold),
            hold.Id,
            dsarId: null,
            outcome: "active",
            failureCode: PrivacyErrorCodes.LegalHold,
            metadata: new Dictionary<string, string?>
            {
                ["subjectUserId"] = hold.SubjectUserId?.ToString(),
                ["entityType"] = hold.EntityType,
                ["entityId"] = hold.EntityId?.ToString(),
                ["reasonHash"] = HashText(hold.Reason)
            });
        await _db.SaveChangesAsync(ct);
        return Result.Created(ToLegalHoldDto(hold));
    }

    public async Task<Result<PrivacyLegalHoldDto>> ReleaseLegalHoldAsync(
        Guid holdId,
        PrivacyLegalHoldReleaseRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var hold = await _db.PrivacyLegalHolds.FirstOrDefaultAsync(item => item.Id == holdId, ct);
        if (hold == null)
        {
            return Result.NotFound<PrivacyLegalHoldDto>("Legal hold was not found.");
        }

        if (!userId.HasValue || !await CanManageTenantAsync(hold.TenantId, hold.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<PrivacyLegalHoldDto>();
        }

        if (!MatchesRowVersion(hold.RowVersion, request.RowVersion))
        {
            return Result.Failure<PrivacyLegalHoldDto>(
                "The legal hold was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        if (hold.Status == PrivacyLegalHoldStatuses.Released)
        {
            return Result.Success(ToLegalHoldDto(hold));
        }

        hold.Status = PrivacyLegalHoldStatuses.Released;
        hold.ReleasedAt = DateTimeOffset.UtcNow;
        hold.ReleasedById = userId;
        AddAudit(
            hold.TenantId,
            hold.ProjectId,
            userId,
            "PRIVACY_LEGAL_HOLD_RELEASED",
            nameof(PrivacyLegalHold),
            hold.Id,
            outcome: "released",
            metadata: new Dictionary<string, string?>
            {
                ["reasonHash"] = HashText(request.Reason ?? string.Empty)
            });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PrivacyLegalHoldDto>(
                "The legal hold was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        return Result.Success(ToLegalHoldDto(hold));
    }

    private async Task<bool> SubjectBelongsToTenantAsync(
        Guid subjectUserId,
        Guid tenantId,
        Guid? projectId,
        CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(user => user.Id == subjectUserId, ct))
        {
            return false;
        }

        if (projectId.HasValue)
        {
            return await _db.Projects.AnyAsync(project => project.Id == projectId.Value &&
                    project.OwnerId == subjectUserId &&
                    (project.OrganizationId ?? project.Id) == tenantId, ct) ||
                await _db.ProjectMembers.AnyAsync(member => member.ProjectId == projectId.Value && member.UserId == subjectUserId, ct);
        }

        return await _db.Organizations.AnyAsync(organization => organization.Id == tenantId && organization.OwnerId == subjectUserId, ct) ||
            await _db.OrganizationMembers.AnyAsync(member => member.OrganizationId == tenantId && member.UserId == subjectUserId, ct) ||
            await _db.Projects.AnyAsync(project => project.Id == tenantId && project.OwnerId == subjectUserId, ct);
    }

    private static PrivacyLegalHoldDto ToLegalHoldDto(PrivacyLegalHold hold)
        => new(
            hold.Id,
            hold.TenantId,
            hold.ProjectId,
            hold.SubjectUserId,
            hold.EntityType,
            hold.EntityId,
            hold.Status,
            hold.Reason,
            hold.HeldAt,
            hold.ReleasedAt,
            EncodeRowVersion(hold.RowVersion));
}
