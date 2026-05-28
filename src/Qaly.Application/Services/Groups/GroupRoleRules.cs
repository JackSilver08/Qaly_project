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

    public static bool IsValid(string? role)
        => string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, Member, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

    public static bool CanManage(string? role)
        => string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);

    public static bool CanChangeMemberRole(string? actorRole, string? targetRole)
    {
        var normalizedActorRole = Normalize(actorRole);
        var normalizedTargetRole = Normalize(targetRole);

        if (normalizedActorRole == Owner)
        {
            return true;
        }

        if (normalizedActorRole == Admin)
        {
            return normalizedTargetRole == Member;
        }

        return false;
    }

    public static bool CanRemoveMember(string? actorRole, string? targetRole, bool isSelfAction)
    {
        var normalizedActorRole = Normalize(actorRole);
        var normalizedTargetRole = Normalize(targetRole);

        if (isSelfAction)
        {
            return true;
        }

        if (normalizedActorRole == Owner)
        {
            return true;
        }

        if (normalizedActorRole == Admin)
        {
            return normalizedTargetRole == Member;
        }

        return false;
    }

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
