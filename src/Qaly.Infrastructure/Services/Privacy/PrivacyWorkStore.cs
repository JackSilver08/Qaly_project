using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.Privacy;

public static class PrivacyWorkKinds
{
    public const string Retention = "retention";
    public const string DataSubjectRequest = "dsar";
}

public sealed record PrivacyWorkLease(
    string Kind,
    Guid WorkId,
    int AttemptNumber,
    DateTimeOffset LeaseExpiresAt);

public interface IPrivacyWorkStore
{
    Task<PrivacyWorkLease?> ClaimNextAsync(string workerId, TimeSpan leaseDuration, CancellationToken ct = default);
    Task<bool> RenewLeaseAsync(PrivacyWorkLease lease, string workerId, TimeSpan leaseDuration, CancellationToken ct = default);
    Task AbandonLeaseAsync(PrivacyWorkLease lease, string workerId, string errorCode, string errorMessage, CancellationToken ct = default);
    Task<bool> ReleaseLeaseAsync(PrivacyWorkLease lease, string workerId, CancellationToken ct = default);
}

public sealed class PrivacyWorkStore : IPrivacyWorkStore
{
    private readonly QalyDbContext _db;
    private readonly PrivacyV4Options _options;

    public PrivacyWorkStore(QalyDbContext db, IOptions<PrivacyV4Options> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PrivacyWorkLease?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // A verified DSAR has a statutory deadline, while retention cleanup is maintenance work.
            // Claim DSAR first so a continuous retention backlog cannot starve a legal request.
            var dataSubjectRequest = await ClaimDataSubjectRequestAsync(workerId, leaseDuration, ct);
            return dataSubjectRequest ?? await ClaimRetentionAsync(workerId, leaseDuration, ct);
        });
    }

    public async Task<bool> RenewLeaseAsync(
        PrivacyWorkLease lease,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_db.Database.IsRelational())
        {
            var affected = lease.Kind == PrivacyWorkKinds.Retention
                ? await _db.PrivacyRetentionActions
                    .Where(item =>
                        item.Id == lease.WorkId &&
                        item.Status == PrivacyWorkerStatuses.Running &&
                        item.LeaseOwner == workerId &&
                        item.LeaseExpiresAt != null &&
                        item.LeaseExpiresAt > now)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            item => item.LeaseExpiresAt,
                            now.Add(leaseDuration)),
                        ct)
                : await _db.DataSubjectRequests
                    .Where(item =>
                        item.Id == lease.WorkId &&
                        item.Status == DataSubjectRequestStatuses.Collecting &&
                        item.LeaseOwner == workerId &&
                        item.LeaseExpiresAt != null &&
                        item.LeaseExpiresAt > now)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            item => item.LeaseExpiresAt,
                            now.Add(leaseDuration)),
                        ct);
            return affected == 1;
        }

        if (lease.Kind == PrivacyWorkKinds.Retention)
        {
            var action = await _db.PrivacyRetentionActions.FirstOrDefaultAsync(item =>
                item.Id == lease.WorkId &&
                item.Status == PrivacyWorkerStatuses.Running &&
                item.LeaseOwner == workerId &&
                item.LeaseExpiresAt != null &&
                item.LeaseExpiresAt > now,
                ct);
            if (action == null)
            {
                return false;
            }

            action.LeaseExpiresAt = now.Add(leaseDuration);
        }
        else
        {
            var request = await _db.DataSubjectRequests.FirstOrDefaultAsync(item =>
                item.Id == lease.WorkId &&
                item.Status == DataSubjectRequestStatuses.Collecting &&
                item.LeaseOwner == workerId &&
                item.LeaseExpiresAt != null &&
                item.LeaseExpiresAt > now,
                ct);
            if (request == null)
            {
                return false;
            }

            request.LeaseExpiresAt = now.Add(leaseDuration);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task AbandonLeaseAsync(
        PrivacyWorkLease lease,
        string workerId,
        string errorCode,
        string errorMessage,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (lease.Kind == PrivacyWorkKinds.Retention)
        {
            var action = await _db.PrivacyRetentionActions.FirstOrDefaultAsync(item =>
                item.Id == lease.WorkId && item.LeaseOwner == workerId,
                ct);
            if (action == null)
            {
                return;
            }

            action.LeaseOwner = null;
            action.LeaseExpiresAt = null;
            action.LastErrorCode = Limit(errorCode, 100);
            action.LastErrorMessage = Limit(errorMessage, 2000);
            if (action.AttemptCount < action.MaxAttempts)
            {
                action.Status = PrivacyWorkerStatuses.Pending;
                action.AvailableAt = RetryAt(now, action.AttemptCount);
            }
            else
            {
                action.Status = PrivacyWorkerStatuses.Failed;
                action.CompletedAt = now;
            }
        }
        else
        {
            var request = await _db.DataSubjectRequests.FirstOrDefaultAsync(item =>
                item.Id == lease.WorkId && item.LeaseOwner == workerId,
                ct);
            if (request == null)
            {
                return;
            }

            request.LeaseOwner = null;
            request.LeaseExpiresAt = null;
            request.LastErrorCode = Limit(errorCode, 100);
            request.LastErrorMessage = Limit(errorMessage, 2000);
            if (request.AttemptCount < request.MaxAttempts)
            {
                request.Status = DataSubjectRequestStatuses.Accepted;
                request.AvailableAt = RetryAt(now, request.AttemptCount);
            }
            else
            {
                request.Status = DataSubjectRequestStatuses.Failed;
                request.CompletedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<PrivacyWorkLease?> ClaimRetentionAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
            : null;
        await RecoverExpiredRetentionLeaseAsync(now, ct);

        PrivacyRetentionAction? action;
        if (_db.Database.IsSqlServer())
        {
            action = (await _db.PrivacyRetentionActions
                .FromSqlInterpolated($$"""
                    SELECT TOP (1) action.*
                    FROM [PrivacyRetentionActions] AS action WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE action.[Status] = N'pending'
                      AND action.[DueAt] <= {{now}}
                      AND action.[AvailableAt] <= {{now}}
                      AND (action.[LeaseExpiresAt] IS NULL OR action.[LeaseExpiresAt] <= {{now}})
                    ORDER BY action.[DueAt], action.[AvailableAt], action.[CreatedAt], action.[Id]
                    """)
                .AsTracking()
                .ToListAsync(ct))
                .FirstOrDefault();
        }
        else
        {
            action = await _db.PrivacyRetentionActions
                .Where(item => item.Status == PrivacyWorkerStatuses.Pending &&
                    item.DueAt <= now &&
                    item.AvailableAt <= now &&
                    (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now))
                .OrderBy(item => item.DueAt)
                .ThenBy(item => item.AvailableAt)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (action == null)
        {
            if (transaction != null) await transaction.CommitAsync(ct);
            return null;
        }

        var expiresAt = now.Add(leaseDuration);
        action.Status = PrivacyWorkerStatuses.Running;
        action.LeaseOwner = workerId;
        action.LeaseExpiresAt = expiresAt;
        action.StartedAt ??= now;
        action.AttemptCount++;
        action.LastErrorCode = null;
        action.LastErrorMessage = null;
        await _db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return new PrivacyWorkLease(PrivacyWorkKinds.Retention, action.Id, action.AttemptCount, expiresAt);
    }

    private async Task<PrivacyWorkLease?> ClaimDataSubjectRequestAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
            : null;
        await RecoverExpiredDataSubjectLeaseAsync(now, ct);

        DataSubjectRequest? request;
        if (_db.Database.IsSqlServer())
        {
            request = (await _db.DataSubjectRequests
                .FromSqlInterpolated($$"""
                    SELECT TOP (1) request.*
                    FROM [DataSubjectRequests] AS request WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE request.[Status] = N'accepted'
                      AND request.[AvailableAt] <= {{now}}
                      AND (request.[LeaseExpiresAt] IS NULL OR request.[LeaseExpiresAt] <= {{now}})
                    ORDER BY
                        CASE WHEN request.[DeadlineAt] IS NULL THEN 1 ELSE 0 END,
                        request.[DeadlineAt],
                        request.[AvailableAt],
                        request.[RequestedAt],
                        request.[Id]
                    """)
                .AsTracking()
                .ToListAsync(ct))
                .FirstOrDefault();
        }
        else
        {
            request = await _db.DataSubjectRequests
                .Where(item => item.Status == DataSubjectRequestStatuses.Accepted &&
                    item.AvailableAt <= now &&
                    (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now))
                .OrderBy(item => item.DeadlineAt == null)
                .ThenBy(item => item.DeadlineAt)
                .ThenBy(item => item.AvailableAt)
                .ThenBy(item => item.RequestedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (request == null)
        {
            if (transaction != null) await transaction.CommitAsync(ct);
            return null;
        }

        var expiresAt = now.Add(leaseDuration);
        request.Status = DataSubjectRequestStatuses.Collecting;
        request.LeaseOwner = workerId;
        request.LeaseExpiresAt = expiresAt;
        request.StartedAt ??= now;
        request.AttemptCount++;
        request.LastErrorCode = null;
        request.LastErrorMessage = null;
        await _db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return new PrivacyWorkLease(PrivacyWorkKinds.DataSubjectRequest, request.Id, request.AttemptCount, expiresAt);
    }

    private async Task RecoverExpiredRetentionLeaseAsync(DateTimeOffset now, CancellationToken ct)
    {
        var action = await _db.PrivacyRetentionActions
            .Where(item => item.Status == PrivacyWorkerStatuses.Running &&
                item.LeaseExpiresAt != null && item.LeaseExpiresAt <= now)
            .OrderBy(item => item.LeaseExpiresAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(ct);
        if (action == null)
        {
            return;
        }

        action.LeaseOwner = null;
        action.LeaseExpiresAt = null;
        action.LastErrorCode = "PRIVACY_WORKER_LEASE_EXPIRED";
        action.LastErrorMessage = "The retention worker lease expired before completion.";
        if (action.AttemptCount < action.MaxAttempts)
        {
            action.Status = PrivacyWorkerStatuses.Pending;
            action.AvailableAt = now;
        }
        else
        {
            action.Status = PrivacyWorkerStatuses.Failed;
            action.CompletedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RecoverExpiredDataSubjectLeaseAsync(DateTimeOffset now, CancellationToken ct)
    {
        var request = await _db.DataSubjectRequests
            .Where(item => item.Status == DataSubjectRequestStatuses.Collecting &&
                item.LeaseExpiresAt != null && item.LeaseExpiresAt <= now)
            .OrderBy(item => item.LeaseExpiresAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(ct);
        if (request == null)
        {
            return;
        }

        request.LeaseOwner = null;
        request.LeaseExpiresAt = null;
        request.LastErrorCode = "PRIVACY_WORKER_LEASE_EXPIRED";
        request.LastErrorMessage = "The data-subject worker lease expired before completion.";
        if (request.AttemptCount < request.MaxAttempts)
        {
            request.Status = DataSubjectRequestStatuses.Accepted;
            request.AvailableAt = now;
        }
        else
        {
            request.Status = DataSubjectRequestStatuses.Failed;
            request.CompletedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ReleaseLeaseAsync(
        PrivacyWorkLease lease,
        string workerId,
        CancellationToken ct = default)
    {
        if (lease.Kind == PrivacyWorkKinds.Retention)
        {
            var action = await _db.PrivacyRetentionActions.FirstOrDefaultAsync(
                item =>
                    item.Id == lease.WorkId &&
                    item.Status == PrivacyWorkerStatuses.Running &&
                    item.LeaseOwner == workerId,
                ct);
            if (action == null) return false;

            action.Status = PrivacyWorkerStatuses.Pending;
            action.LeaseOwner = null;
            action.LeaseExpiresAt = null;
            action.AvailableAt = DateTimeOffset.UtcNow;
            action.LastErrorCode = PrivacyErrorCodes.WorkerPaused;
            action.LastErrorMessage = "The host stopped before privacy work reached a terminal state.";
        }
        else
        {
            var request = await _db.DataSubjectRequests.FirstOrDefaultAsync(
                item =>
                    item.Id == lease.WorkId &&
                    item.Status == DataSubjectRequestStatuses.Collecting &&
                    item.LeaseOwner == workerId,
                ct);
            if (request == null) return false;

            request.Status = DataSubjectRequestStatuses.Accepted;
            request.LeaseOwner = null;
            request.LeaseExpiresAt = null;
            request.AvailableAt = DateTimeOffset.UtcNow;
            request.LastErrorCode = PrivacyErrorCodes.WorkerPaused;
            request.LastErrorMessage = "The host stopped before privacy work reached a terminal state.";
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    private DateTimeOffset RetryAt(DateTimeOffset now, int attemptCount)
    {
        var delay = Math.Min(3600, Math.Max(1, _options.BaseRetrySeconds) * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return now.AddSeconds(delay);
    }

    private static string Limit(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];
}
