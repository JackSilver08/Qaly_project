namespace Qaly.Domain.Entities;

public class AiJobDispatch : BaseEntity
{
    public Guid AiJobId { get; set; }
    public int Priority { get; set; } = 100;
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public int DeliveryCount { get; set; }
    public string? LastDispatchErrorCode { get; set; }
    public string? LastDispatchError { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public AiJob AiJob { get; set; } = null!;
}
