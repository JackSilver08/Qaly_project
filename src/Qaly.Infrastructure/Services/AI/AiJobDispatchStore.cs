using System.Data;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed record AiJobLease(
    Guid DispatchId,
    Guid JobId,
    Guid ProviderAttemptId,
    int AttemptNumber,
    DateTimeOffset LeaseExpiresAt);

public interface IAiJobDispatchStore
{
    Task<AiJobLease?> ClaimNextAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(Guid dispatchId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task AbandonLeaseAsync(Guid dispatchId, string workerId, string errorCode, string errorMessage, CancellationToken cancellationToken = default);
}

public sealed class AiJobDispatchStore : IAiJobDispatchStore
{
    private readonly QalyDbContext _db;

    public AiJobDispatchStore(QalyDbContext db)
    {
        _db = db;
    }

    public Task<AiJobLease?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            () => ClaimNextCoreAsync(workerId, leaseDuration, cancellationToken));
    }

    private async Task<AiJobLease?> ClaimNextCoreAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        await RecoverExpiredLeaseAsync(now, cancellationToken);

        AiJobDispatch? dispatch;
        if (_db.Database.IsSqlServer())
        {
            var candidates = await _db.AiJobDispatches
                .FromSqlInterpolated($$"""
                    SELECT TOP (1) dispatch.*
                    FROM [AiJobDispatches] AS dispatch WITH (UPDLOCK, READPAST, ROWLOCK)
                    INNER JOIN [AiJobs] AS job ON job.[Id] = dispatch.[AiJobId]
                    WHERE dispatch.[CompletedAt] IS NULL
                      AND dispatch.[AvailableAt] <= {{now}}
                      AND (dispatch.[LeaseExpiresAt] IS NULL OR dispatch.[LeaseExpiresAt] <= {{now}})
                      AND job.[Status] IN (N'queued', N'retrying')
                    ORDER BY dispatch.[Priority], dispatch.[AvailableAt], dispatch.[CreatedAt]
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);
            dispatch = candidates.FirstOrDefault();
        }
        else
        {
            dispatch = await _db.AiJobDispatches
                .Include(item => item.AiJob)
                .Where(item =>
                    item.CompletedAt == null &&
                    item.AvailableAt <= now &&
                    (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now) &&
                    (item.AiJob.Status == AiJobStatuses.Queued || item.AiJob.Status == AiJobStatuses.Retrying))
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.AvailableAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (dispatch == null)
        {
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var job = await _db.AiJobs.FirstAsync(item => item.Id == dispatch.AiJobId, cancellationToken);
        if (AiJobStatuses.IsTerminal(job.Status))
        {
            dispatch.CompletedAt = now;
            dispatch.LeaseOwner = null;
            dispatch.LeaseExpiresAt = null;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var leaseExpiresAt = now.Add(leaseDuration);
        dispatch.LeaseOwner = workerId;
        dispatch.LeaseExpiresAt = leaseExpiresAt;
        dispatch.DeliveryCount++;
        dispatch.LastDispatchErrorCode = null;
        dispatch.LastDispatchError = null;

        job.Status = AiJobStatuses.Running;
        job.StartedAt ??= now;
        job.AttemptCount++;
        job.ProgressPercent = Math.Max(job.ProgressPercent, 5);
        job.LastErrorCode = null;
        job.LastErrorMessage = null;
        job.LastErrorRetryable = false;

        var attempt = new AiProviderAttempt
        {
            AiJobId = job.Id,
            AttemptNumber = job.AttemptCount,
            Status = AiAttemptStatuses.Running,
            ProviderName = string.IsNullOrWhiteSpace(job.ProviderHint) ? "auto" : job.ProviderHint,
            ModelName = "pending",
            StartedAt = now,
            RequestHash = job.RequestHash
        };
        _db.AiProviderAttempts.Add(attempt);

        await _db.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return new AiJobLease(dispatch.Id, job.Id, attempt.Id, attempt.AttemptNumber, leaseExpiresAt);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid dispatchId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dispatch = await _db.AiJobDispatches.FirstOrDefaultAsync(
            item => item.Id == dispatchId && item.LeaseOwner == workerId && item.CompletedAt == null,
            cancellationToken);
        if (dispatch == null) return false;

        dispatch.LeaseExpiresAt = now.Add(leaseDuration);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RecoverExpiredLeaseAsync(DateTimeOffset now, CancellationToken ct)
    {
        AiJobDispatch? expired;
        if (_db.Database.IsSqlServer())
        {
            var candidates = await _db.AiJobDispatches
                .FromSqlInterpolated($$"""
                    SELECT TOP (1) dispatch.*
                    FROM [AiJobDispatches] AS dispatch WITH (UPDLOCK, READPAST, ROWLOCK)
                    INNER JOIN [AiJobs] AS job ON job.[Id] = dispatch.[AiJobId]
                    WHERE dispatch.[CompletedAt] IS NULL
                      AND dispatch.[LeaseExpiresAt] IS NOT NULL
                      AND dispatch.[LeaseExpiresAt] <= {{now}}
                      AND job.[Status] = N'running'
                    ORDER BY dispatch.[LeaseExpiresAt]
                    """)
                .AsTracking()
                .ToListAsync(ct);
            expired = candidates.FirstOrDefault();
        }
        else
        {
            expired = await _db.AiJobDispatches
                .Include(item => item.AiJob)
                .Where(item =>
                    item.CompletedAt == null &&
                    item.LeaseExpiresAt != null &&
                    item.LeaseExpiresAt <= now &&
                    item.AiJob.Status == AiJobStatuses.Running)
                .OrderBy(item => item.LeaseExpiresAt)
                .FirstOrDefaultAsync(ct);
        }

        if (expired == null) return;
        var job = await _db.AiJobs.FirstAsync(item => item.Id == expired.AiJobId, ct);
        var attempt = await _db.AiProviderAttempts.FirstOrDefaultAsync(
            item => item.AiJobId == job.Id && item.AttemptNumber == job.AttemptCount,
            ct);
        if (attempt != null)
        {
            attempt.Status = AiAttemptStatuses.Failed;
            attempt.FinishedAt = now;
            attempt.ErrorCode = "AI_WORKER_LEASE_EXPIRED";
            attempt.ErrorMessage = "The worker lease expired before a terminal result was committed.";
            attempt.Retryable = true;
        }

        expired.LeaseOwner = null;
        expired.LeaseExpiresAt = null;
        expired.LastDispatchErrorCode = "AI_WORKER_LEASE_EXPIRED";
        expired.LastDispatchError = "The worker lease expired before a terminal result was committed.";
        if (job.AttemptCount < job.MaxAttempts)
        {
            job.Status = AiJobStatuses.Retrying;
            job.AvailableAt = now;
            job.NextRetryAt = now;
            job.LastErrorCode = "AI_WORKER_LEASE_EXPIRED";
            job.LastErrorMessage = expired.LastDispatchError;
            job.LastErrorRetryable = true;
            expired.AvailableAt = now;
        }
        else
        {
            job.Status = AiJobStatuses.Failed;
            job.FinishedAt = now;
            job.LastErrorCode = "AI_WORKER_LEASE_EXPIRED";
            job.LastErrorMessage = expired.LastDispatchError;
            job.LastErrorRetryable = true;
            expired.CompletedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task AbandonLeaseAsync(
        Guid dispatchId,
        string workerId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _db.AiJobDispatches
            .Include(item => item.AiJob)
            .FirstOrDefaultAsync(item => item.Id == dispatchId && item.LeaseOwner == workerId, cancellationToken);
        if (dispatch == null) return;

        var now = DateTimeOffset.UtcNow;
        dispatch.LeaseOwner = null;
        dispatch.LeaseExpiresAt = null;
        dispatch.LastDispatchErrorCode = errorCode;
        dispatch.LastDispatchError = Truncate(errorMessage, 2000);

        var job = dispatch.AiJob;
        job.LastErrorCode = errorCode;
        job.LastErrorMessage = Truncate(errorMessage, 2000);
        job.LastErrorRetryable = true;
        if (job.AttemptCount < job.MaxAttempts)
        {
            var retryAt = now.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Max(0, job.AttemptCount - 1))));
            job.Status = AiJobStatuses.Retrying;
            job.NextRetryAt = retryAt;
            job.AvailableAt = retryAt;
            dispatch.AvailableAt = retryAt;
        }
        else
        {
            job.Status = AiJobStatuses.Failed;
            job.FinishedAt = now;
            dispatch.CompletedAt = now;
        }

        var attempt = await _db.AiProviderAttempts.FirstOrDefaultAsync(
            item => item.AiJobId == job.Id && item.AttemptNumber == job.AttemptCount,
            cancellationToken);
        if (attempt != null)
        {
            attempt.Status = AiAttemptStatuses.Failed;
            attempt.FinishedAt = now;
            attempt.ErrorCode = errorCode;
            attempt.ErrorMessage = Truncate(errorMessage, 2000);
            attempt.Retryable = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
