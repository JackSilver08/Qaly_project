using FluentAssertions;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services.Meetings;
using Qaly.Application.Services.Tasks;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class MeetingAndGanttCoverageTests
{
    [Fact]
    public void LiveKitTokenService_WhenConfigurationIsMissing_Throws()
    {
        var service = CreateLiveKitService(new LiveKitOptions());

        var act = () => service.CreateJoinToken(new LiveKitTokenRequest("room", "user", "User"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("LiveKit is not configured.");
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(120, 120)]
    [InlineData(2000, 1440)]
    public void LiveKitTokenService_CreatesTokenWithClampedTtl(int configuredMinutes, int expectedMinutes)
    {
        var service = CreateLiveKitService(new LiveKitOptions
        {
            ServerUrl = " wss://livekit.example.test ",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret-with-enough-entropy",
            TokenTtlMinutes = configuredMinutes
        });
        var before = DateTimeOffset.UtcNow.AddMinutes(expectedMinutes);

        var result = service.CreateJoinToken(new LiveKitTokenRequest(
            "qaly-room",
            "participant-1",
            "Qaly User",
            """{"userId":"participant-1"}"""));

        result.ServerUrl.Should().Be("wss://livekit.example.test");
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.Token.Split('.').Should().HaveCount(3);
        result.ExpiresAt.Should().BeOnOrAfter(before.AddSeconds(-2));
        result.ExpiresAt.Should().BeOnOrBefore(before.AddSeconds(2));
    }

    [Fact]
    public void GanttCriticalPathCalculator_WithEmptyOrUndatedTasks_DoesNotMarkCriticalPath()
    {
        var undated = new GanttTaskDto { Id = Guid.NewGuid(), Title = "Undated" };

        GanttCriticalPathCalculator.MarkCriticalPath([]);
        GanttCriticalPathCalculator.MarkCriticalPath([undated]);

        undated.IsCriticalPath.Should().BeFalse();
    }

    [Fact]
    public void GanttCriticalPathCalculator_MarksDependencyChainAndLeavesShortParallelTaskNonCritical()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var parallelId = Guid.NewGuid();
        var tasks = new List<GanttTaskDto>
        {
            CreateGanttTask(firstId, "Design", start, start.AddDays(2)),
            CreateGanttTask(secondId, "Build", start.AddDays(2), start.AddDays(4), firstId),
            CreateGanttTask(parallelId, "Notes", start, start.AddDays(1))
        };

        GanttCriticalPathCalculator.MarkCriticalPath(tasks);

        tasks.Single(task => task.Id == firstId).IsCriticalPath.Should().BeTrue();
        tasks.Single(task => task.Id == secondId).IsCriticalPath.Should().BeTrue();
        tasks.Single(task => task.Id == parallelId).IsCriticalPath.Should().BeFalse();
    }

    private static LiveKitTokenService CreateLiveKitService(LiveKitOptions options)
        => new(Options.Create(options));

    private static GanttTaskDto CreateGanttTask(
        Guid id,
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        params Guid[] dependencies)
        => new()
        {
            Id = id,
            Title = title,
            StartDate = start,
            EndDate = end,
            Dependencies = dependencies.ToList()
        };
}
#pragma warning restore CA1707
