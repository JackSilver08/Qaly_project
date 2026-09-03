namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Hộp thư đến cho webhook GitHub. Bảo đảm idempotency (theo DeliveryId), cho
/// phép retry và audit. Endpoint webhook chỉ lưu vào đây rồi trả 2xx sớm; xử lý
/// nghiệp vụ chạy ở background. OrganizationId có thể null lúc nhận, được điền
/// sau khi phân giải từ installation.
/// </summary>
public class GitHubWebhookInbox : BaseEntity
{
    /// <summary>X-GitHub-Delivery — unique, dùng chống replay/trùng.</summary>
    public string DeliveryId { get; set; } = string.Empty;

    /// <summary>Loại event (push, pull_request, ...).</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>Installation ID nguồn (số) từ payload.</summary>
    public long? InstallationId { get; set; }

    /// <summary>Repository ID nguồn (số) từ payload.</summary>
    public long? RepositoryExternalId { get; set; }

    /// <summary>Tenant phân giải được (điền sau khi xử lý).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Payload JSON hoặc phần metadata cần thiết.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Pending, Processing, Processed, Failed.</summary>
    public string Status { get; set; } = GitHubWebhookInboxStatuses.Pending;

    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    /// <summary>Identifies the worker that currently owns this delivery.</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>
    /// Upper bound for the current claim. A different worker may reclaim a Processing
    /// delivery only after this instant, which makes a crashed worker recoverable.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public static class GitHubWebhookInboxStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
}
