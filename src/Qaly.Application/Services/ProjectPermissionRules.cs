using Qaly.Application.DTOs.Project;

namespace Qaly.Application.Services;

/// <summary>
/// Turns a project role into the capability set the UI renders from.
///
/// This mirrors what the endpoints enforce; it does not replace those checks. Keeping the mapping
/// in one place is what makes it possible to demonstrate each role's reach without hand-maintaining
/// a parallel list in the frontend.
/// </summary>
public static class ProjectPermissionRules
{
    public static ProjectPermissionsDto Resolve(string? projectRole, bool isOwner, bool isSystemAdmin)
    {
        var effectiveRole = isOwner
            ? ProjectRoleRules.Owner
            : ProjectRoleRules.NormalizeProjectRole(projectRole);

        var isMemberOfProject = isOwner || isSystemAdmin || !string.IsNullOrWhiteSpace(projectRole);
        var manages = isOwner || isSystemAdmin || ProjectRoleRules.CanManageProject(effectiveRole);
        var readOnly = !manages && (ProjectRoleRules.IsViewer(effectiveRole) || ProjectRoleRules.IsCustomer(effectiveRole));
        var canWrite = isMemberOfProject && !readOnly;

        var reviews = manages
            || string.Equals(effectiveRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal)
            || string.Equals(effectiveRole, ProjectRoleRules.Tester, StringComparison.Ordinal);

        var specialist = manages
            || string.Equals(effectiveRole, ProjectRoleRules.Developer, StringComparison.Ordinal)
            || string.Equals(effectiveRole, ProjectRoleRules.Tester, StringComparison.Ordinal)
            || string.Equals(effectiveRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal);

        var aiTier = AiCapabilityRules.ResolveTier(
            projectRole,
            isSystemAdmin: isSystemAdmin,
            isProjectOwner: isOwner);

        return new ProjectPermissionsDto(
            Role: isMemberOfProject ? effectiveRole : string.Empty,
            RoleLabel: DescribeRoleVietnamese(isMemberOfProject ? effectiveRole : null),
            CanManageProject: manages,
            CanManageMembers: manages,
            CanManageAllTasks: manages,
            CanCreateTask: specialist,
            CanUpdateOwnTasks: canWrite,
            CanComment: canWrite,
            CanTrackTime: canWrite,
            CanReviewEvidence: reviews,
            // Customers see only what is shared with them; everyone else on the project sees internal pages.
            CanReadInternalWiki: isMemberOfProject && !ProjectRoleRules.IsCustomer(effectiveRole),
            CanWriteWiki: canWrite,
            CanManageIntegrations: manages,
            AiTier: aiTier.ToString(),
            AiTierDescription: AiCapabilityRules.DescribeVietnamese(aiTier));
    }

    public static string DescribeRoleVietnamese(string? role) => role switch
    {
        ProjectRoleRules.Owner => "Chủ dự án",
        ProjectRoleRules.Manager => "Quản lý dự án",
        ProjectRoleRules.ScrumMaster => "Scrum Master",
        ProjectRoleRules.Developer => "Lập trình viên",
        ProjectRoleRules.Tester => "Kiểm thử viên",
        ProjectRoleRules.Reviewer => "Người review",
        ProjectRoleRules.Member => "Thành viên",
        ProjectRoleRules.Viewer => "Người xem",
        ProjectRoleRules.Customer => "Khách hàng",
        _ => "Không thuộc dự án"
    };
}
