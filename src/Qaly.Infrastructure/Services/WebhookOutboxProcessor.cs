using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed partial class WebhookOutboxProcessor
{
    internal const int MaxRetryCount = 5;
    // A single delivery can consume the publisher's full retry window. Keep the
    // lease comfortably above that window so another worker cannot concurrently
    // claim the same occurrence while the first worker is still delivering it.
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private readonly QalyDbContext _db;
    private readonly IWebhookPublisher _publisher;
    private readonly ILogger<WebhookOutboxProcessor> _logger;

    public WebhookOutboxProcessor(
        QalyDbContext db,
        IWebhookPublisher publisher,
        ILogger<WebhookOutboxProcessor>? logger = null)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger ?? NullLogger<WebhookOutboxProcessor>.Instance;
    }

    public async Task<bool> ProcessNextAsync(string workerId, CancellationToken ct = default)
    {
        var message = await ClaimNextAsync(workerId, ct);
        if (message == null) return false;

        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var receipt = await _publisher.PublishOutboxAsync(
                message.Id,
                message.ProjectId,
                message.EventType,
                document.RootElement.Clone(),
                ct);
            if (!receipt.IsComplete)
            {
                throw new InvalidOperationException(
                    $"Webhook delivery incomplete ({receipt.DeliveredCount}/{receipt.SubscriptionCount}): " +
                    string.Join(", ", receipt.FailedStatuses));
            }

            await CompleteAsync(message.Id, workerId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await TryReleaseClaimAsync(message.Id, workerId);
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(message.Id, workerId, exception.Message, ct);
        }

        return true;
    }

    private async Task<WebhookOutboxMessage?> ClaimNextAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidateId = await _db.WebhookOutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.ProcessedAt == null &&
                    message.DeadLetteredAt == null &&
                    message.RetryCount < MaxRetryCount &&
                    message.NextAttemptAt <= now &&
                    (message.LockedUntil == null || message.LockedUntil <= now))
                .OrderBy(message => message.NextAttemptAt)
                .ThenBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Select(message => (Guid?)message.Id)
                .FirstOrDefaultAsync(ct);
            if (candidateId == null) return null;

            if (_db.Database.IsRelational())
            {
                var claimed = await _db.WebhookOutboxMessages
                    .Where(message =>
                    message.Id == candidateId.Value &&
                    message.ProcessedAt == null &&
                    message.DeadLetteredAt == null &&
                    message.RetryCount < MaxRetryCount &&
                    message.NextAttemptAt <= now &&
                    (message.LockedUntil == null || message.LockedUntil <= now))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(message => message.LeaseOwner, workerId)
                        .SetProperty(message => message.LockedUntil, now.Add(LeaseDuration)), ct);
                if (claimed == 0) continue;
            }
            else
            {
                var tracked = await _db.WebhookOutboxMessages.FindAsync([candidateId.Value], ct);
                if (tracked == null || tracked.ProcessedAt != null || tracked.DeadLetteredAt != null ||
                    tracked.RetryCount >= MaxRetryCount || tracked.NextAttemptAt > now || tracked.LockedUntil > now)
                {
                    continue;
                }
                tracked.LeaseOwner = workerId;
                tracked.LockedUntil = now.Add(LeaseDuration);
                await _db.SaveChangesAsync(ct);
            }

            return await _db.WebhookOutboxMessages
                .AsNoTracking()
                .SingleAsync(message => message.Id == candidateId.Value && message.LeaseOwner == workerId, ct);
        }

        return null;
    }

    private async Task CompleteAsync(Guid messageId, string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (_db.Database.IsRelational())
        {
            await _db.WebhookOutboxMessages
                .Where(message => message.Id == messageId && message.LeaseOwner == workerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.ProcessedAt, now)
                    .SetProperty(message => message.LeaseOwner, (string?)null)
                    .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null)
                    .SetProperty(message => message.ErrorMessage, (string?)null), ct);
            return;
        }

        var message = await _db.WebhookOutboxMessages.FindAsync([messageId], ct);
        if (message?.LeaseOwner != workerId) return;
        message.ProcessedAt = now;
        message.LeaseOwner = null;
        message.LockedUntil = null;
        message.ErrorMessage = null;
        await _db.SaveChangesAsync(ct);
    }

    private async Task RecordFailureAsync(Guid messageId, string workerId, string error, CancellationToken ct)
    {
        var message = await _db.WebhookOutboxMessages
            .SingleOrDefaultAsync(item => item.Id == messageId && item.LeaseOwner == workerId, ct);
        if (message == null) return;

        message.RetryCount++;
        message.ErrorMessage = error.Length <= 2000 ? error : error[..2000];
        message.LeaseOwner = null;
        message.LockedUntil = null;
        if (message.RetryCount >= MaxRetryCount)
        {
            message.DeadLetteredAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var delaySeconds = Math.Min(300, Math.Pow(2, message.RetryCount));
            message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task TryReleaseClaimAsync(Guid messageId, string workerId)
    {
        try
        {
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            if (_db.Database.IsRelational())
            {
                await _db.WebhookOutboxMessages
                    .Where(message => message.Id == messageId && message.LeaseOwner == workerId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(message => message.LeaseOwner, (string?)null)
                        .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null), recoveryTimeout.Token);
                return;
            }

            var message = await _db.WebhookOutboxMessages.FindAsync([messageId], recoveryTimeout.Token);
            if (message?.LeaseOwner != workerId) return;
            message.LeaseOwner = null;
            message.LockedUntil = null;
            await _db.SaveChangesAsync(recoveryTimeout.Token);
        }
        catch (Exception exception)
        {
            ClaimReleaseFailed(_logger, messageId, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook outbox claim release failed for message {MessageId} during shutdown.")]
    private static partial void ClaimReleaseFailed(ILogger logger, Guid messageId, Exception exception);
}
