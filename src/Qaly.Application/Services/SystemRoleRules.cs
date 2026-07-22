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
        => IsAdmin(role) || IsModerator(role);

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
