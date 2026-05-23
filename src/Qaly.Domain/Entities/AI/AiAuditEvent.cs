using System;

namespace Qaly.Domain.Entities;

public class AiAuditEvent : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = null!;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public long? JobId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}