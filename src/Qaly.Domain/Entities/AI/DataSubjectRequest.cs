using System;

namespace Qaly.Domain.Entities;

public class DataSubjectRequest : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? RequesterUserId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string RequestType { get; set; } = null!;
    public string ScopeJson { get; set; } = null!;
    public string Status { get; set; } = DataSubjectRequestStatuses.Submitted;
    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }
    public string? PolicyVersion { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? IdentityVerifiedAt { get; set; }
    public Guid? IdentityVerifiedById { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? EvidenceUri { get; set; }
    public string? EncryptedResultPayload { get; set; }
    public string? ResultSummaryJson { get; set; }
    public string? ResultContentType { get; set; }
    public string? ResultFileName { get; set; }
    public DateTimeOffset? ResultExpiresAt { get; set; }
    public int DownloadCount { get; set; }
    public DateTimeOffset? LastDownloadedAt { get; set; }
    public bool LegalHoldDetected { get; set; }
    public string? LegalHoldReason { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
