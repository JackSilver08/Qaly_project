namespace Qaly.Application.Services;

public static class ModeratorCapabilities
{
    public const string UsersView = "organization.users.view";
    public const string UsersInvite = "organization.users.invite";
    public const string UsersUpdateRole = "organization.users.update_role";
    public const string UsersRemove = "organization.users.remove";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        UsersView,
        UsersInvite,
        UsersUpdateRole,
        UsersRemove
    };
}
