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
        var webhookEvents = await _context.WebhookOutboxMessages
            .AsNoTracking()
            .OrderBy(message => message.CreatedAt)
            .ToListAsync();
        webhookEvents.Should().HaveCount(2);
        webhookEvents.Should().OnlyContain(message =>
            message.ProjectId == projectId && message.EventType == "task.updated");
    }

    [Fact]
    public async Task BatchUpdateStatus_WhenAnyTaskIsUnauthorized_IsAtomic()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var ownTaskId = Guid.NewGuid();
        var protectedTaskId = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = "batch-owner@qaly.dev", IsActive = true },
            new User { Id = memberId, FullName = "Member", Email = "batch-member@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Batch project", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            Role = ProjectRoleRules.Member
        });
        _context.TaskItems.AddRange(
            new TaskItem { Id = ownTaskId, ProjectId = projectId, ReporterId = memberId, Title = "Own task", Status = "Todo" },
            new TaskItem { Id = protectedTaskId, ProjectId = projectId, ReporterId = ownerId, Title = "Protected task", Status = "Todo" });
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(user => user.UserId).Returns(memberId);
        var result = await CreateService().BatchUpdateStatusAsync(
            [ownTaskId, protectedTaskId],
            "InProgress");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await _context.TaskItems.AsNoTracking().SingleAsync(task => task.Id == ownTaskId)).Status.Should().Be("Todo");
        (await _context.TaskItems.AsNoTracking().SingleAsync(task => task.Id == protectedTaskId)).Status.Should().Be("Todo");
        (await _context.WebhookOutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BatchDelete_WhenAnyTaskIsMissing_DoesNotDeleteExistingTask()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var existingTaskId = Guid.NewGuid();
        _context.Users.Add(new User { Id = ownerId, FullName = "Owner", Email = "delete-owner@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Delete project", OwnerId = ownerId });
        _context.TaskItems.Add(new TaskItem
        {
            Id = existingTaskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Must survive",
            Status = "Todo"
        });
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        var result = await CreateService().BatchDeleteAsync([existingTaskId, Guid.NewGuid()]);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        (await _context.TaskItems.AsNoTracking().AnyAsync(task => task.Id == existingTaskId)).Should().BeTrue();
        (await _context.WebhookOutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WhenAuditStagingFails_DoesNotCommitTaskOrIntegrationSignals()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = ownerId,
            FullName = "Owner",
            Email = "task-audit-failure@qaly.dev",
            IsActive = true
        });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Atomic task project",
            OwnerId = ownerId
        });
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        var auditLogService = new Mock<IAuditLogService>();
        auditLogService
            .Setup(service => service.StageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated audit staging failure"));
        var countingUnitOfWork = new CountingUnitOfWork(_context);
        var request = new CreateTaskDto(
            "Must roll back",
            "The whole canonical graph must remain uncommitted.",
            "High",
            DateTimeOffset.UtcNow.AddDays(1),
            4,
            projectId,
            null);

        var action = () => CreateService(countingUnitOfWork, auditLogService.Object).CreateAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated audit staging failure");
        countingUnitOfWork.SaveCount.Should().Be(0);
        _context.ChangeTracker.Clear();
        (await _context.TaskItems.CountAsync()).Should().Be(0);
        (await _context.VectorSyncOutbox.CountAsync()).Should().Be(0);
        (await _context.WebhookOutboxMessages.CountAsync()).Should().Be(0);
    }

    private TaskService CreateService(
        IUnitOfWork? unitOfWork = null,
        IAuditLogService? auditLogService = null)
    {
        var projectRepo = new GenericRepository<Project>(_context);
        var memberRepo = new GenericRepository<ProjectMember>(_context);
        var organizationMemberRepo = new GenericRepository<OrganizationMember>(_context);
        var accessPolicy = new TaskAccessPolicy(_currentUser.Object, projectRepo, memberRepo, organizationMemberRepo,
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));
        
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
            new GenericRepository<WebhookOutboxMessage>(_context),
            unitOfWork ?? new UnitOfWork(_context),
            accessPolicy,
            Mock.Of<INotificationService>(),
            auditLogService ?? Mock.Of<IAuditLogService>(),
            Mock.Of<ITaskPrioritySuggestionService>(),
            _currentUser.Object);
    }

    private sealed class CountingUnitOfWork(QalyDbContext context) : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return await context.SaveChangesAsync(cancellationToken);
        }

        public void Dispose()
        {
        }
    }
}
