using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public class AiComplianceService : IAiComplianceService
{
    private readonly QalyDbContext _context;

    public AiComplianceService(QalyDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanProcessInCloudAsync(Guid? tenantId, Guid? projectId, Guid? userId, bool isSensitive, CancellationToken cancellationToken = default)
    {
        if (!isSensitive) return true;

        // Check if there is an explicit policy allowing cloud processing for sensitive data
        var policy = await _context.AiBudgetPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProjectId == projectId, cancellationToken);

        if (policy != null && policy.AllowCloudForSensitive)
        {
            return true;
        }

        // Check user consent
        if (userId.HasValue)
        {
            var consent = await _context.PrivacyConsents
                .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ConsentType == "ai_cloud_processing" && c.Status == "granted", cancellationToken);
            
            if (consent != null)
            {
                return true;
            }
        }

        return false;
    }

    public async Task LogAuditEventAsync(Guid? tenantId, Guid? projectId, Guid? actorUserId, string eventType, string entityType, long? entityId, string? beforeJson, string? afterJson, CancellationToken cancellationToken = default)
    {
        var auditEvent = new AiAuditEvent
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ActorUserId = actorUserId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        };

        _context.AiAuditEvents.Add(auditEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }
}