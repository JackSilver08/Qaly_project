using FluentAssertions;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

public class OrganizationRoleRulesTests
{
    [Theory]
    [InlineData("Owner", true)]
    [InlineData("OrganizationAdmin", true)]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("PrivacyOperator", false)]
    [InlineData("BillingAdmin", false)]
    [InlineData("Member", false)]
    [InlineData("ScrumMaster", false)]
    public void CanManageOrganization_UsesOrganizationRolesOnly(string role, bool expected)
        => OrganizationRoleRules.CanManageOrganization(role).Should().Be(expected);

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("OrganizationAdmin", true)]
    [InlineData("BillingAdmin", true)]
    [InlineData("PrivacyOperator", false)]
    [InlineData("Member", false)]
    public void CanManageAiBudget_IncludesBillingRoleOnly(string role, bool expected)
        => OrganizationRoleRules.CanManageAiBudget(role).Should().Be(expected);

    [Theory]
    [InlineData("Admin", "OrganizationAdmin")]
    [InlineData("Manager", "OrganizationAdmin")]
    [InlineData("PrivacyOperator", "PrivacyOperator")]
    [InlineData("unknown", "Member")]
    public void Normalize_MigratesLegacyRoles(string role, string expected)
        => OrganizationRoleRules.Normalize(role).Should().Be(expected);

    [Theory]
    [InlineData("OrganizationAdmin", true)]
    [InlineData("PrivacyOperator", true)]
    [InlineData("BillingAdmin", true)]
    [InlineData("Member", true)]
    [InlineData("Owner", false)]
    [InlineData("ScrumMaster", false)]
    [InlineData("unknown", false)]
    public void TryNormalizeAssignableRole_PreventsOwnerAndProjectRoles(string role, bool expected)
        => OrganizationRoleRules.TryNormalizeAssignableRole(role, out _).Should().Be(expected);
}
