namespace Qaly.Application.Services;

public static class OrganizationRoleRules
{
    public const string Owner = "Owner";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string PrivacyOperator = "PrivacyOperator";
    public const string BillingAdmin = "BillingAdmin";
    public const string Member = "Member";

    public static bool CanManageOrganization(string? role)
        => string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, OrganizationAdmin, StringComparison.OrdinalIgnoreCase)
           // Backward compatibility for organization memberships created before v4.
           || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

    public static bool TryNormalizeAssignableRole(string? role, out string normalized)
    {
        normalized = Normalize(role);
        return string.Equals(normalized, OrganizationAdmin, StringComparison.Ordinal)
               || string.Equals(normalized, PrivacyOperator, StringComparison.Ordinal)
               || string.Equals(normalized, BillingAdmin, StringComparison.Ordinal)
               || (string.Equals(normalized, Member, StringComparison.Ordinal)
                   && (string.IsNullOrWhiteSpace(role) || string.Equals(role.Trim(), Member, StringComparison.OrdinalIgnoreCase)));
    }

    public static string Normalize(string? role)
    {
        var value = role?.Trim();
        if (string.Equals(value, Owner, StringComparison.OrdinalIgnoreCase)) return Owner;
        if (string.Equals(value, OrganizationAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Manager", StringComparison.OrdinalIgnoreCase)) return OrganizationAdmin;
        if (string.Equals(value, PrivacyOperator, StringComparison.OrdinalIgnoreCase)) return PrivacyOperator;
        if (string.Equals(value, BillingAdmin, StringComparison.OrdinalIgnoreCase)) return BillingAdmin;
        return Member;
    }
}
