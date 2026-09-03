using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed record WebhookRetentionResult(int DeliveryLogsDeleted, int ProcessedOutboxDeleted);

public sealed class WebhookOperationsRetentionService
{
    private readonly QalyDbContext _db;
    private readonly IConfiguration _configuration;

    public WebhookOperationsRetentionService(QalyDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<WebhookRetentionResult> CleanupAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var deliveryRetentionDays = Math.Clamp(
            _configuration.GetValue("OperationalHealth:WebhookDeliveryRetentionDays", 30),
            7,
            365);
        var outboxRetentionDays = Math.Clamp(
            _configuration.GetValue("OperationalHealth:WebhookProcessedOutboxRetentionDays", 30),
            7,
            365);
        var deliveryCutoff = now.AddDays(-deliveryRetentionDays);
        var outboxCutoff = now.AddDays(-outboxRetentionDays);

        if (_db.Database.IsRelational())
        {
            // Operational retention must also cover history whose webhook/project was
            // soft-deleted; visibility query filters are for product reads, not cleanup.
            var deliveryDeleted = await _db.WebhookDeliveryLogs
                .IgnoreQueryFilters()
                .Where(log => log.CreatedAt < deliveryCutoff)
                .ExecuteDeleteAsync(cancellationToken);
            var outboxDeleted = await _db.WebhookOutboxMessages
                .Where(message =>
                    message.ProcessedAt != null &&
                    message.ProcessedAt < outboxCutoff &&
                    message.DeadLetteredAt == null)
                .ExecuteDeleteAsync(cancellationToken);
            return new WebhookRetentionResult(deliveryDeleted, outboxDeleted);
        }

        var oldDeliveries = await _db.WebhookDeliveryLogs
            .IgnoreQueryFilters()
            .Where(log => log.CreatedAt < deliveryCutoff)
            .ToListAsync(cancellationToken);
        var oldProcessedOutbox = await _db.WebhookOutboxMessages
            .Where(message =>
                message.ProcessedAt != null &&
                message.ProcessedAt < outboxCutoff &&
                message.DeadLetteredAt == null)
            .ToListAsync(cancellationToken);
        _db.WebhookDeliveryLogs.RemoveRange(oldDeliveries);
        _db.WebhookOutboxMessages.RemoveRange(oldProcessedOutbox);
        await _db.SaveChangesAsync(cancellationToken);
        return new WebhookRetentionResult(oldDeliveries.Count, oldProcessedOutbox.Count);
    }
}
