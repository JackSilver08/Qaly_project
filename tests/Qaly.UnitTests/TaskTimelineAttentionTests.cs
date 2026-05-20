using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class TaskTimelineAttentionTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public TaskTimelineAttentionTests()
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
    public async Task GetAttentionByProjectAsync_AssigneeCanSeeOwnOverdueTask()
    {
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        SeedUsers(ownerId, assigneeId);
        _context.Projects.Add(new Project { Id = projectId, Name = "Dự án", Code = "du-an", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = "Developer" });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            AssigneeId = assigneeId,
            Title = "Hoàn thiện màn timeline",
            Status = "InProgress",
            Priority = "High",
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            DueDate = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();
        _currentUser.SetupGet(user => user.UserId).Returns(assigneeId);
        _currentUser.SetupGet(user => user.Role).Returns("User");

        var service = CreateService();

        var result = await service.GetAttentionByProjectAsync(projectId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items[0].IsOverdue.Should().BeTrue();
        result.Data.Items[0].Reasons.Should().Contain("QuaHan");
    }

    [Fact]
    public async Task MarkViewedAsync_WhenViewedAfterAssignment_RemovesUnseenReason()
    {
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        SeedUsers(ownerId, assigneeId);
        _context.Projects.Add(new Project { Id = projectId, Name = "Dự án", Code = "du-an", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = "Developer" });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Đọc yêu cầu mới",
            Status = "Todo",
            Priority = "Medium",
            StartDate = DateTimeOffset.UtcNow.AddHours(-1)
        });
        _context.TaskAssignments.Add(new TaskAssignment
        {
            TaskItemId = taskId,
            UserId = assigneeId,
            AssignedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            AssignedByUserId = ownerId
        });
        await _context.SaveChangesAsync();
        _currentUser.SetupGet(user => user.UserId).Returns(assigneeId);
        _currentUser.SetupGet(user => user.Role).Returns("User");

        var service = CreateService();
        (await service.GetAttentionByProjectAsync(projectId)).Data!.Items[0].Reasons.Should().Contain("ChuaXem");

        var markViewed = await service.MarkViewedAsync(projectId, taskId);
        markViewed.IsSuccess.Should().BeTrue(markViewed.Error);

        var afterViewed = await service.GetAttentionByProjectAsync(projectId);
        afterViewed.Data!.Items[0].Reasons.Should().NotContain("ChuaXem");
    }

    private void SeedUsers(Guid ownerId, Guid assigneeId)
    {
        _context.Users.Add(new User { Id = ownerId, FullName = "PM", Email = "pm@qaly.dev", Role = "User", IsActive = true });
        _context.Users.Add(new User { Id = assigneeId, FullName = "Dev", Email = "dev@qaly.dev", Role = "User", IsActive = true });
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
            new GenericRepository<VectorSyncOutbox>(_context),
            new UnitOfWork(_context),
            accessPolicy,
            Mock.Of<INotificationService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ITaskPrioritySuggestionService>(),
            Mock.Of<IWebhookPublisher>());
    }
}
#pragma warning restore CA1707
