using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public interface IAiJobProcessor
{
    Task ProcessAsync(AiJobLease lease, string workerId, CancellationToken cancellationToken = default);
}

public sealed partial class AiJobProcessor : IAiJobProcessor
{
    private readonly QalyDbContext _db;
    private readonly IAiGateway _gateway;
    private readonly IAiSourceGuard _sourceGuard;
    private readonly IAiComplianceService _compliance;
    private readonly IOptionsMonitor<AiJobPlatformOptions> _options;
    private readonly ILogger<AiJobProcessor> _logger;

    public AiJobProcessor(
        QalyDbContext db,
        IAiGateway gateway,
        IAiSourceGuard sourceGuard,
        IAiComplianceService compliance,
        IOptionsMonitor<AiJobPlatformOptions> options,
        ILogger<AiJobProcessor> logger)
    {
        _db = db;
        _gateway = gateway;
        _sourceGuard = sourceGuard;
        _compliance = compliance;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessAsync(
        AiJobLease lease,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.AiJobs
            .Include(item => item.Sources)
            .Include(item => item.Dispatch)
            .Include(item => item.Drafts)
            .FirstOrDefaultAsync(item => item.Id == lease.JobId, cancellationToken);
        var attempt = await _db.AiProviderAttempts
            .FirstOrDefaultAsync(item => item.Id == lease.ProviderAttemptId, cancellationToken);
        if (job == null || attempt == null || job.Dispatch?.LeaseOwner != workerId) return;

        if (!_options.CurrentValue.WorkerEnabled)
        {
            await PauseForWorkerShutdownAsync(job, attempt, cancellationToken);
            return;
        }

        if (string.Equals(job.Status, AiJobStatuses.Canceled, StringComparison.Ordinal))
        {
            await CompleteCanceledAsync(job, attempt, cancellationToken);
            return;
        }

        var sourceValidation = job.ProjectId.HasValue
            ? await _sourceGuard.ValidateAsync(
                job.ProjectId.Value,
                job.RequestedById,
                job.Sources.OrderBy(source => source.SortOrder).Select(ToSourceInput).ToList(),
                enforceFreshness: true,
                cancellationToken)
            : new AiSourceGuardResult(false, AiErrorCodes.PermissionDenied, "A project-scoped source is required.");
        if (!sourceValidation.IsAllowed)
        {
            await CompleteFailureAsync(
                job,
                attempt,
                sourceValidation.ErrorCode ?? AiErrorCodes.PermissionDenied,
                sourceValidation.ErrorMessage ?? "The source is unavailable.",
                retryable: false,
                cancellationToken);
            return;
        }

        var request = BuildGatewayRequest(job, lease.ProviderAttemptId, _options.CurrentValue.AllowProviderDegradedMock);
        var stopwatch = Stopwatch.StartNew();
        var response = await _gateway.ExecuteAsync(request, cancellationToken);
        stopwatch.Stop();

        await _db.Entry(job).ReloadAsync(cancellationToken);
        await _db.Entry(attempt).ReloadAsync(cancellationToken);
        if (job.Dispatch != null) await _db.Entry(job.Dispatch).ReloadAsync(cancellationToken);

        if (!_options.CurrentValue.WorkerEnabled)
        {
            await PauseForWorkerShutdownAsync(job, attempt, cancellationToken);
            return;
        }

        if (string.Equals(job.Status, AiJobStatuses.Canceled, StringComparison.Ordinal))
        {
            await CompleteCanceledAsync(job, attempt, cancellationToken);
            return;
        }

        attempt.ProviderName = string.IsNullOrWhiteSpace(response.ProviderName) ? job.ProviderHint : response.ProviderName;
        attempt.ModelName = string.IsNullOrWhiteSpace(response.ModelName) ? "unknown" : response.ModelName;
        attempt.FinishedAt = DateTimeOffset.UtcNow;
        attempt.LatencyMs = checked((int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds));
        attempt.InputTokens = response.InputTokens;
        attempt.OutputTokens = response.OutputTokens;
        attempt.EstimatedCostUsd = response.EstimatedCostUsd;
        attempt.CacheHit = response.CacheHit;
        attempt.IsMock = response.IsMock;
        attempt.MockReason = response.MockReason;

        if (!response.IsSuccess)
        {
            await CompleteFailureAsync(
                job,
                attempt,
                response.ErrorCode ?? AiErrorCodes.ProviderUnavailable,
                response.ErrorMessage ?? "AI execution failed.",
                response.Retryable,
                cancellationToken);
            return;
        }

        if (!TryNormalizeJson(response.Content, out var resultJson))
        {
            await CompleteFailureAsync(
                job,
                attempt,
                AiErrorCodes.SchemaInvalid,
                "AI output is not valid JSON.",
                retryable: false,
                cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var resultHash = Hash(resultJson);
        job.ResultJson = resultJson;
        job.ResultHash = resultHash;
        job.Status = AiJobStatuses.Succeeded;
        job.ProgressPercent = 100;
        job.FinishedAt = now;
        job.NextRetryAt = null;
        job.LastErrorCode = null;
        job.LastErrorMessage = null;
        job.LastErrorRetryable = false;
        job.SelectedProvider = response.ProviderName;
        job.SelectedModel = response.ModelName;
        job.EstimatedCostUsd = response.EstimatedCostUsd;
        job.ActualCostUsd = response.EstimatedCostUsd;
        job.CacheHit = response.CacheHit;
        job.IsMock = response.IsMock;
        job.MockReason = response.MockReason;

        attempt.Status = AiAttemptStatuses.Succeeded;
        attempt.ResponseHash = resultHash;

        if (RequiresDraft(job.JobType) && !await _db.AiGeneratedDrafts.AnyAsync(draft => draft.AiJobId == job.Id, cancellationToken))
        {
            _db.AiGeneratedDrafts.Add(new AiGeneratedDraft
            {
                AiJobId = job.Id,
                ProjectId = job.ProjectId!.Value,
                DraftType = ResolveDraftType(job.JobType),
                PayloadJson = resultJson,
                OriginalPayloadJson = resultJson,
                WorkingPayloadJson = resultJson,
                Status = AiDraftStatuses.PendingReview,
                SchemaId = job.SchemaId,
                SourceHashAtGeneration = job.RequestHash,
                ExpiresAt = now.AddDays(30)
            });
        }

        CompleteDispatch(job.Dispatch, now);
        await _db.SaveChangesAsync(cancellationToken);
        await _compliance.LogJobAuditEventAsync(
            job.TenantId,
            job.ProjectId,
            job.RequestedById,
            "AI_JOB_SUCCEEDED",
            nameof(AiJob),
            null,
            null,
            JsonSerializer.Serialize(new { job.Id, job.Status, job.ResultHash, job.IsMock }),
            entityGuid: job.Id,
            aiJobId: job.Id,
            providerAttemptId: attempt.Id,
            cancellationToken: cancellationToken);
        JobSucceeded(_logger, job.Id, attempt.AttemptNumber);
    }

    private async Task CompleteFailureAsync(
        AiJob job,
        AiProviderAttempt attempt,
        string errorCode,
        string errorMessage,
        bool retryable,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var canRetry = retryable && job.AttemptCount < job.MaxAttempts;
        attempt.Status = AiAttemptStatuses.Failed;
        attempt.FinishedAt ??= now;
        attempt.ErrorCode = errorCode;
        attempt.ErrorMessage = Truncate(errorMessage, 2000);
        attempt.Retryable = retryable;

        job.LastErrorCode = errorCode;
        job.LastErrorMessage = Truncate(errorMessage, 2000);
        job.LastErrorRetryable = retryable;
        if (canRetry)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(
                300,
                Math.Max(1, _options.CurrentValue.BaseRetrySeconds) * Math.Pow(2, Math.Max(0, job.AttemptCount - 1))));
            job.Status = AiJobStatuses.Retrying;
            job.NextRetryAt = now.Add(delay);
            job.AvailableAt = job.NextRetryAt.Value;
            if (job.Dispatch != null)
            {
                job.Dispatch.AvailableAt = job.NextRetryAt.Value;
                job.Dispatch.LeaseOwner = null;
                job.Dispatch.LeaseExpiresAt = null;
                job.Dispatch.LastDispatchErrorCode = errorCode;
                job.Dispatch.LastDispatchError = job.LastErrorMessage;
            }
        }
        else
        {
            job.Status = AiJobStatuses.Failed;
            job.FinishedAt = now;
            CompleteDispatch(job.Dispatch, now, errorCode, job.LastErrorMessage);
        }

        await _db.SaveChangesAsync(ct);
        await _compliance.LogJobAuditEventAsync(
            job.TenantId,
            job.ProjectId,
            job.RequestedById,
            canRetry ? "AI_JOB_RETRY_SCHEDULED" : "AI_JOB_FAILED",
            nameof(AiJob),
            null,
            null,
            JsonSerializer.Serialize(new { job.Id, job.Status, errorCode, retryable, job.NextRetryAt }),
            entityGuid: job.Id,
            aiJobId: job.Id,
            providerAttemptId: attempt.Id,
            cancellationToken: ct);
        JobFailed(_logger, job.Id, errorCode, canRetry);
    }

    private async Task CompleteCanceledAsync(AiJob job, AiProviderAttempt attempt, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        job.Status = AiJobStatuses.Canceled;
        job.CanceledAt ??= now;
        job.FinishedAt ??= now;
        attempt.Status = AiAttemptStatuses.Canceled;
        attempt.FinishedAt ??= now;
        CompleteDispatch(job.Dispatch, now);
        await _db.SaveChangesAsync(ct);
    }

    private async Task PauseForWorkerShutdownAsync(AiJob job, AiProviderAttempt attempt, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        attempt.Status = AiAttemptStatuses.Failed;
        attempt.FinishedAt ??= now;
        attempt.ErrorCode = AiErrorCodes.WorkerPaused;
        attempt.ErrorMessage = "The worker was disabled before a terminal result was committed.";
        attempt.Retryable = true;

        job.Status = AiJobStatuses.Retrying;
        job.NextRetryAt = now;
        job.AvailableAt = now;
        job.FinishedAt = null;
        job.LastErrorCode = AiErrorCodes.WorkerPaused;
        job.LastErrorMessage = attempt.ErrorMessage;
        job.LastErrorRetryable = true;
        if (job.Dispatch != null)
        {
            job.Dispatch.AvailableAt = now;
            job.Dispatch.LeaseOwner = null;
            job.Dispatch.LeaseExpiresAt = null;
            job.Dispatch.CompletedAt = null;
            job.Dispatch.LastDispatchErrorCode = AiErrorCodes.WorkerPaused;
            job.Dispatch.LastDispatchError = attempt.ErrorMessage;
        }

        await _db.SaveChangesAsync(ct);
        await _compliance.LogJobAuditEventAsync(
            job.TenantId,
            job.ProjectId,
            job.RequestedById,
            "AI_JOB_WORKER_PAUSED",
            nameof(AiJob),
            null,
            null,
            JsonSerializer.Serialize(new { job.Id, job.Status }),
            entityGuid: job.Id,
            aiJobId: job.Id,
            providerAttemptId: attempt.Id,
            cancellationToken: ct);
    }

    private static AiRequest BuildGatewayRequest(AiJob job, Guid attemptId, bool allowMockFallback)
    {
        using var document = JsonDocument.Parse(job.RequestJson);
        var root = document.RootElement;
        var sourceText = root.TryGetProperty("sourceText", out var sourceTextElement) && sourceTextElement.ValueKind == JsonValueKind.String
            ? sourceTextElement.GetString()
            : null;
        var options = root.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Object
            ? optionsElement
            : default;
        var prompt = options.ValueKind == JsonValueKind.Object && options.TryGetProperty("prompt", out var promptElement)
            ? promptElement.GetString()
            : sourceText;
        var systemPrompt = options.ValueKind == JsonValueKind.Object && options.TryGetProperty("systemPrompt", out var systemElement)
            ? systemElement.GetString()
            : null;

        return new AiRequest
        {
            JobId = job.Id,
            ProviderAttemptId = attemptId,
            JobType = job.JobType,
            ProviderHint = job.ProviderHint,
            Prompt = prompt ?? "Generate a grounded result from the authorized source references.",
            SystemPrompt = systemPrompt ?? $"Return only valid JSON matching schema {job.SchemaId}. Do not execute domain mutations.",
            ExpectedSchemaId = job.SchemaId,
            IsSensitive = job.Sensitive,
            ProjectId = job.ProjectId,
            TenantId = job.TenantId,
            UserId = job.RequestedById,
            ConsentId = job.ConsentId,
            RetentionPolicyId = job.RetentionPolicyId,
            Purpose = job.JobType.Contains("meeting", StringComparison.OrdinalIgnoreCase)
                ? PrivacyPurposes.MeetingActionExtraction
                : PrivacyPurposes.AiCloudProcessing,
            DataClassification = job.Sensitive
                ? PrivacyDataClasses.SensitiveCollaboration
                : PrivacyDataClasses.Internal,
            ProviderClass = string.Equals(job.ProviderHint, "local", StringComparison.OrdinalIgnoreCase)
                ? PrivacyProviderClasses.Local
                : PrivacyProviderClasses.Any,
            SourceType = job.SourceType ?? job.Sources.OrderBy(source => source.SortOrder).FirstOrDefault()?.SourceType ?? "ai_job",
            SourceEntityId = job.Sources.OrderBy(source => source.SortOrder).FirstOrDefault()?.SourceEntityId,
            UseCache = true,
            AllowMockFallback = allowMockFallback
        };
    }

    private static AiJobSourceInputDto ToSourceInput(AiJobSource source)
        => new(source.SourceType, source.SourceEntityId, source.LegacySourceKey, source.SourceVersion, source.SourceHash, source.SourceTimestamp);

    private static bool RequiresDraft(string jobType)
        => NormalizeType(jobType) is "meetingactionextraction" or "meetingactionextract" or "taskdraft" or
            "taskbreakdown" or "acceptancechecklist" or "draftchange";

    private static string ResolveDraftType(string jobType)
        => NormalizeType(jobType) switch
        {
            "meetingactionextraction" or "meetingactionextract" => "MeetingActionItems",
            "taskbreakdown" => "TaskBreakdown",
            "acceptancechecklist" => "AcceptanceChecklist",
            "draftchange" => "DraftChange",
            _ => "TaskDraft"
        };

    private static string NormalizeType(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool TryNormalizeJson(string value, out string normalized)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            normalized = JsonSerializer.Serialize(document.RootElement);
            return true;
        }
        catch (JsonException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static void CompleteDispatch(AiJobDispatch? dispatch, DateTimeOffset now, string? errorCode = null, string? errorMessage = null)
    {
        if (dispatch == null) return;
        dispatch.CompletedAt = now;
        dispatch.LeaseOwner = null;
        dispatch.LeaseExpiresAt = null;
        dispatch.LastDispatchErrorCode = errorCode;
        dispatch.LastDispatchError = errorMessage;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AI job {JobId} succeeded on attempt {AttemptNumber}.")]
    private static partial void JobSucceeded(ILogger logger, Guid jobId, int attemptNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI job {JobId} failed with {ErrorCode}; retry scheduled: {RetryScheduled}.")]
    private static partial void JobFailed(ILogger logger, Guid jobId, string errorCode, bool retryScheduled);
}
