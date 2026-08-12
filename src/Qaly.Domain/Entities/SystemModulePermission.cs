namespace Qaly.Domain.Entities;

/// <summary>
/// Quản lý phân quyền hiển thị Module & AI Access ở Tầng Hệ Thống (System-Level)
/// </summary>
public class SystemModulePermission : BaseEntity
{
    /// <summary>
    /// System role áp dụng (Admin, Manager, User, Member...) hoặc null nếu là User override cụ thể
    /// </summary>
    public string? SystemRole { get; set; }

    /// <summary>
    /// ID của User nếu đây là cấu hình User Override cụ thể (User-level override)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Khóa module hệ thống: Dashboard, Projects, WorkGroups, Wiki, AiHub, AuditLogs, Settings...
    /// </summary>
    public string ModuleKey { get; set; } = string.Empty;

    /// <summary>
    /// Quyền truy cập module: true = Cho phép, false = Cấm (Explicit Deny)
    /// </summary>
    public bool IsAllowed { get; set; } = true;

    /// <summary>
    /// Quyền cấp độ AI: "Full" (Full capabilities), "SummaryOnly" (Chỉ xem tóm tắt), "Restricted" (Cấm AI)
    /// </summary>
    public string AiTier { get; set; } = "Full";

    // Navigation property (nếu có UserId)
    public User? User { get; set; }
}
