using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public static class AuditLogTransactionExtensions
{
    /// <summary>
    /// Stages the audit record before committing the shared unit of work. EF SaveChanges is
    /// transactional, so neither the canonical mutation nor its audit evidence can persist alone.
    /// </summary>
    public static async Task<int> SaveChangesWithAuditAsync(
        this IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        string action,
        string entityType,
        string entityId,
        object? changes = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(auditLogService);

        await auditLogService.StageAsync(action, entityType, entityId, changes, ct);
        return await unitOfWork.SaveChangesAsync(ct);
    }
}
