using Qaly.Application.Services;

namespace Qaly.Application.Services.Groups;

public static class GroupRoleRules
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static string Normalize(string? role)
    {
        if (string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase))
        {
            return Owner;
        }

        if (string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            return Admin;
        }

        return Member;
    }

    public static bool CanManage(string? role)
        => string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);

    public static string ToProjectRole(string? groupRole)
    {
        var normalized = Normalize(groupRole);
        return normalized switch
        {
            Owner => ProjectRoleRules.Owner,
            Admin => ProjectRoleRules.Manager,
            _ => ProjectRoleRules.Member
        };
    }
}
