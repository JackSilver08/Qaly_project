namespace Qaly.Application.Services;

public static class ProjectRoleRules
{
    public const string SystemAdmin = "Admin";
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string ScrumMaster = "ScrumMaster";
    public const string Developer = "Developer";
    public const string Tester = "Tester";
    public const string Reviewer = "Reviewer";
    public const string Member = "Member";
    public const string Viewer = "Viewer";
    public const string Customer = "Customer";

    public static bool IsSystemAdmin(string? role)
        => string.Equals(role, SystemAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool CanManageProject(string? projectRole)
        => IsProjectManager(projectRole);

    public static bool CanWrite(string? projectRole)
        => IsProjectManager(projectRole) || string.Equals(projectRole, Member, StringComparison.OrdinalIgnoreCase);

    public static bool IsViewer(string? projectRole)
        => string.Equals(projectRole, Viewer, StringComparison.OrdinalIgnoreCase);

    public static bool IsCustomer(string? projectRole)
        => string.Equals(projectRole, Customer, StringComparison.OrdinalIgnoreCase);

    public static bool IsProjectManager(string? projectRole)
        => string.Equals(projectRole, Owner, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, Manager, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, SystemAdmin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, "PM", StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, "ProjectOwner", StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, ScrumMaster, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, "Admin", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeProjectRole(string? role)
    {
        var normalized = string.IsNullOrWhiteSpace(role) ? Member : role.Trim();

        if (string.Equals(normalized, Owner, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ProjectOwner", StringComparison.OrdinalIgnoreCase))
            return Owner;

        if (string.Equals(normalized, Manager, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "PM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, SystemAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Project Manager", StringComparison.OrdinalIgnoreCase))
            return Manager;

        if (string.Equals(normalized, ScrumMaster, StringComparison.OrdinalIgnoreCase))
            return ScrumMaster;

        if (string.Equals(normalized, Developer, StringComparison.OrdinalIgnoreCase))
            return Developer;

        if (string.Equals(normalized, Tester, StringComparison.OrdinalIgnoreCase))
            return Tester;

        if (string.Equals(normalized, Reviewer, StringComparison.OrdinalIgnoreCase))
            return Reviewer;

        if (string.Equals(normalized, Viewer, StringComparison.OrdinalIgnoreCase))
            return Viewer;

        if (string.Equals(normalized, Customer, StringComparison.OrdinalIgnoreCase))
            return Customer;

        if (string.Equals(normalized, Member, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Thanh vien", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ThÃ nh viÃªn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Thành viên", StringComparison.OrdinalIgnoreCase))
            return Member;

        return Member;
    }
}
