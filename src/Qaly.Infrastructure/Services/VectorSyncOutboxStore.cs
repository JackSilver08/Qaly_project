using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public interface IVectorSyncOutboxStore
{
    Task<VectorSyncOutbox?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken ct = default);

    Task<bool> RenewLeaseAsync(
        Guid outboxId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default);

    Task CompleteAsync(VectorSyncOutbox item, string workerId, CancellationToken ct = default);
    Task RecordFailureAsync(
        VectorSyncOutbox item,
        string workerId,
        string failureMessage,
        int maxAttempts,
        TimeSpan baseRetryDelay,
        CancellationToken ct = default);
    Task<bool> ReleaseLeaseAsync(Guid outboxId, string workerId, CancellationToken ct = default);
}

/// <summary>
/// Durable multi-instance queue for vector synchronization. A conditional update
/// grants one owner, while the correlated predecessor check serializes events for
/// each aggregate so an update cannot overtake an earlier delete/update.
/// </summary>
public sealed class VectorSyncOutboxStore : IVectorSyncOutboxStore
{
    private const int ClaimContentionRetries = 8;
    private const string ExhaustedLeaseError =
        "The vector sync lease expired after the final permitted processing attempt.";
    private const string InvalidContractError =
        "The vector sync event has an invalid event type or aggregate identity.";
    private static readonly string[] SupportedEventTypes = [.. VectorSyncEventTypes.Supported];

    private readonly QalyDbContext _db;

    public VectorSyncOutboxStore(QalyDbContext db) => _db = db;

    public async Task<VectorSyncOutbox?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        var now = DateTimeOffset.UtcNow;
        await DeadLetterInvalidContractsAsync(now, ct);
        await RecoverExhaustedLeasesAsync(now, maxAttempts, ct);

        for (var contentionAttempt = 0; contentionAttempt < ClaimContentionRetries; contentionAttempt++)
        {
            now = DateTimeOffset.UtcNow;
            var candidateId = await Eligible(now, maxAttempts)
                .AsNoTracking()
                .OrderBy(item => item.SequenceNumber)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(ct);
            if (candidateId == null) return null;

            if (_db.Database.IsRelational())
            {
                var claimed = await Eligible(now, maxAttempts)
                    .Where(item => item.Id == candidateId.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.RetryCount, item => item.RetryCount + 1)
                        .SetProperty(item => item.LeaseOwner, workerId)
                        .SetProperty(item => item.LeaseExpiresAt, now.Add(leaseDuration))
                        .SetProperty(item => item.ErrorMessage, (string?)null)
                        .SetProperty(item => item.UpdatedAt, now), ct);
                if (claimed == 0) continue;
            }
            else
            {
                var tracked = await _db.VectorSyncOutbox.FindAsync([candidateId.Value], ct);
                if (tracked == null || !IsEligible(tracked, now, maxAttempts)) continue;
                tracked.RetryCount++;
                tracked.LeaseOwner = workerId;
                tracked.LeaseExpiresAt = now.Add(leaseDuration);
                tracked.ErrorMessage = null;
                await _db.SaveChangesAsync(ct);
                return tracked;
            }

