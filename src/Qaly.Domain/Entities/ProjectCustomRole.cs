namespace Qaly.Domain.Entities;

/// <summary>
/// Thể hiện vai trò chuyên sâu trong Dự án (PO, PM, Lead Dev, Backend Dev, Frontend Dev, QA/Tester, AI Engineer...)
/// </summary>
public class ProjectCustomRole : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorCode { get; set; } // Hex color badge e.g. #8B5CF6
    public bool IsSystemDefault { get; set; } // Role mặc định hệ thống hay role custom do Project Owner tạo

    /// <summary>
    /// Ma trận phân quyền JSON của Role này trong project (Kanban: View/Edit, Roadmap: View, Wiki: Hide...)
    /// </summary>
    public string PermissionMatrixJson { get; set; } = "{}";

    // Navigation properties
    public Project Project { get; set; } = null!;
    public ICollection<ProjectMemberRoleHistory> RoleHistories { get; set; } = new List<ProjectMemberRoleHistory>();
}
