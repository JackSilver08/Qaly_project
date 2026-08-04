namespace Qaly.Domain.Entities;

public sealed class MemberAvailabilityWindow : BaseEntity
{
    public const string Unavailable = "Unavailable";
    public const string ReducedCapacity = "ReducedCapacity";

    public Guid OrganizationMemberCapacityProfileId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Kind { get; set; } = Unavailable;
    public decimal? AvailableHours { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public OrganizationMemberCapacityProfile Profile { get; set; } = null!;
}
