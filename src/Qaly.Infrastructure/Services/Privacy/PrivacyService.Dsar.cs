using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed partial class PrivacyService
{
    public async Task<Result<DataSubjectRequestDto>> SubmitDataSubjectRequestAsync(
        DataSubjectRequestSubmitRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue || !await CanAccessTenantAsync(request.TenantId, request.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<DataSubjectRequestDto>();
        }

        var requestType = request.RequestType.Trim().ToLowerInvariant();
        var scope = request.Scope.Trim().ToLowerInvariant();
        var idempotencyKey = request.IdempotencyKey.Trim();
        var subjectUserId = request.SubjectUserId ?? userId.Value;
        if (request.TenantId == Guid.Empty ||
            requestType is not (DataSubjectRequestTypes.Export or DataSubjectRequestTypes.Delete) ||
            scope is not ("all" or "project" or "meetings" or "ai") ||
            (scope == "project" && !request.ProjectId.HasValue) ||
            idempotencyKey.Length is < 8 or > 160)
        {
            return Result.Failure<DataSubjectRequestDto>(
                "The request type, scope, tenant, or idempotency key is invalid.",
                400,
                PrivacyErrorCodes.IdentityRequired);
        }

        if (subjectUserId != userId.Value &&
            !await CanManageTenantAsync(request.TenantId, request.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<DataSubjectRequestDto>();
        }

        var requestHash = HashText(JsonSerializer.Serialize(new
        {
            request.TenantId,
            request.ProjectId,
            SubjectUserId = subjectUserId,
            RequestType = requestType,
            Scope = scope
        }, JsonOptions));
        var existing = await _db.DataSubjectRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == request.TenantId &&
                item.RequesterUserId == userId.Value &&
                item.IdempotencyKey == idempotencyKey, ct);
        if (existing != null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Result.Failure<DataSubjectRequestDto>(
                    "The idempotency key was used for a different data-subject request.",
                    409,
                    PrivacyErrorCodes.IdempotencyConflict);
            }

            return Result.Accepted(ToDataSubjectRequestDto(existing));
        }

        var now = DateTimeOffset.UtcNow;
        var dataRequest = new DataSubjectRequest
        {
            TenantId = request.TenantId,
            ProjectId = request.ProjectId,
            RequesterUserId = userId,
            SubjectUserId = subjectUserId,
            RequestType = requestType,
            ScopeJson = JsonSerializer.Serialize(new { scope }, JsonOptions),
            Status = DataSubjectRequestStatuses.Submitted,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            RequestedAt = now,
            AvailableAt = now,
            MaxAttempts = Math.Max(1, _options.MaxAttempts)
        };
        _db.DataSubjectRequests.Add(dataRequest);
        AddAudit(
            request.TenantId,
            request.ProjectId,
            userId,
            "DSAR_SUBMITTED",
            nameof(DataSubjectRequest),
            dataRequest.Id,
            dsarId: dataRequest.Id,
            purpose: requestType == DataSubjectRequestTypes.Export
                ? PrivacyPurposes.SubjectExport
                : PrivacyPurposes.SubjectDeletion,
            outcome: "submitted",
            metadata: new Dictionary<string, string?>
            {
                ["requestType"] = requestType,
                ["scope"] = scope,
                ["subjectUserId"] = subjectUserId.ToString()
            });
        await _db.SaveChangesAsync(ct);
        return Result.Accepted(ToDataSubjectRequestDto(dataRequest));
    }

    public async Task<Result<IReadOnlyList<DataSubjectRequestDto>>> ListDataSubjectRequestsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue || !await CanAccessTenantAsync(tenantId, null, userId.Value, ct))
        {
            return Result.Forbidden<IReadOnlyList<DataSubjectRequestDto>>();
        }

        var canManage = await CanManageTenantAsync(tenantId, null, userId.Value, ct);
        var query = _db.DataSubjectRequests.AsNoTracking().Where(request => request.TenantId == tenantId);
        if (!canManage)
        {
            query = query.Where(request => request.RequesterUserId == userId.Value || request.SubjectUserId == userId.Value);
        }

        var requests = await query.OrderByDescending(request => request.RequestedAt).ToListAsync(ct);
        return Result.Success<IReadOnlyList<DataSubjectRequestDto>>(requests.Select(ToDataSubjectRequestDto).ToList());
    }

    public async Task<Result<DataSubjectRequestDto>> GetDataSubjectRequestAsync(
        Guid requestId,
        CancellationToken ct = default)
    {
        var access = await GetVisibleDataSubjectRequestAsync(requestId, tracking: false, ct);
        return access.IsSuccess && access.Data != null
            ? Result.Success(ToDataSubjectRequestDto(access.Data))
            : Result.Failure<DataSubjectRequestDto>(access.Error!, access.StatusCode, access.ErrorCode);
    }

    public async Task<Result<DataSubjectRequestDto>> AcceptDataSubjectRequestAsync(
        Guid requestId,
        DataSubjectRequestDecisionRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var dataRequest = await _db.DataSubjectRequests.FirstOrDefaultAsync(item => item.Id == requestId, ct);
        if (dataRequest == null)
        {
            return Result.NotFound<DataSubjectRequestDto>("Data-subject request was not found.");
        }

        if (!userId.HasValue || !dataRequest.TenantId.HasValue ||
            !await CanManageTenantAsync(dataRequest.TenantId.Value, dataRequest.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<DataSubjectRequestDto>();
        }

        if (!dataRequest.SubjectUserId.HasValue ||
            dataRequest.Status is not (DataSubjectRequestStatuses.Submitted or
                DataSubjectRequestStatuses.IdentityVerification or
                DataSubjectRequestStatuses.Failed))
        {
            return Result.Failure<DataSubjectRequestDto>(
                "The request cannot transition to accepted from its current state.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        var now = DateTimeOffset.UtcNow;
        dataRequest.IdentityVerifiedAt = now;
        dataRequest.IdentityVerifiedById = userId;
        dataRequest.ApprovedBy = userId;
        dataRequest.AcceptedAt = now;
        dataRequest.DeadlineAt = now.AddDays(Math.Max(1, _options.DsarDeadlineDays));
        dataRequest.AvailableAt = now;
        dataRequest.Status = DataSubjectRequestStatuses.Accepted;
        dataRequest.LeaseOwner = null;
        dataRequest.LeaseExpiresAt = null;
        dataRequest.LastErrorCode = null;
        dataRequest.LastErrorMessage = null;
        AddAudit(
            dataRequest.TenantId,
            dataRequest.ProjectId,
            userId,
            "DSAR_ACCEPTED",
            nameof(DataSubjectRequest),
            dataRequest.Id,
            dsarId: dataRequest.Id,
            purpose: dataRequest.RequestType == DataSubjectRequestTypes.Export
                ? PrivacyPurposes.SubjectExport
                : PrivacyPurposes.SubjectDeletion,
            outcome: "accepted",
            metadata: new Dictionary<string, string?>
            {
                ["subjectUserId"] = dataRequest.SubjectUserId?.ToString(),
                ["decisionReasonHash"] = HashText(request.Reason ?? string.Empty)
            });
        await _db.SaveChangesAsync(ct);
        return Result.Accepted(ToDataSubjectRequestDto(dataRequest));
    }

    public async Task<Result<DataSubjectRequestDto>> RejectDataSubjectRequestAsync(
        Guid requestId,
        DataSubjectRequestDecisionRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var dataRequest = await _db.DataSubjectRequests.FirstOrDefaultAsync(item => item.Id == requestId, ct);
        if (dataRequest == null)
        {
            return Result.NotFound<DataSubjectRequestDto>("Data-subject request was not found.");
        }

        if (!userId.HasValue || !dataRequest.TenantId.HasValue ||
            !await CanManageTenantAsync(dataRequest.TenantId.Value, dataRequest.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<DataSubjectRequestDto>();
        }

        if (dataRequest.Status is DataSubjectRequestStatuses.Completed or
            DataSubjectRequestStatuses.PartiallyCompleted or
            DataSubjectRequestStatuses.Collecting)
        {
            return Result.Failure<DataSubjectRequestDto>(
                "A processing or completed request cannot be rejected.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        dataRequest.Status = DataSubjectRequestStatuses.Rejected;
        dataRequest.RejectionReason = LimitNullable(request.Reason, 1000) ?? "Rejected by an authorized privacy operator.";
        dataRequest.CompletedAt = DateTimeOffset.UtcNow;
        dataRequest.LeaseOwner = null;
        dataRequest.LeaseExpiresAt = null;
        AddAudit(
            dataRequest.TenantId,
            dataRequest.ProjectId,
            userId,
            "DSAR_REJECTED",
            nameof(DataSubjectRequest),
            dataRequest.Id,
            dsarId: dataRequest.Id,
            purpose: dataRequest.RequestType == DataSubjectRequestTypes.Export
                ? PrivacyPurposes.SubjectExport
                : PrivacyPurposes.SubjectDeletion,
            outcome: "rejected",
            metadata: new Dictionary<string, string?>
            {
                ["reasonHash"] = HashText(request.Reason ?? string.Empty)
            });
        await _db.SaveChangesAsync(ct);
        return Result.Success(ToDataSubjectRequestDto(dataRequest));
    }

    public async Task<Result<PrivacyExportArtifact>> DownloadDataSubjectExportAsync(
        Guid requestId,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var dataRequest = await _db.DataSubjectRequests.FirstOrDefaultAsync(item => item.Id == requestId, ct);
        if (dataRequest == null)
        {
            return Result.NotFound<PrivacyExportArtifact>("Data-subject request was not found.");
        }

        var isOwner = userId.HasValue &&
            (dataRequest.RequesterUserId == userId.Value || dataRequest.SubjectUserId == userId.Value);
        var canManage = userId.HasValue && dataRequest.TenantId.HasValue &&
            await CanManageTenantAsync(dataRequest.TenantId.Value, dataRequest.ProjectId, userId.Value, ct);
        if (!isOwner && !canManage)
        {
            return Result.Forbidden<PrivacyExportArtifact>();
        }

        if (dataRequest.RequestType != DataSubjectRequestTypes.Export ||
            dataRequest.Status is not (DataSubjectRequestStatuses.Completed or DataSubjectRequestStatuses.PartiallyCompleted) ||
            string.IsNullOrWhiteSpace(dataRequest.EncryptedResultPayload))
        {
            return Result.Failure<PrivacyExportArtifact>(
                "The export is not ready.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        if (!dataRequest.ResultExpiresAt.HasValue || dataRequest.ResultExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure<PrivacyExportArtifact>(
                "The export artifact has expired.",
                410,
                PrivacyErrorCodes.ExportExpired);
        }

        string plaintext;
        try
        {
            plaintext = _payloadProtector.Unprotect(dataRequest.EncryptedResultPayload);
        }
        catch (CryptographicException)
        {
            return Result.Failure<PrivacyExportArtifact>(
                "The export artifact cannot be decrypted.",
                500,
                PrivacyErrorCodes.WorkerUnavailable);
        }

        dataRequest.DownloadCount++;
        dataRequest.LastDownloadedAt = DateTimeOffset.UtcNow;
        AddAudit(
            dataRequest.TenantId,
            dataRequest.ProjectId,
            userId,
            "DSAR_EXPORT_DOWNLOADED",
            nameof(DataSubjectRequest),
            dataRequest.Id,
            dsarId: dataRequest.Id,
            purpose: PrivacyPurposes.SubjectExport,
            outcome: "downloaded",
            metadata: new Dictionary<string, string?>
            {
                ["downloadCount"] = dataRequest.DownloadCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        await _db.SaveChangesAsync(ct);

        return Result.Success(new PrivacyExportArtifact(
            Encoding.UTF8.GetBytes(plaintext),
            dataRequest.ResultContentType ?? "application/json",
            dataRequest.ResultFileName ?? $"qaly-dsar-{dataRequest.Id:N}.json",
            dataRequest.ResultExpiresAt.Value));
    }

    private async Task<Result<DataSubjectRequest>> GetVisibleDataSubjectRequestAsync(
        Guid requestId,
        bool tracking,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
        {
            return Result.Forbidden<DataSubjectRequest>();
        }

        var query = tracking ? _db.DataSubjectRequests.AsQueryable() : _db.DataSubjectRequests.AsNoTracking();
        var dataRequest = await query.FirstOrDefaultAsync(item => item.Id == requestId, ct);
        if (dataRequest == null)
        {
            return Result.NotFound<DataSubjectRequest>("Data-subject request was not found.");
        }

        var isOwner = dataRequest.RequesterUserId == userId.Value || dataRequest.SubjectUserId == userId.Value;
        var canManage = dataRequest.TenantId.HasValue &&
            await CanManageTenantAsync(dataRequest.TenantId.Value, dataRequest.ProjectId, userId.Value, ct);
        return isOwner || canManage
            ? Result.Success(dataRequest)
            : Result.Forbidden<DataSubjectRequest>();
    }

    private static DataSubjectRequestDto ToDataSubjectRequestDto(DataSubjectRequest request)
        => new(
            request.Id,
            request.TenantId,
            request.ProjectId,
            request.RequesterUserId,
            request.SubjectUserId,
            request.RequestType,
            request.ScopeJson,
            request.Status,
            request.RequestedAt,
            request.IdentityVerifiedAt,
            request.AcceptedAt,
            request.DeadlineAt,
            request.StartedAt,
            request.CompletedAt,
            request.ResultExpiresAt,
            request.LegalHoldDetected,
            request.LegalHoldReason,
            request.RejectionReason,
            request.AttemptCount,
            request.LastErrorCode,
            EncodeRowVersion(request.RowVersion));
}
