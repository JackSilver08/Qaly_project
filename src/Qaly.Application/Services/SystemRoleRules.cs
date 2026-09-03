namespace Qaly.Application.Services;

public static class SystemRoleRules
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Member = "Member";

    public static bool IsAdmin(string? role)
        => string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsModerator(string? role)
        => string.Equals(role, Moderator, StringComparison.OrdinalIgnoreCase);

    public static bool CanManageUsers(string? role)
        => IsAdmin(role);

    /// <summary>
    /// Normalizes roles already stored on a user. "User" and the legacy system-level "Manager"
    /// value are retained as read-compatible aliases for Member. Project/organization management
    /// authority is still resolved independently, so this compatibility path never grants system
    /// administration privileges. Unknown roles fail closed.
    /// </summary>
    public static bool TryNormalizeKnownRole(string? role, out string normalized)
    {
        if (IsAdmin(role))
        {
            normalized = Admin;
            return true;
        }

        if (IsModerator(role))
        {
            normalized = Moderator;
            return true;
        }

        if (string.Equals(role, Member, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "User", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Member;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    public static bool TryNormalizeAssignableRole(string? role, out string normalized)
    {
        if (IsModerator(role))
        {
            normalized = Moderator;
            return true;
        }

        if (string.IsNullOrWhiteSpace(role) || string.Equals(role, Member, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Member;
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