            return await _db.VectorSyncOutbox.SingleAsync(
                item => item.Id == candidateId.Value && item.LeaseOwner == workerId,
                ct);
        }

        return null;
    }

    public async Task<bool> RenewLeaseAsync(
        Guid outboxId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;

        if (_db.Database.IsRelational())
        {
            return await _db.VectorSyncOutbox
                .Where(item => item.Id == outboxId &&
                               item.ProcessedAt == null &&
                               item.DeadLetteredAt == null &&
                               item.LeaseOwner == workerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAt, now.Add(leaseDuration))
                    .SetProperty(item => item.UpdatedAt, now), ct) == 1;
        }

        var item = await _db.VectorSyncOutbox.FindAsync([outboxId], ct);
        if (item?.LeaseOwner != workerId || item.ProcessedAt != null || item.DeadLetteredAt != null)
            return false;
        item.LeaseExpiresAt = now.Add(leaseDuration);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task CompleteAsync(
        VectorSyncOutbox item,
        string workerId,
        CancellationToken ct = default)
    {
        EnsureOwned(item, workerId);
        item.ProcessedAt = DateTimeOffset.UtcNow;
        item.ErrorMessage = null;
        item.LeaseOwner = null;
        item.LeaseExpiresAt = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordFailureAsync(
        VectorSyncOutbox item,
        string workerId,
        string failureMessage,
        int maxAttempts,
        TimeSpan baseRetryDelay,
        CancellationToken ct = default)
    {
        EnsureOwned(item, workerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseRetryDelay, TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;
        item.ErrorMessage = Truncate(failureMessage);
        item.LeaseOwner = null;
        item.LeaseExpiresAt = null;

        if (item.RetryCount >= maxAttempts)
        {
            item.DeadLetteredAt = now;
        }
        else
        {
            var retrySeconds = Math.Min(
                900,
                baseRetryDelay.TotalSeconds * Math.Pow(2, Math.Max(0, item.RetryCount - 1)));
            item.NextAttemptAt = now.AddSeconds(retrySeconds);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ReleaseLeaseAsync(
        Guid outboxId,
        string workerId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var now = DateTimeOffset.UtcNow;
        if (_db.Database.IsRelational())
        {
            return await _db.VectorSyncOutbox
                .Where(item => item.Id == outboxId &&
                               item.ProcessedAt == null &&
                               item.DeadLetteredAt == null &&
                               item.LeaseOwner == workerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseOwner, (string?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.ErrorMessage, (string?)null)
                    .SetProperty(item => item.NextAttemptAt, now)
                    .SetProperty(item => item.UpdatedAt, now), ct) == 1;
        }

        var item = await _db.VectorSyncOutbox.FindAsync([outboxId], ct);
        if (item?.LeaseOwner != workerId || item.ProcessedAt != null || item.DeadLetteredAt != null)
            return false;
        item.LeaseOwner = null;
        item.LeaseExpiresAt = null;
        item.ErrorMessage = null;
        item.NextAttemptAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<VectorSyncOutbox> Eligible(DateTimeOffset now, int maxAttempts)
        => _db.VectorSyncOutbox.Where(item =>
            item.ProcessedAt == null &&
            item.DeadLetteredAt == null &&
            item.RetryCount < maxAttempts &&
            item.NextAttemptAt <= now &&
            (item.LeaseOwner == null ||
             (item.LeaseExpiresAt != null && item.LeaseExpiresAt <= now)) &&
            !_db.VectorSyncOutbox.Any(predecessor =>
                predecessor.AggregateType == item.AggregateType &&
                predecessor.AggregateId == item.AggregateId &&
                predecessor.SequenceNumber < item.SequenceNumber &&
                predecessor.ProcessedAt == null &&
                predecessor.DeadLetteredAt == null));

    private static bool IsEligible(VectorSyncOutbox item, DateTimeOffset now, int maxAttempts)
        => item.ProcessedAt == null &&
           item.DeadLetteredAt == null &&
           item.RetryCount < maxAttempts &&
           item.NextAttemptAt <= now &&
           (item.LeaseOwner == null ||
            (item.LeaseExpiresAt != null && item.LeaseExpiresAt <= now));

    private async Task DeadLetterInvalidContractsAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.VectorSyncOutbox
                .Where(item => item.ProcessedAt == null &&
                               item.DeadLetteredAt == null &&
                               (!SupportedEventTypes.Contains(item.EventType) ||
                                item.AggregateType == "" ||
                                item.AggregateId == Guid.Empty))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DeadLetteredAt, now)
                    .SetProperty(item => item.ErrorMessage, InvalidContractError)
                    .SetProperty(item => item.LeaseOwner, (string?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now), ct);
            return;
        }

        var invalid = await _db.VectorSyncOutbox
            .Where(item => item.ProcessedAt == null && item.DeadLetteredAt == null)
            .ToListAsync(ct);
        invalid = invalid.Where(item =>
            !VectorSyncEventTypes.Supported.Contains(item.EventType) ||
            string.IsNullOrWhiteSpace(item.AggregateType) ||
            item.AggregateId == Guid.Empty).ToList();
        foreach (var item in invalid)
        {
            item.DeadLetteredAt = now;
            item.ErrorMessage = InvalidContractError;
            item.LeaseOwner = null;
            item.LeaseExpiresAt = null;
        }
        if (invalid.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private async Task RecoverExhaustedLeasesAsync(
        DateTimeOffset now,
        int maxAttempts,
        CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.VectorSyncOutbox
                .Where(item => item.ProcessedAt == null &&
                               item.DeadLetteredAt == null &&
                               item.LeaseOwner != null &&
                               item.LeaseExpiresAt != null &&
                               item.LeaseExpiresAt <= now &&
                               item.RetryCount >= maxAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DeadLetteredAt, now)
                    .SetProperty(item => item.ErrorMessage, ExhaustedLeaseError)
                    .SetProperty(item => item.LeaseOwner, (string?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now), ct);
            return;
        }

        var exhausted = await _db.VectorSyncOutbox
            .Where(item => item.ProcessedAt == null &&
                           item.DeadLetteredAt == null &&
                           item.LeaseOwner != null &&
                           item.LeaseExpiresAt != null &&
                           item.LeaseExpiresAt <= now &&
                           item.RetryCount >= maxAttempts)
            .ToListAsync(ct);
        foreach (var item in exhausted)
        {
            item.DeadLetteredAt = now;
            item.ErrorMessage = ExhaustedLeaseError;
            item.LeaseOwner = null;
            item.LeaseExpiresAt = null;
        }
        if (exhausted.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private static void EnsureOwned(VectorSyncOutbox item, string workerId)
    {
        if (item.ProcessedAt != null || item.DeadLetteredAt != null || item.LeaseOwner != workerId)
            throw new DbUpdateConcurrencyException("The vector sync lease is no longer owned by this worker.");
    }

    private static string Truncate(string value)
        => value.Length <= 2000 ? value : value[..2000];
}
