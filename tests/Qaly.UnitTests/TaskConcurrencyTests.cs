using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public class TaskConcurrencyTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public TaskConcurrencyTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleRowVersion_ReturnsConflict()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var initialRowVersion = new byte[] { 1, 2, 3 };

        _context.Users.Add(new User { Id = ownerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Test Project", OwnerId = ownerId });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Initial Title",
            Status = "Todo",
            RowVersion = initialRowVersion
        });
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        var service = CreateService();

        var staleRowVersion = Convert.ToBase64String(new byte[] { 0, 0, 0 });
        var updateDto = new UpdateTaskDto(
            Title: "Updated Title",
            Description: null,
            Status: "Todo",
            Priority: "Medium",
            DueDate: null,
            EstimatedHours: null,
            ActualHours: null,
            AssigneeId: null,
            IsPrivate: false,
            RowVersion: staleRowVersion);

        // Act
        var result = await service.UpdateAsync(taskId, updateDto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("modified by another request");
    }

    [Fact]
    public async Task MoveOnKanbanAsync_MultipleMoves_StabilizesSortOrder()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var task1Id = Guid.NewGuid();
        var task2Id = Guid.NewGuid();
        var task3Id = Guid.NewGuid();

        _context.Users.Add(new User { Id = ownerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Test Project", OwnerId = ownerId });
        _context.TaskItems.AddRange(
            new TaskItem { Id = task1Id, ProjectId = projectId, ReporterId = ownerId, Title = "Task 1", Status = "Todo", SortOrder = 1000 },
            new TaskItem { Id = task2Id, ProjectId = projectId, ReporterId = ownerId, Title = "Task 2", Status = "Todo", SortOrder = 2000 },
            new TaskItem { Id = task3Id, ProjectId = projectId, ReporterId = ownerId, Title = "Task 3", Status = "Todo", SortOrder = 3000 }
        );
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        var service = CreateService();

        // Act 1: Move Task 3 to top
        await service.MoveOnKanbanAsync(projectId, new KanbanMoveRequest(task3Id, "Todo", "Todo", task1Id, null, null));

        // Act 2: Move Task 1 to middle (after task 3)
        await service.MoveOnKanbanAsync(projectId, new KanbanMoveRequest(task1Id, "Todo", "Todo", null, task3Id, null));

        // Assert
        var tasks = await _context.TaskItems
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        tasks[0].Id.Should().Be(task3Id);
        tasks[1].Id.Should().Be(task1Id);
        tasks[2].Id.Should().Be(task2Id);

        // Verify no duplicate sort orders
        tasks.Select(t => t.SortOrder).Distinct().Should().HaveCount(3);
        // Verify rebalancing (multiples of 1000)
        tasks[0].SortOrder.Should().Be(1000);
        tasks[1].SortOrder.Should().Be(2000);
        tasks[2].SortOrder.Should().Be(3000);
    }

    private TaskService CreateService()
    {
        var projectRepo = new GenericRepository<Project>(_context);
        var memberRepo = new GenericRepository<ProjectMember>(_context);
        var organizationMemberRepo = new GenericRepository<OrganizationMember>(_context);
        var accessPolicy = new TaskAccessPolicy(_currentUser.Object, projectRepo, memberRepo, organizationMemberRepo);
        
        return new TaskService(
            new GenericRepository<TaskItem>(_context),
            new GenericRepository<TaskDependency>(_context),
            projectRepo,
            memberRepo,
            new GenericRepository<User>(_context),
            new GenericRepository<TaskAttachment>(_context),
            new GenericRepository<TaskAssignment>(_context),
            new GenericRepository<TaskViewEvent>(_context),
            new GenericRepository<TaskLabel>(_context),
            new GenericRepository<ProjectLabel>(_context),
            new GenericRepository<Sprint>(_context),
            new GenericRepository<VectorSyncOutbox>(_context),
            new UnitOfWork(_context),
            accessPolicy,
            Mock.Of<INotificationService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ITaskPrioritySuggestionService>(),
            Mock.Of<IWebhookPublisher>(),
            _currentUser.Object);
    }
}
