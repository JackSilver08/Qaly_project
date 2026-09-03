using FluentAssertions;
using Qaly.Application.Services.Tasks;

namespace Qaly.UnitTests;

public class TaskStatusRulesTests
{
    [Theory]
    [InlineData("Todo", true)]
    [InlineData("InProgress", true)]
    [InlineData("InReview", true)]
    [InlineData("OnHold", true)]
    [InlineData("Done", false)]
    [InlineData("Cancelled", false)]
    [InlineData("Completed", false)]
    [InlineData("Closed", false)]
    [InlineData(null, false)]
    public void IsOpen_UsesCanonicalOpenStatuses(string? status, bool expected)
    {
        TaskStatusRules.IsOpen(status).Should().Be(expected);
    }

    [Theory]
    [InlineData("Done", true)]
    [InlineData("done", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Canceled", true)]
    [InlineData("Completed", true)]
    [InlineData("Closed", true)]
    [InlineData("Todo", false)]
    [InlineData(null, false)]
    public void IsClosed_RecognizesTerminalStatuses(string? status, bool expected)
    {
        TaskStatusRules.IsClosed(status).Should().Be(expected);
    }

    [Theory]
    [InlineData("Todo", true)]
    [InlineData("InProgress", true)]
    [InlineData("InReview", true)]
    [InlineData("OnHold", true)]
    [InlineData("Done", false)]
    [InlineData("Cancelled", false)]
    public void IsOverdue_OnlyCountsPastDueOpenTasks(string status, bool expected)
    {
        var now = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

        TaskStatusRules.IsOverdue(status, now.AddMinutes(-1), now).Should().Be(expected);
    }

    [Fact]
    public void IsOverdue_RejectsMissingOrFutureDueDate()
    {
        var now = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

        TaskStatusRules.IsOverdue("Todo", null, now).Should().BeFalse();
        TaskStatusRules.IsOverdue("Todo", now, now).Should().BeFalse();
        TaskStatusRules.IsOverdue("Todo", now.AddMinutes(1), now).Should().BeFalse();
    }

    [Fact]
    public void IsDueSoon_UsesOpenStatusAndExclusiveBoundary()
    {
        var now = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
        var boundary = now.AddHours(24);

        TaskStatusRules.IsDueSoon("Todo", now, now, boundary).Should().BeTrue();
        TaskStatusRules.IsDueSoon("OnHold", boundary.AddTicks(-1), now, boundary).Should().BeTrue();
        TaskStatusRules.IsDueSoon("Cancelled", now.AddHours(1), now, boundary).Should().BeFalse();
        TaskStatusRules.IsDueSoon("Done", now.AddHours(1), now, boundary).Should().BeFalse();
        TaskStatusRules.IsDueSoon("Todo", boundary, now, boundary).Should().BeFalse();
    }
}
