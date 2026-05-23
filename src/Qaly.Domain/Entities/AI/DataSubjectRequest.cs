using System;

namespace Qaly.Domain.Entities;

public class DataSubjectRequest : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? RequesterUserId { get; set; }
    public string RequestType { get; set; } = null!; // export|delete|anonymize
    public string ScopeJson { get; set; } = null!;
    public string Status { get; set; } = "pending"; // pending|approved|processing|completed|rejected
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? EvidenceUri { get; set; }
}