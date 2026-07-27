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
    [InlineData("Viewer", false)]
    public void CanWrite_ShouldAllowEditors(string? role, bool expected)
    {
        ProjectRoleRules.CanWrite(role).Should().Be(expected);
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
