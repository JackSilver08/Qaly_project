using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class DashboardSummaryServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly ProjectDashboardSummaryRepository _repository;
    private readonly Mock<ITaskAccessPolicy> _taskAccessPolicy = new();

    public DashboardSummaryServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new QalyDbContext(options);
        _repository = new ProjectDashboardSummaryRepository(_context);

        _taskAccessPolicy
            .Setup(policy => policy.ApplyVisibilityFilter(It.IsAny<IQueryable<TaskItem>>()))
            .Returns<IQueryable<TaskItem>>(query => query);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetProjectSummaryAsync_WithValidData_ReturnsExpectedMetrics()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Project A",
            Code = "proj-a",
            OwnerId = ownerId
        });

        var todoTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Todo",
            Status = "Todo",
            Priority = "Medium",
            DueDate = null
        };

        var inProgressOverdueTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "In progress overdue",
            Status = "InProgress",
            Priority = "High",
            DueDate = now.AddHours(-3)
        };

        var doneTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Done",
            Status = "Done",
            Priority = "Medium",
            DueDate = now.AddHours(10)
        };

        var cancelledTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Cancelled",
            Status = "Cancelled",
            Priority = "Low",
            DueDate = now.AddHours(10)
        };

        var inReviewDueSoonTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "In review due soon",
            Status = "InReview",
            Priority = "Critical",
            DueDate = now.AddHours(2)
        };

        var onHoldWithAssignment = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "On hold assigned",
            Status = "OnHold",
            Priority = "Low",
            DueDate = now.AddHours(30),
            AssigneeId = null
        };

        _context.TaskItems.AddRange(todoTask, inProgressOverdueTask, doneTask, cancelledTask, inReviewDueSoonTask, onHoldWithAssignment);
        _context.TaskAssignments.Add(new TaskAssignment
        {
            Id = Guid.NewGuid(),
            TaskItemId = onHoldWithAssignment.Id,
            UserId = assigneeId
        });
        await _context.SaveChangesAsync();

        _taskAccessPolicy
            .Setup(policy => policy.CanAccessProjectAsync(projectId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new DashboardSummaryService(_repository, _taskAccessPolicy.Object);

        // Act
        var result = await service.GetProjectSummaryAsync(projectId);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        var data = result.Data!;

        data.Metrics.TotalTasks.Should().Be(6);
        data.Metrics.OpenTasks.Should().Be(4);
        data.Metrics.BacklogTasks.Should().Be(1);
        data.Metrics.InProgressTasks.Should().Be(1);
        data.Metrics.DoneTasks.Should().Be(1);
        data.Metrics.CancelledTasks.Should().Be(1);
        data.Metrics.CompletionRate.Should().Be(20.00m);
        data.Metrics.OverdueTasks.Should().Be(1);
        data.Metrics.DueSoon24h.Should().Be(1);
        data.DataQuality.MissingDueDateOpen.Should().Be(1);
        data.DataQuality.MissingAssigneeOpen.Should().Be(3);

        data.StatusBreakdown.Should().Contain(item => item.Status == "Todo" && item.Count == 1);
        data.StatusBreakdown.Should().Contain(item => item.Status == "InProgress" && item.Count == 1);
        data.StatusBreakdown.Should().Contain(item => item.Status == "InReview" && item.Count == 1);
        data.StatusBreakdown.Should().Contain(item => item.Status == "OnHold" && item.Count == 1);
        data.StatusBreakdown.Should().Contain(item => item.Status == "Done" && item.Count == 1);
        data.StatusBreakdown.Should().Contain(item => item.Status == "Cancelled" && item.Count == 1);
    }

    [Fact]
    public async Task GetProjectSummaryAsync_WhenCompletionDenominatorIsZero_ReturnsZeroCompletionRate()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Project B",
            Code = "proj-b",
            OwnerId = ownerId
        });

        _context.TaskItems.AddRange(
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ReporterId = ownerId,
                Title = "Cancelled",
                Status = "Cancelled",
                Priority = "Medium",
                ContributesToProgress = true
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ReporterId = ownerId,
                Title = "Non-progress Todo",
                Status = "Todo",
                Priority = "Medium",
                ContributesToProgress = false
            });
        await _context.SaveChangesAsync();

        _taskAccessPolicy
            .Setup(policy => policy.CanAccessProjectAsync(projectId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new DashboardSummaryService(_repository, _taskAccessPolicy.Object);

        // Act
        var result = await service.GetProjectSummaryAsync(projectId);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Metrics.CompletionRate.Should().Be(0m);
    }

    [Fact]
    public async Task GetProjectSummaryAsync_WhenFromGreaterThanTo_ReturnsBadRequest()
    {
        // Arrange
        var service = new DashboardSummaryService(_repository, _taskAccessPolicy.Object);
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);

        // Act
        var result = await service.GetProjectSummaryAsync(Guid.NewGuid(), from, to);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }
}
#pragma warning restore CA1707
