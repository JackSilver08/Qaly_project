using FluentAssertions;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class AiCapabilityRulesTests
{
    [Theory]
    [InlineData("Owner", AiCapabilityTier.Full)]
    [InlineData("Manager", AiCapabilityTier.Full)]
    [InlineData("ScrumMaster", AiCapabilityTier.Full)]
    [InlineData("Developer", AiCapabilityTier.Specialist)]
    [InlineData("Tester", AiCapabilityTier.Specialist)]
    [InlineData("Reviewer", AiCapabilityTier.Specialist)]
    [InlineData("Member", AiCapabilityTier.Contributor)]
    [InlineData("Viewer", AiCapabilityTier.ReadOnly)]
    [InlineData("Customer", AiCapabilityTier.ReadOnly)]
    [InlineData(null, AiCapabilityTier.None)]
    [InlineData("", AiCapabilityTier.None)]
    public void ResolveTier_MapsEveryProjectRole(string? role, AiCapabilityTier expected)
    {
        AiCapabilityRules.ResolveTier(role).Should().Be(expected);
    }

    [Fact]
    public void ResolveTier_SystemAdminAndProjectOwnerGetFullTier()
    {
        AiCapabilityRules.ResolveTier("Viewer", isSystemAdmin: true).Should().Be(AiCapabilityTier.Full);
        AiCapabilityRules.ResolveTier(null, isProjectOwner: true).Should().Be(AiCapabilityTier.Full);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Developer")]
    [InlineData("Tester")]
    [InlineData("Reviewer")]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [InlineData("Customer")]
    public void EveryProjectRoleMayReadProgressAndSummary(string role)
    {
        // The customer requirement: members must not be locked out of progress and summary.
        var tier = AiCapabilityRules.ResolveTier(role);
        AiCapabilityRules.Allows(tier, AiCapabilityRules.ProgressAndSummaryTier).Should().BeTrue();
    }

    [Fact]
    public void NonMemberCannotReadProgress()
    {
        var tier = AiCapabilityRules.ResolveTier(null);
        AiCapabilityRules.Allows(tier, AiCapabilityRules.ProgressAndSummaryTier).Should().BeFalse();
    }

    [Theory]
    [InlineData("Member", false)]
    [InlineData("Viewer", false)]
    [InlineData("Customer", false)]
    [InlineData("Developer", true)]
    [InlineData("Tester", true)]
    [InlineData("Reviewer", true)]
    [InlineData("Manager", true)]
    public void RolesAboveMemberUnlockTeamAnalysis(string role, bool expected)
    {
        var tier = AiCapabilityRules.ResolveTier(role);
        AiCapabilityRules.Allows(tier, AiCapabilityRules.TeamAnalysisTier).Should().Be(expected);
    }

    [Theory]
    [InlineData("Developer")]
    [InlineData("Tester")]
    [InlineData("Reviewer")]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [InlineData("Customer")]
    public void OnlyFullTierMayPlanAndStaff(string role)
    {
        var tier = AiCapabilityRules.ResolveTier(role);
        AiCapabilityRules.Allows(tier, AiCapabilityRules.PlanningTier).Should().BeFalse();
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Customer")]
    public void ReadOnlyRolesGetNoWriteTools(string role)
    {
        var tier = AiCapabilityRules.ResolveTier(role);
        var tools = AiCapabilityRules.AllowedToolNames(tier, isTaskAssignee: true);

        tools.Should().NotBeNull();
        tools!.Should().NotContain("AddComment");
        tools.Should().NotContain("StartTimeTracking");
        tools.Should().NotContain("StopTimeTracking");
        tools.Should().NotContain("CreateTask");
        tools.Should().NotContain("UpdateTaskStatus");
        tools.Should().Contain("GetProjectSummary");
    }

    [Fact]
    public void MemberSeesOwnWorkButNotOtherPeoplesWorkload()
    {
        var tier = AiCapabilityRules.ResolveTier("Member");
        var tools = AiCapabilityRules.AllowedToolNames(tier, isTaskAssignee: false);

        tools.Should().NotBeNull();
        tools!.Should().Contain("GetProjectSummary");
        tools.Should().Contain("GetMyTimeLogs");
        tools.Should().NotContain("GetMemberWorkload");
        tools.Should().NotContain("SuggestTaskAssignment");
        tools.Should().NotContain("AssignTask");
    }

    [Fact]
    public void MemberGetsStatusUpdateOnlyWhenAssignee()
    {
        var tier = AiCapabilityRules.ResolveTier("Member");

        AiCapabilityRules.AllowedToolNames(tier, isTaskAssignee: false)!
            .Should().NotContain(AiCapabilityRules.AssigneeOnlyTool);
        AiCapabilityRules.AllowedToolNames(tier, isTaskAssignee: true)!
            .Should().Contain(AiCapabilityRules.AssigneeOnlyTool);
    }

    [Fact]
    public void SpecialistSeesTeamWorkloadButNotStaffingSuggestions()
    {
        var tier = AiCapabilityRules.ResolveTier("Developer");
        var tools = AiCapabilityRules.AllowedToolNames(tier, isTaskAssignee: false);

        tools.Should().NotBeNull();
        tools!.Should().Contain("GetMemberWorkload");
        tools.Should().Contain("CreateTask");
        tools.Should().NotContain("SuggestTaskAssignment");
        tools.Should().NotContain("AssignTask");
    }

    [Fact]
    public void FullTierIsUnfiltered()
    {
        var tier = AiCapabilityRules.ResolveTier("Manager");
        AiCapabilityRules.AllowedToolNames(tier, isTaskAssignee: false).Should().BeNull();
    }

    [Fact]
    public void NonMemberGetsNoTools()
    {
        var tools = AiCapabilityRules.AllowedToolNames(AiCapabilityTier.None, isTaskAssignee: true);
        tools.Should().NotBeNull();
        tools!.Should().BeEmpty();
    }

    [Fact]
    public void TiersAreStrictlyOrderedSupersets()
    {
        var readOnly = AiCapabilityRules.AllowedToolNames(AiCapabilityTier.ReadOnly, false)!;
        var contributor = AiCapabilityRules.AllowedToolNames(AiCapabilityTier.Contributor, false)!;
        var specialist = AiCapabilityRules.AllowedToolNames(AiCapabilityTier.Specialist, false)!;

        contributor.Should().Contain(readOnly);
        specialist.Should().Contain(contributor);
        specialist.Count.Should().BeGreaterThan(contributor.Count);
        contributor.Count.Should().BeGreaterThan(readOnly.Count);
    }
}
#pragma warning restore CA1707
