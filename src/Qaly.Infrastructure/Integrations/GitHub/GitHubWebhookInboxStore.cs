using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Integrations.GitHub;

public interface IGitHubWebhookInboxStore
{
    Task<GitHubWebhookInbox?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken ct = default);

    Task<bool> RenewLeaseAsync(
        Guid inboxId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default);

    Task CompleteAsync(GitHubWebhookInbox inbox, string workerId, CancellationToken ct = default);
    Task RecordFailureAsync(
        GitHubWebhookInbox inbox,
        string workerId,
        string failureMessage,
        TimeSpan baseRetryDelay,
        CancellationToken ct = default);
    Task<bool> ReleaseLeaseAsync(Guid inboxId, string workerId, CancellationToken ct = default);
}

/// <summary>
/// Durable, multi-instance claim lifecycle for the GitHub webhook inbox. Relational
/// claims use a conditional update so only one worker can win even when instances
/// read the same candidate concurrently. LeaseOwner is also an EF concurrency token,
/// preventing a stale worker from committing processor mutations after its lease was
/// reclaimed.
/// </summary>
public sealed class GitHubWebhookInboxStore : IGitHubWebhookInboxStore
{
    private const int ClaimContentionRetries = 8;
    private readonly QalyDbContext _db;

    public GitHubWebhookInboxStore(QalyDbContext db) => _db = db;

    public async Task<GitHubWebhookInbox?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        await RecoverExhaustedLeasesAsync(DateTimeOffset.UtcNow, maxAttempts, ct);

        for (var contentionAttempt = 0; contentionAttempt < ClaimContentionRetries; contentionAttempt++)
        {
            var now = DateTimeOffset.UtcNow;
            var candidateId = await Eligible(now, maxAttempts)
                .AsNoTracking()
                .OrderBy(item => item.ReceivedAt)
                .ThenBy(item => item.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(ct);
            if (candidateId == null) return null;

            if (_db.Database.IsRelational())
            {
                var claimed = await Eligible(now, maxAttempts)
                    .Where(item => item.Id == candidateId.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, GitHubWebhookInboxStatuses.Processing)
                        .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                        .SetProperty(item => item.LeaseOwner, workerId)
                        .SetProperty(item => item.LeaseExpiresAt, now.Add(leaseDuration))
                        .SetProperty(item => item.LastError, (string?)null)
                        .SetProperty(item => item.UpdatedAt, now), ct);
                if (claimed == 0) continue;
            }
            else
            {
                var tracked = await _db.GitHubWebhookInbox.FindAsync([candidateId.Value], ct);
                if (tracked == null || !IsEligible(tracked, now, maxAttempts)) continue;
                tracked.Status = GitHubWebhookInboxStatuses.Processing;
                tracked.AttemptCount++;
                tracked.LeaseOwner = workerId;
                tracked.LeaseExpiresAt = now.Add(leaseDuration);
                tracked.LastError = null;
                await _db.SaveChangesAsync(ct);
                return tracked;
            }

            return await _db.GitHubWebhookInbox.SingleAsync(
                item => item.Id == candidateId.Value &&
                        item.Status == GitHubWebhookInboxStatuses.Processing &&
                        item.LeaseOwner == workerId,
                ct);
        }

