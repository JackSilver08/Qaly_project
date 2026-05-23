using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class DashboardV2ControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public DashboardV2ControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(user => user.Id == userId))
        {
            db.Users.Add(new User { Id = userId, FullName = name, Email = email, IsActive = true });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetSummary_WithValidProject_ReturnsExpectedMvpMetrics()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = _factory.TestUserId;
        var now = DateTimeOffset.UtcNow;

        await EnsureUserExists(ownerId, "Test User", "test@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Dashboard Project",
                Code = $"dash-{Guid.NewGuid():N}",
                OwnerId = ownerId
            });

            db.TaskItems.AddRange(
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = "Todo task",
                    Status = "Todo",
                    Priority = "Medium",
                    DueDate = null
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = "In progress overdue",
                    Status = "InProgress",
                    Priority = "High",
                    DueDate = now.AddHours(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = "Done task",
                    Status = "Done",
                    Priority = "Low",
                    DueDate = now.AddHours(1)
                });

            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync($"/api/dashboard/v2/projects/{projectId}/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<ProjectDashboardSummaryResponse>>();
        payload!.IsSuccess.Should().BeTrue(payload.Error);
        payload.Data.Should().NotBeNull();
        payload.Data!.Metrics.TotalTasks.Should().Be(3);
        payload.Data.Metrics.OpenTasks.Should().Be(2);
        payload.Data.Metrics.BacklogTasks.Should().Be(1);
        payload.Data.Metrics.InProgressTasks.Should().Be(1);
        payload.Data.Metrics.DoneTasks.Should().Be(1);
        payload.Data.Metrics.CancelledTasks.Should().Be(0);
        payload.Data.Metrics.OverdueTasks.Should().Be(1);
        payload.Data.Metrics.DueSoon24h.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_WhenFromGreaterThanTo_ReturnsBadRequest()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = _factory.TestUserId;
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);

        await EnsureUserExists(ownerId, "Test User", "test@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Validation Project",
                Code = $"validation-{Guid.NewGuid():N}",
                OwnerId = ownerId
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync($"/api/dashboard/v2/projects/{projectId}/summary?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);

    private sealed record ProjectDashboardSummaryResponse(ProjectDashboardMetricsResponse Metrics);

    private sealed record ProjectDashboardMetricsResponse(
        int TotalTasks,
        int OpenTasks,
        int BacklogTasks,
        int InProgressTasks,
        int DoneTasks,
        int CancelledTasks,
        decimal CompletionRate,
        int OverdueTasks,
        int DueSoon24h);
}
#pragma warning restore CA1707
