using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class DashboardBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public DashboardBoundaryIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetOverview_WhenUserHasNoProjects_DoesNotLeakOtherWorkspaceData()
    {
        var otherUserId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();

        await EnsureUserExists(_factory.TestUserId, "Test User", "test-user@qaly.dev");
        await EnsureUserExists(otherUserId, "Foreign Owner", "foreign-owner@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = foreignProjectId,
                Name = "Secret Project",
                Code = $"secret-{Guid.NewGuid():N}",
                OwnerId = otherUserId
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/dashboard/overview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Projects.Should().BeEmpty();
        payload.Team.Should().ContainSingle(member => member.Id == _factory.TestUserId);
        payload.Team.Should().NotContain(member => member.Email == "foreign-owner@qaly.dev");
    }

    [Fact]
    public async Task GetRecentActivities_WhenUserCannotAccessForeignProject_HidesForeignEntries()
    {
        var otherUserId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var visibleTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var foreignTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1);

        await EnsureUserExists(_factory.TestUserId, "Test User", "test-user@qaly.dev");
        await EnsureUserExists(otherUserId, "Foreign Owner", "foreign-owner@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = foreignProjectId,
                Name = "Secret Project",
                Code = $"secret-{Guid.NewGuid():N}",
                OwnerId = otherUserId
            });

            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(User),
                    EntityId = _factory.TestUserId.ToString(),
                    UserId = _factory.TestUserId,
                    ChangesJson = """{"title":"Profile update"}""",
                    Timestamp = visibleTimestamp
                },
                new AuditLog
                {
                    Action = "Create",
                    EntityType = nameof(Project),
                    EntityId = foreignProjectId.ToString(),
                    UserId = otherUserId,
                    ChangesJson = $$"""{"projectId":"{{foreignProjectId}}","title":"Secret Project"}""",
                    Timestamp = foreignTimestamp
                });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/dashboard/recent-activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RecentActivitiesResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.LatestActivities.Should().ContainSingle();
        payload.LatestActivities.Should().Contain(activity => activity.ActorName == "Test User");
        payload.LatestActivities.Should().NotContain(activity => activity.ProjectId == foreignProjectId);
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(user => user.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = name,
                Email = email,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed record DashboardOverviewResponse(
        IReadOnlyList<DashboardProjectResponse> Projects,
        IReadOnlyList<DashboardMemberResponse> Team);

    private sealed record DashboardProjectResponse(Guid Id, string Name);

    private sealed record DashboardMemberResponse(Guid Id, string FullName, string Email);

    private sealed record RecentActivitiesResponse(
        int TodayCount,
        int WeekCount,
        IReadOnlyList<ActivityByDayResponse> ActivityByDay,
        IReadOnlyList<RecentActivityResponse> LatestActivities);

    private sealed record ActivityByDayResponse(string Date, int Count);

    private sealed record RecentActivityResponse(
        string Type,
        string Title,
        string ActorName,
        string? ProjectName,
        Guid? ProjectId,
        string CreatedAt);
}
#pragma warning restore CA1707
