using FluentAssertions;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class OnboardingGuideRulesTests
{
    [Theory]
    [InlineData("Manager", "Quản lý dự án")]
    [InlineData("Developer", "Lập trình viên")]
    [InlineData("Member", "Thành viên")]
    [InlineData("Viewer", "Người xem")]
    [InlineData("Customer", "Khách hàng")]
    public void Build_LabelsTheRoleInVietnamese(string role, string expectedLabel)
    {
        OnboardingGuideRules.Build(role).RoleLabel.Should().Be(expectedLabel);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Developer")]
    [InlineData("Member")]
    [InlineData("Viewer")]
    public void Build_StaysShortEnoughToBeRead(string role)
    {
        var guide = OnboardingGuideRules.Build(role);

        guide.FirstActions.Should().HaveCountLessThanOrEqualTo(4);
        guide.Workflow.Should().HaveCount(5);
        guide.SampleQuestions.Should().HaveCountLessThanOrEqualTo(3);
        guide.Summary.Should().NotBeEmpty();

        foreach (var step in guide.FirstActions.Concat(guide.Workflow))
        {
            step.Title.Should().NotBeEmpty();
            step.Detail.Should().NotBeEmpty();
            step.Detail.Length.Should().BeLessThan(120, "guide cards must stay scannable");
        }
    }

    [Fact]
    public void Build_EveryRoleSeesTheSameWorkflow()
    {
        var manager = OnboardingGuideRules.Build("Manager").Workflow;
        var member = OnboardingGuideRules.Build("Member").Workflow;
        var viewer = OnboardingGuideRules.Build("Viewer").Workflow;

        member.Should().BeEquivalentTo(manager);
        viewer.Should().BeEquivalentTo(manager);
    }

    [Fact]
    public void Build_SuggestsOnlyQuestionsTheRolesTierCanAnswer()
    {
        var member = OnboardingGuideRules.Build("Member");
        var manager = OnboardingGuideRules.Build("Manager");

        // Staffing questions need the Full tier, so they must not be offered to a member.
        member.SampleQuestions.Should().NotContain(question => question.Contains("năng lực", StringComparison.Ordinal));
        manager.SampleQuestions.Should().Contain(question => question.Contains("năng lực", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ReadOnlyRoleIsNotToldToTrackTime()
    {
        var viewer = OnboardingGuideRules.Build("Viewer");

        viewer.FirstActions.Should()
            .NotContain(step => step.Title.Contains("chấm công", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_OwnerAndAdminGetTheManagementGuide()
    {
        var owner = OnboardingGuideRules.Build(null, isOwner: true);
        var admin = OnboardingGuideRules.Build("Member", isSystemAdmin: true);

        owner.RoleLabel.Should().Be("Chủ dự án");
        owner.FirstActions.Should().Contain(step => step.Title.Contains("Phân công", StringComparison.Ordinal));
        admin.SampleQuestions.Should().Contain(question => question.Contains("năng lực", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_NonMemberGetsNoAiPrompts()
    {
        OnboardingGuideRules.Build(null).SampleQuestions.Should().BeEmpty();
    }
}
#pragma warning restore CA1707
