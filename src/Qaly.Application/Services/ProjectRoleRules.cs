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
           || string.Equals(projectRole, SystemAdmin, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeProjectRole(string? role)
    {
        var normalized = string.IsNullOrWhiteSpace(role) ? Member : role.Trim();

        if (string.Equals(normalized, Owner, StringComparison.OrdinalIgnoreCase))
            return Owner;

        if (string.Equals(normalized, Manager, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, SystemAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Project Manager", StringComparison.OrdinalIgnoreCase))
            return Manager;

        if (string.Equals(normalized, Viewer, StringComparison.OrdinalIgnoreCase))
            return Viewer;

        if (string.Equals(normalized, Member, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Thanh vien", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ThÃ nh viÃªn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Thành viên", StringComparison.OrdinalIgnoreCase))
            return Member;

        return Member;
    }
}
