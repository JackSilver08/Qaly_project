using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Comment;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public sealed class NotificationAndWikiPolicyTests : IDisposable
{
    private readonly QalyDbContext _context;

    public NotificationAndWikiPolicyTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task NotificationService_WithSameIdempotencyKey_CreatesOneNotification()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var publisher = new Mock<INotificationPublisher>();
        var pushSender = new Mock<Qaly.Application.Common.Interfaces.IPushSender>();
        var service = new NotificationService(
            new GenericRepository<Notification>(_context),
            new GenericRepository<PushSubscription>(_context),
            new UnitOfWork(_context),
            publisher.Object,
            pushSender.Object,
            NullLogger<NotificationService>.Instance);

        await service.CreateAsync(
            userId,
            "Bạn đã được giao nhiệm vụ \"API\".",
            "TaskAssigned",
            "info",
            taskId,
            nameof(TaskItem),
            "task-assigned-key");
        await service.CreateAsync(
            userId,
            "Bạn đã được giao nhiệm vụ \"API\".",
            "TaskAssigned",
            "info",
            taskId,
            nameof(TaskItem),
            "task-assigned-key");

        _context.Notifications.Should().ContainSingle();
        publisher.Verify(
            item => item.PublishAsync(userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WikiService_CustomerRole_OnlyReturnsPublicPages()
    {
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        SeedUser(ownerId, "Owner");
        SeedUser(customerId, "Customer");
        _context.Projects.Add(new Project { Id = projectId, Name = "Portal", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = customerId,
            Role = ProjectRoleRules.Customer
        });
        _context.WikiPages.AddRange(
            new WikiPage
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AuthorId = ownerId,
                Title = "Public Handbook",
                Content = "visible",
                IsPublic = true,
                Visibility = "public"
            },
            new WikiPage
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AuthorId = ownerId,
                Title = "Internal Runbook",
                Content = "internal",
                IsPublic = false,
                Visibility = "internal"
            });
        await _context.SaveChangesAsync();

        var currentUser = CurrentUser(customerId);
        var service = CreateWikiService(currentUser);

        var result = await service.GetByProjectAsync(projectId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data.Should().ContainSingle();
        result.Data!.Single().Title.Should().Be("Public Handbook");
        result.Data!.Single().Visibility.Should().Be("public");
    }

    [Fact]
    public async Task CommentService_MentionedUser_DoesNotReceiveDuplicateCommentNotification()
    {
        var ownerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        SeedUser(ownerId, "Owner");
        SeedUser(authorId, "Author");
        SeedUser(reporterId, "Reporter");
        SeedUser(assigneeId, "Assignee");
        _context.Projects.Add(new Project { Id = projectId, Name = "Notifications", OwnerId = ownerId });
        _context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = authorId, Role = ProjectRoleRules.Member },
            new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = ProjectRoleRules.Member });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = reporterId,
            AssigneeId = assigneeId,
            Title = "Review API",
            Status = "Todo"
        });
        await _context.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        var service = new CommentService(
            new GenericRepository<TaskComment>(_context),
            new GenericRepository<TaskItem>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<VectorSyncOutbox>(_context),
            new UnitOfWork(_context),
            CurrentUser(authorId).Object,
            notifications.Object,
            Mock.Of<IAuditLogService>());

        var result = await service.CreateAsync(new CreateCommentDto("Nhờ bạn xem giúp", taskId, MentionedUserIds: [assigneeId]));

        result.IsSuccess.Should().BeTrue(result.Error);
        notifications.Verify(
            item => item.CreateAsync(
                assigneeId,
                It.IsAny<string>(),
                "Mentioned",
                "info",
                taskId,
                nameof(TaskItem),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(
            item => item.CreateAsync(
                assigneeId,
                It.IsAny<string>(),
                "CommentAdded",
                "info",
                taskId,
                nameof(TaskItem),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        notifications.Verify(
            item => item.CreateAsync(
                reporterId,
                It.IsAny<string>(),
                "CommentAdded",
                "info",
                taskId,
                nameof(TaskItem),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private WikiService CreateWikiService(Mock<ICurrentUserService> currentUser)
    {
        var projectRepo = new GenericRepository<Project>(_context);
        var memberRepo = new GenericRepository<ProjectMember>(_context);
        var accessPolicy = new TaskAccessPolicy(
            currentUser.Object,
            projectRepo,
            memberRepo,
            new GenericRepository<OrganizationMember>(_context));

        return new WikiService(
            new GenericRepository<WikiPage>(_context),
            projectRepo,
            new GenericRepository<User>(_context),
            accessPolicy,
            new UnitOfWork(_context),
            Mock.Of<IAuditLogService>());
    }

    private void SeedUser(Guid id, string name)
        => _context.Users.Add(new User { Id = id, FullName = name, Email = $"{name.ToLowerInvariant()}@qaly.dev", IsActive = true });

    private static Mock<ICurrentUserService> CurrentUser(Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.Role).Returns("User");
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        return currentUser;
    }
}
#pragma warning restore CA1707
