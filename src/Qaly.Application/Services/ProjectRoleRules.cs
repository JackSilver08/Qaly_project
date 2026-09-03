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

    /// <summary>
    /// Roles a project manager may assign. <see cref="Owner"/> is excluded: ownership transfers
    /// through its own flow, not through membership updates.
    /// </summary>
    public static readonly IReadOnlyList<string> AssignableRoles =
    [
        Manager, ScrumMaster, Developer, Tester, Reviewer, Member, Viewer, Customer
    ];

    public static bool CanManageProject(string? projectRole)
        => IsProjectManager(projectRole);

    /// <summary>
    /// Every role except the read-only ones may contribute work.
    /// </summary>
    public static bool CanWrite(string? projectRole)
        => !string.IsNullOrWhiteSpace(projectRole)
           && !IsViewer(projectRole)
           && !IsCustomer(projectRole);

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
           || string.Equals(projectRole, "ProjectManager", StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, ScrumMaster, StringComparison.OrdinalIgnoreCase)
           || string.Equals(projectRole, "Admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strict normalization for write paths. Unlike <see cref="NormalizeProjectRole"/> this refuses
    /// unknown input instead of silently downgrading it to <see cref="Member"/>, so a typo in a role
    /// name surfaces as a 400 rather than as an unintended change of privilege.
    /// </summary>
    public static bool TryNormalizeAssignableRole(string? role, out string normalized)
    {
        normalized = NormalizeProjectRole(role);

        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        // NormalizeProjectRole falls back to Member, so an input that is not itself a Member alias
        // must be rejected rather than accepted as Member.
        if (string.Equals(normalized, Member, StringComparison.Ordinal)
            && !IsMemberAlias(role))
        {
            return false;
        }

        return AssignableRoles.Contains(normalized, StringComparer.Ordinal);
    }

    private static bool IsMemberAlias(string role)
    {
        var value = role.Trim();
        return string.Equals(value, Member, StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "Thanh vien", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "Thành viên", StringComparison.OrdinalIgnoreCase);
    }

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
            || string.Equals(normalized, "ProjectManager", StringComparison.OrdinalIgnoreCase)
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
            || string.Equals(normalized, "Thành viên", StringComparison.OrdinalIgnoreCase))
            return Member;

        return Member;
    }
}
