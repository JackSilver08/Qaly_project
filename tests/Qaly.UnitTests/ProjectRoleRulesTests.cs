using FluentAssertions;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class ProjectRoleRulesTests
{
    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Manager", true)]
    [InlineData("ProjectManager", true)]
    [InlineData("Admin", true)]
    [InlineData("Member", false)]
    [InlineData("Viewer", false)]
    [InlineData(null, false)]
    public void IsProjectManager_ShouldIdentifyManagers(string? role, bool expected)
    {
        ProjectRoleRules.IsProjectManager(role).Should().Be(expected);
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Manager", true)]
    [InlineData("Member", true)]
    [InlineData("ScrumMaster", true)]
    [InlineData("Developer", true)]
    [InlineData("Tester", true)]
    [InlineData("Reviewer", true)]
    [InlineData("Viewer", false)]
    [InlineData("Customer", false)]
    [InlineData(null, false)]
    public void CanWrite_ShouldAllowEveryoneExceptReadOnlyRoles(string? role, bool expected)
    {
        ProjectRoleRules.CanWrite(role).Should().Be(expected);
    }

    [Theory]
    [InlineData("Manager", "Manager")]
    [InlineData("ScrumMaster", "ScrumMaster")]
    [InlineData("Developer", "Developer")]
    [InlineData("Tester", "Tester")]
    [InlineData("Reviewer", "Reviewer")]
    [InlineData("Member", "Member")]
    [InlineData("Viewer", "Viewer")]
    [InlineData("Customer", "Customer")]
    [InlineData("Thành viên", "Member")]
    [InlineData("  developer  ", "Developer")]
    [InlineData("Project Manager", "Manager")]
    public void TryNormalizeAssignableRole_ShouldAcceptAssignableRoles(string input, string expected)
    {
        ProjectRoleRules.TryNormalizeAssignableRole(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("Backend Dev")]
    [InlineData("PO")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalizeAssignableRole_ShouldRejectUnknownRoleInsteadOfDowngradingToMember(string? input)
    {
        // Silently mapping an unrecognised role to Member is a privilege change the caller
        // never asked for; write paths must surface it as an error.
        ProjectRoleRules.TryNormalizeAssignableRole(input, out _).Should().BeFalse();
    }

    [Fact]
    public void TryNormalizeAssignableRole_ShouldRejectOwnerBecauseOwnershipHasItsOwnFlow()
    {
        ProjectRoleRules.TryNormalizeAssignableRole("Owner", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("Thành viên", "Member")]
    [InlineData("Project Manager", "Manager")]
    [InlineData("ProjectManager", "Manager")]
    [InlineData("Admin", "Manager")]
    [InlineData("  viewer  ", "Viewer")]
    [InlineData("Unknown", "Member")]
    [InlineData(null, "Member")]
    public void NormalizeProjectRole_ShouldMapRolesCorrectly(string? input, string expected)
    {
        ProjectRoleRules.NormalizeProjectRole(input).Should().Be(expected);
    }
}
#pragma warning restore CA1707