        return null;
    }

    public async Task<bool> RenewLeaseAsync(
        Guid inboxId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_db.Database.IsRelational())
        {
            return await _db.GitHubWebhookInbox
                .Where(item => item.Id == inboxId &&
                               item.Status == GitHubWebhookInboxStatuses.Processing &&
                               item.LeaseOwner == workerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAt, now.Add(leaseDuration))
                    .SetProperty(item => item.UpdatedAt, now), ct) == 1;
        }

        var inbox = await _db.GitHubWebhookInbox.FindAsync([inboxId], ct);
        if (inbox?.Status != GitHubWebhookInboxStatuses.Processing || inbox.LeaseOwner != workerId)
            return false;
        inbox.LeaseExpiresAt = now.Add(leaseDuration);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task CompleteAsync(GitHubWebhookInbox inbox, string workerId, CancellationToken ct = default)
    {
        EnsureOwned(inbox, workerId);
        inbox.Status = GitHubWebhookInboxStatuses.Processed;
        inbox.ProcessedAt = DateTimeOffset.UtcNow;
        inbox.LastError = null;
        inbox.LeaseOwner = null;
        inbox.LeaseExpiresAt = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordFailureAsync(
        GitHubWebhookInbox inbox,
        string workerId,
        string failureMessage,
        TimeSpan baseRetryDelay,
        CancellationToken ct = default)
    {
        EnsureOwned(inbox, workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseRetryDelay, TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;
        inbox.Status = GitHubWebhookInboxStatuses.Failed;
        inbox.LastError = failureMessage.Length <= 2000 ? failureMessage : failureMessage[..2000];
        inbox.LeaseOwner = null;
        inbox.LeaseExpiresAt = null;
        var retrySeconds = Math.Min(
            900,
            baseRetryDelay.TotalSeconds * Math.Pow(2, Math.Max(0, inbox.AttemptCount - 1)));
        inbox.NextAttemptAt = now.AddSeconds(retrySeconds);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ReleaseLeaseAsync(Guid inboxId, string workerId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_db.Database.IsRelational())
        {
            return await _db.GitHubWebhookInbox
                .Where(item => item.Id == inboxId &&
                               item.Status == GitHubWebhookInboxStatuses.Processing &&
                               item.LeaseOwner == workerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, GitHubWebhookInboxStatuses.Pending)
                    .SetProperty(item => item.LeaseOwner, (string?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LastError, (string?)null)
                    .SetProperty(item => item.NextAttemptAt, now)
                    .SetProperty(item => item.UpdatedAt, now), ct) == 1;
        }

        var inbox = await _db.GitHubWebhookInbox.FindAsync([inboxId], ct);
        if (inbox?.Status != GitHubWebhookInboxStatuses.Processing || inbox.LeaseOwner != workerId)
            return false;
        inbox.Status = GitHubWebhookInboxStatuses.Pending;
        inbox.LeaseOwner = null;
        inbox.LeaseExpiresAt = null;
        inbox.LastError = null;
        inbox.NextAttemptAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<GitHubWebhookInbox> Eligible(DateTimeOffset now, int maxAttempts)
        => _db.GitHubWebhookInbox.Where(item =>
            item.AttemptCount < maxAttempts &&
            (((item.Status == GitHubWebhookInboxStatuses.Pending ||
               item.Status == GitHubWebhookInboxStatuses.Failed) &&
              item.NextAttemptAt <= now) ||
             (item.Status == GitHubWebhookInboxStatuses.Processing &&
              item.LeaseExpiresAt != null && item.LeaseExpiresAt <= now)));

    private static bool IsEligible(GitHubWebhookInbox item, DateTimeOffset now, int maxAttempts)
        => item.AttemptCount < maxAttempts &&
           (((item.Status == GitHubWebhookInboxStatuses.Pending ||
              item.Status == GitHubWebhookInboxStatuses.Failed) &&
             item.NextAttemptAt <= now) ||
            (item.Status == GitHubWebhookInboxStatuses.Processing &&
             item.LeaseExpiresAt != null && item.LeaseExpiresAt <= now));

    private async Task RecoverExhaustedLeasesAsync(
        DateTimeOffset now,
        int maxAttempts,
        CancellationToken ct)
    {
        const string terminalError = "The worker lease expired after the final permitted delivery attempt.";
        if (_db.Database.IsRelational())
        {
            await _db.GitHubWebhookInbox
                .Where(item => item.Status == GitHubWebhookInboxStatuses.Processing &&
                               item.LeaseExpiresAt != null &&
                               item.LeaseExpiresAt <= now &&
                               item.AttemptCount >= maxAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, GitHubWebhookInboxStatuses.Failed)
                    .SetProperty(item => item.LeaseOwner, (string?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LastError, terminalError)
                    .SetProperty(item => item.UpdatedAt, now), ct);
            return;
        }

        var exhausted = await _db.GitHubWebhookInbox
            .Where(item => item.Status == GitHubWebhookInboxStatuses.Processing &&
                           item.LeaseExpiresAt != null &&
                           item.LeaseExpiresAt <= now &&
                           item.AttemptCount >= maxAttempts)
            .ToListAsync(ct);
        foreach (var item in exhausted)
        {
            item.Status = GitHubWebhookInboxStatuses.Failed;
            item.LeaseOwner = null;
            item.LeaseExpiresAt = null;
            item.LastError = terminalError;
        }
        if (exhausted.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private static void EnsureOwned(GitHubWebhookInbox inbox, string workerId)
    {
        if (inbox.Status != GitHubWebhookInboxStatuses.Processing || inbox.LeaseOwner != workerId)
            throw new DbUpdateConcurrencyException("The GitHub webhook lease is no longer owned by this worker.");
    }
}
