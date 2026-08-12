namespace Qaly.Domain.Entities;

/// <summary>
/// Lưu lịch sử vai trò chuyên môn của thành viên theo thời gian/giai đoạn dự án.
/// Quy tắc cứng: Tại một thời điểm, mỗi ProjectMemberId chỉ có duy nhất 1 record active (EndDate == null).
/// Được bảo vệ bởi DB Unique Filtered Index: IX_ProjectMemberRoleHistory_Active (ProjectMemberId) WHERE EndDate IS NULL.
/// </summary>
public class ProjectMemberRoleHistory : BaseEntity
{
    public Guid ProjectMemberId { get; set; }
    public Guid RoleId { get; set; }
    public string? PhaseName { get; set; }
    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndDate { get; set; } // null = Role đang Active
    public string? ReasonOrNote { get; set; }
    public Guid AssignedByUserId { get; set; }

    // Navigation properties
    public ProjectMember ProjectMember { get; set; } = null!;
    public ProjectCustomRole Role { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
}
