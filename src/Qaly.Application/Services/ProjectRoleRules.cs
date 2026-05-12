namespace Qaly.Application.Services;

internal static class ProjectRoleRules
{
    public const string SystemAdmin = "Admin";
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Member = "Member";
    public const string Viewer = "Viewer";

    public static bool IsSystemAdmin(string? role)
        => string.Equals(role, SystemAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool CanManageProject(string? projectRole)
        => IsProjectManager(projectRole);

    public static bool CanWrite(string? projectRole)
        => IsProjectManager(projectRole) || string.Equals(projectRole, Member, StringComparison.OrdinalIgnoreCase);

    public static bool IsViewer(string? projectRole)
        => string.Equals(projectRole, Viewer, StringComparison.OrdinalIgnoreCase);

    public static bool IsProjectManager(string? projectRole)
        => string.Equals(projectRole, Owner, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, Manager, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, "Admin", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeProjectRole(string? role)
    {
        var normalized = string.IsNullOrWhiteSpace(role) ? Member : role.Trim();
        return normalized switch
        {
            Owner => Owner,
            Manager => Manager,
            "Admin" => Manager,
            Member => Member,
            Viewer => Viewer,
            "Project Manager" => Manager,
            "Thành viên" => Member,
            _ => Member
        };
    }
}
