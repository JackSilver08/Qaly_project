namespace Qaly.Domain.Entities;

public sealed class OrganizationMemberCapacityProfile : BaseEntity
{
    public const decimal DefaultWeeklyCapacityHours = 40m;

    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public decimal WeeklyCapacityHours { get; set; } = DefaultWeeklyCapacityHours;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public byte[] RowVersion { get; set; } = [];

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<MemberAvailabilityWindow> AvailabilityWindows { get; set; } = new List<MemberAvailabilityWindow>();
}
