using System.Threading;
using System.Threading.Tasks;

namespace Qaly.Application.Services;

public interface IAiComplianceService
{
    /// <summary>
    /// Evaluates if the current data can be sent to a cloud AI provider based on policies and user consents.
    /// If sensitive=true and no override/consent is provided, this should return false.
    /// </summary>
    Task<bool> CanProcessInCloudAsync(Guid? tenantId, Guid? projectId, Guid? userId, bool isSensitive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an AI-related audit event (e.g. data export, sensitive data processing, model fallback).
    /// </summary>
    Task LogAuditEventAsync(Guid? tenantId, Guid? projectId, Guid? actorUserId, string eventType, string entityType, long? entityId, string? beforeJson, string? afterJson, CancellationToken cancellationToken = default);
}