namespace Qaly.Domain.Entities;

/// <summary>
/// Server-owned delivery preference and delivery state for a Project digest.
/// A subscription is unique per user/project and is never represented by a
/// browser-only toggle.
/// </summary>
public sealed class ProjectDigestSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Cadence { get; set; } = "weekly";
    public int DayOfWeek { get; set; } = 1;
    public int LocalTimeMinutes { get; set; } = 540;
    public string TimeZoneId { get; set; } = "Asia/Saigon";
    public DateTimeOffset? NextDeliveryAt { get; set; }
    public DateTimeOffset? LastDeliveryAt { get; set; }
    public string LastDeliveryStatus { get; set; } = "never";
    public string? LastError { get; set; }
    public string? LastDeliveryKey { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public int Revision { get; set; } = 1;
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
}
