using System.ComponentModel.DataAnnotations;

namespace Qaly.Domain.Entities;

public class PushSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public string Endpoint { get; set; } = null!;

    public string? P256dh { get; set; }
    public string? Auth { get; set; }

    public string Device { get; set; } = "Unknown";
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
}
