using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Comment;
using Qaly.Application.DTOs.Wiki;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Application.Services.Groups;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services;

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
        var publisher = new Mock<INotificationPublisher>();
        var pushSender = new Mock<Qaly.Application.Common.Interfaces.IPushSender>();
        var service = CreateNotificationService(publisher.Object, pushSender.Object);

        await service.CreateAsync(
            userId,
            "Bạn đã được giao nhiệm vụ \"API\".",
            "TaskAssigned",
            "info",
            null,
            null,
            "task-assigned-key");
        await service.CreateAsync(
            userId,
            "Bạn đã được giao nhiệm vụ \"API\".",
            "TaskAssigned",
            "info",
            null,
            null,
            "task-assigned-key");

        _context.Notifications.Should().ContainSingle();
        publisher.Verify(
            item => item.PublishAsync(userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotificationService_MarkAsRead_RejectsOtherUsersNotification()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Message = "Private notification",
            Type = "Info",
            Tone = "info",
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var service = CreateNotificationService();

        var result = await service.MarkAsReadAsync(attackerId, notification.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);

        var reloaded = await _context.Notifications.SingleAsync(item => item.Id == notification.Id);
        reloaded.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task NotificationService_SubscribePush_RejectsEndpointOwnedByAnotherUser()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        const string endpoint = "https://push.qaly.dev/subscriptions/shared-endpoint";
        _context.PushSubscriptions.Add(new PushSubscription
        {
            UserId = ownerId,
            Endpoint = endpoint,
            P256dh = "ownerP256dh_123",
            Auth = "ownerAuth_123"
        });
        await _context.SaveChangesAsync();
        var service = CreateNotificationService();

        var result = await service.SubscribePushAsync(
            attackerId,
            endpoint,
            "attackerP256dh_123",
            "attackerAuth_123");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        var persisted = await _context.PushSubscriptions.SingleAsync();
        persisted.UserId.Should().Be(ownerId);
        persisted.P256dh.Should().Be("ownerP256dh_123");
        persisted.Auth.Should().Be("ownerAuth_123");
    }

    [Fact]
    public async Task NotificationService_SubscribePush_RejectsNonHttpsEndpoint()
    {
        var service = CreateNotificationService();

        var result = await service.SubscribePushAsync(
            Guid.NewGuid(),
            "http://push.qaly.dev/insecure",
            "validP256dh_123",
            "validAuth_123");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        _context.PushSubscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task NotificationService_SubscribePush_SameOwnerRotatesKeysWithoutDuplicate()
    {
        var userId = Guid.NewGuid();
        const string endpoint = "https://push.qaly.dev/subscriptions/owned-endpoint";
        var service = CreateNotificationService();

        (await service.SubscribePushAsync(userId, endpoint, "firstP256dh_123", "firstAuth_123"))
            .IsSuccess.Should().BeTrue();
        (await service.SubscribePushAsync(userId, endpoint, "secondP256dh_123", "secondAuth_123"))
            .IsSuccess.Should().BeTrue();

        var persisted = await _context.PushSubscriptions.SingleAsync();
        persisted.UserId.Should().Be(userId);
        persisted.P256dh.Should().Be("secondP256dh_123");
        persisted.Auth.Should().Be("secondAuth_123");
    }

    [Fact]
    public async Task NotificationService_SendPush_SwallowsAsyncProviderFailure()
    {
        var pushSender = new Mock<Qaly.Application.Common.Interfaces.IPushSender>();
        pushSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unavailable"));
        var service = CreateNotificationService(pushSender: pushSender.Object);

        var action = () => service.SendPushNotificationAsync(Guid.NewGuid(), "Title", "Message");

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotificationService_PrivateTask_IsHiddenUntilRecipientHasAccess_AndReturnsCanonicalTarget()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SeedUser(ownerId, "Owner");
        SeedUser(memberId, "Member");
        _context.Projects.Add(new Project { Id = projectId, Name = "Private", Code = "PRI", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = memberId, Role = ProjectRoleRules.Member });
        var task = new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Restricted evidence",
            IsPrivate = true
        };
        _context.TaskItems.Add(task);
        _context.Notifications.Add(new Notification
        {
            UserId = memberId,
            Message = "Restricted task changed",
            Type = "TaskStatusChanged",
            RelatedEntityId = taskId,
            RelatedEntityType = nameof(TaskItem)
        });
        await _context.SaveChangesAsync();
        var service = CreateNotificationService();

        (await service.GetByUserAsync(memberId)).Data.Should().BeEmpty();

        task.AssigneeId = memberId;
        await _context.SaveChangesAsync();
        var visible = await service.GetByUserAsync(memberId);
        visible.Data.Should().ContainSingle();
        visible.Data!.Single().TargetUrl.Should().Be($"/projects/{projectId}/tasks/{taskId}");
    }

    [Fact]
    public async Task NotificationService_ScansPastHiddenRecentRows_AndCountsAllVisibleRowsInBatches()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "Recipient");
        var now = DateTimeOffset.UtcNow;
        _context.Notifications.AddRange(Enumerable.Range(0, 120).Select(index => new Notification
        {
            UserId = userId,
            Message = $"Hidden {index}",
            Type = "Unsupported",
            RelatedEntityId = Guid.NewGuid(),
            RelatedEntityType = "UnsupportedEntity",
            CreatedAt = now.AddSeconds(index)
        }));
        _context.Notifications.AddRange(Enumerable.Range(0, 300).Select(index => new Notification
        {
            UserId = userId,
            Message = $"Visible {index}",
            Type = "Info",
            CreatedAt = now.AddMinutes(-10).AddSeconds(index)
        }));
        await _context.SaveChangesAsync();
        var service = CreateNotificationService();

        var page = await service.GetByUserAsync(userId);
        var unread = await service.GetUnreadCountAsync(userId);

        page.Data.Should().HaveCount(50);
        page.Data.Should().OnlyContain(item => item.Message.StartsWith("Visible ", StringComparison.Ordinal));
        unread.Data.Should().Be(300);
    }

    [Fact]
    public async Task NotificationTargetResolver_PrivateTask_AllowsVerifiedCustomManagerRole()
    {
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        const string roleKey = "delivery-lead";
        SeedUser(ownerId, "Owner");
        SeedUser(managerId, "Delivery Lead");
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Delivery Organization",
            Code = "DELIVERY",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = managerId,
            Role = OrganizationRoleRules.Member
        });
        _context.ProjectRoleDefinitions.Add(new ProjectRoleDefinition
        {
            OrganizationId = organizationId,
            Key = roleKey,
            DisplayName = "Delivery lead",
            BaseRole = ProjectRoleRules.Manager,
            CreatedByUserId = ownerId,
            IsActive = true
        });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Managed Project",
            Code = "MNG",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = managerId,
            Role = roleKey
        });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Private managed task",
            IsPrivate = true
        });
        await _context.SaveChangesAsync();

        var result = await new NotificationTargetResolver(_context).ResolveAsync(new Notification
        {
            UserId = managerId,
            Message = "Private task changed",
            Type = "TaskStatusChanged",
            RelatedEntityId = taskId,
            RelatedEntityType = nameof(TaskItem)
        }, managerId);

        result.IsVisible.Should().BeTrue();
        result.TargetUrl.Should().Be($"/projects/{projectId}/tasks/{taskId}");
    }

    [Fact]
    public async Task NotificationTargetResolver_Meeting_UsesCanonicalMeetingRoute()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        SeedUser(ownerId, "Owner");
        SeedUser(memberId, "Member");
        _context.WorkGroups.Add(new WorkGroup { Id = groupId, Name = "Delivery", OwnerId = ownerId });
        _context.WorkGroupMembers.Add(new WorkGroupMember
        {
            WorkGroupId = groupId,
            UserId = memberId,
            Role = GroupRoleRules.Member
        });
        _context.GroupMeetingSessions.Add(new GroupMeetingSession
        {
            Id = meetingId,
            WorkGroupId = groupId,
            StartedByUserId = ownerId,
            RoomId = "delivery-room"
        });
        await _context.SaveChangesAsync();

        var notification = new Notification
        {
            UserId = memberId,
            Message = "Cuộc họp đang diễn ra",
            Type = "MeetingStarted",
            RelatedEntityId = meetingId,
            RelatedEntityType = nameof(GroupMeetingSession)
        };

        var result = await new NotificationTargetResolver(_context)
            .ResolveAsync(notification, memberId);

        result.IsVisible.Should().BeTrue();
        result.TargetUrl.Should().Be($"/groups/{groupId}/meeting?meetingId={meetingId}");
    }

    [Fact]
    public async Task NotificationTargetResolver_Project_DeniesOrdinaryOrganizationMemberButAllowsOrganizationAdmin()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        SeedUser(ownerId, "Owner");
        SeedUser(memberId, "Member");
        SeedUser(adminId, "Organization Admin");
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Portfolio",
            Code = "PORT",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.OrganizationMembers.AddRange(
            new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = memberId,
                Role = OrganizationRoleRules.Member
            },
            new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = adminId,
                Role = OrganizationRoleRules.OrganizationAdmin
            });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Restricted portfolio project",
            Code = "RPP",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        await _context.SaveChangesAsync();
        var notification = new Notification
        {
            RelatedEntityId = projectId,
            RelatedEntityType = nameof(Project),
            Message = "Project changed",
            Type = "ProjectUpdated"
        };
        var resolver = new NotificationTargetResolver(_context);

        var memberResult = await resolver.ResolveAsync(notification, memberId);
        var adminResult = await resolver.ResolveAsync(notification, adminId);

        memberResult.IsVisible.Should().BeFalse();
        adminResult.IsVisible.Should().BeTrue();
        adminResult.TargetUrl.Should().Be($"/projects/{projectId}");
    }

    [Fact]
    public async Task NotificationTargetResolver_UnknownEntityType_FailsClosed()
    {
        var notification = new Notification
        {
            RelatedEntityId = Guid.NewGuid(),
            RelatedEntityType = "UnsupportedEntity",
            Message = "Unknown target",
            Type = "Unknown"
        };

        var result = await new NotificationTargetResolver(_context)
            .ResolveAsync(notification, Guid.NewGuid());

        result.IsVisible.Should().BeFalse();
        result.TargetUrl.Should().BeNull();
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
    public async Task WikiService_PrivatePage_IsVisibleOnlyToAuthorOrManager()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        SeedUser(ownerId, "Owner");
        SeedUser(memberId, "Member");
        _context.Projects.Add(new Project { Id = projectId, Name = "Private Wiki", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            Role = ProjectRoleRules.Member
        });
        var ownerPrivateId = Guid.NewGuid();
        _context.WikiPages.AddRange(
            new WikiPage
            {
                Id = ownerPrivateId,
                ProjectId = projectId,
                AuthorId = ownerId,
                Title = "Owner private draft",
                Content = "secret",
                Visibility = "private"
            },
            new WikiPage
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AuthorId = memberId,
                Title = "Member private draft",
                Content = "mine",
                Visibility = "private"
            },
            new WikiPage
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AuthorId = ownerId,
                Title = "Internal handbook",
                Content = "shared",
                Visibility = "internal"
            });
        await _context.SaveChangesAsync();

        var service = CreateWikiService(CurrentUser(memberId));

        var list = await service.GetByProjectAsync(projectId);
        var updateOtherPrivate = await service.UpdateAsync(
            projectId,
            ownerPrivateId,
            new UpdateWikiPageDto("Changed", "leak", "internal"));

        list.IsSuccess.Should().BeTrue(list.Error);
        list.Data!.Select(page => page.Title).Should().BeEquivalentTo("Member private draft", "Internal handbook");
        updateOtherPrivate.IsSuccess.Should().BeFalse();
        updateOtherPrivate.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task WikiService_WriteBoundary_RejectsReadOnlyRolesAndInvalidVisibility()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        SeedUser(ownerId, "Owner");
        SeedUser(memberId, "Member");
        SeedUser(viewerId, "Viewer");
        SeedUser(customerId, "Customer");
        _context.Projects.Add(new Project { Id = projectId, Name = "Wiki roles", OwnerId = ownerId });
        _context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = memberId, Role = ProjectRoleRules.Member },
            new ProjectMember { ProjectId = projectId, UserId = viewerId, Role = ProjectRoleRules.Viewer },
            new ProjectMember { ProjectId = projectId, UserId = customerId, Role = ProjectRoleRules.Customer });
        await _context.SaveChangesAsync();

        var memberCreate = await CreateWikiService(CurrentUser(memberId))
            .CreateAsync(projectId, new CreateWikiPageDto("Runbook", "content", "internal"));
        var viewerCreate = await CreateWikiService(CurrentUser(viewerId))
            .CreateAsync(projectId, new CreateWikiPageDto("Viewer edit", "content", "internal"));
        var customerCreate = await CreateWikiService(CurrentUser(customerId))
            .CreateAsync(projectId, new CreateWikiPageDto("Customer edit", "content", "customer_safe"));
        var invalidVisibility = await CreateWikiService(CurrentUser(memberId))
            .CreateAsync(projectId, new CreateWikiPageDto("Unknown scope", "content", "organization-wide"));

        memberCreate.IsSuccess.Should().BeTrue(memberCreate.Error);
        viewerCreate.StatusCode.Should().Be(403);
        customerCreate.StatusCode.Should().Be(403);
        invalidVisibility.StatusCode.Should().Be(400);
        _context.WikiPages.Count(page => page.ProjectId == projectId).Should().Be(1);
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
            new ProjectMember { ProjectId = projectId, UserId = reporterId, Role = ProjectRoleRules.Member },
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
        var currentUser = CurrentUser(authorId);
        var projectRepo = new GenericRepository<Project>(_context);
        var projectMemberRepo = new GenericRepository<ProjectMember>(_context);
        var service = new CommentService(
            new GenericRepository<TaskComment>(_context),
            new GenericRepository<TaskItem>(_context),
            projectMemberRepo,
            new GenericRepository<VectorSyncOutbox>(_context),
            new UnitOfWork(_context),
            currentUser.Object,
            notifications.Object,
            Mock.Of<IAuditLogService>(),
            new TaskAccessPolicy(
                currentUser.Object,
                projectRepo,
                projectMemberRepo,
                new GenericRepository<OrganizationMember>(_context),
                new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context))));

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

    [Fact]
    public async Task CommentService_ViewerCannotCreateCommentOnReadableTask()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SeedUser(ownerId, "Comment Owner");
        SeedUser(viewerId, "Comment Viewer");
        _context.Projects.Add(new Project { Id = projectId, Name = "Read only comments", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = viewerId, Role = ProjectRoleRules.Viewer });
        _context.TaskItems.Add(new TaskItem { Id = taskId, ProjectId = projectId, ReporterId = ownerId, Title = "Readable task" });
        await _context.SaveChangesAsync();

        var result = await CreateCommentService(CurrentUser(viewerId), Mock.Of<INotificationService>())
            .CreateAsync(new CreateCommentDto("Must not persist", taskId));

        result.StatusCode.Should().Be(403);
        _context.TaskComments.Should().BeEmpty();
        _context.VectorSyncOutbox.Should().BeEmpty();
    }

    [Fact]
    public async Task CommentService_DoesNotNotifyFormerReporterWhoseProjectMembershipWasRemoved()
    {
        var ownerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var formerReporterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SeedUser(ownerId, "Owner");
        SeedUser(authorId, "Author");
        SeedUser(formerReporterId, "Former reporter");
        _context.Projects.Add(new Project { Id = projectId, Name = "Revoked notifications", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = authorId,
            Role = ProjectRoleRules.Member
        });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = formerReporterId,
            Title = "Historical reporter",
            Status = "Todo"
        });
        await _context.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        var result = await CreateCommentService(CurrentUser(authorId), notifications.Object)
            .CreateAsync(new CreateCommentDto("No stale recipient", taskId));

        result.IsSuccess.Should().BeTrue(result.Error);
        notifications.Verify(
            service => service.CreateAsync(
                formerReporterId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CommentService_PrivateTask_SecondaryAssigneeCanCommentWithoutNotifyingUnrelatedMention()
    {
        var ownerId = Guid.NewGuid();
        var secondaryAssigneeId = Guid.NewGuid();
        var unrelatedMemberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SeedUser(ownerId, "Private Owner");
        SeedUser(secondaryAssigneeId, "Secondary Assignee");
        SeedUser(unrelatedMemberId, "Unrelated Member");
        _context.Projects.Add(new Project { Id = projectId, Name = "Private comments", Code = "PRIVATE-COMMENT", OwnerId = ownerId });
        _context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = secondaryAssigneeId, Role = ProjectRoleRules.Member },
            new ProjectMember { ProjectId = projectId, UserId = unrelatedMemberId, Role = ProjectRoleRules.Member });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Restricted review",
            Status = "Todo",
            IsPrivate = true
        });
        _context.TaskAssignments.Add(new TaskAssignment
        {
            TaskItemId = taskId,
            UserId = secondaryAssigneeId,
            AssignedByUserId = ownerId
        });
        await _context.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        var currentUser = CurrentUser(secondaryAssigneeId);
        var service = CreateCommentService(currentUser, notifications.Object);

        var result = await service.CreateAsync(new CreateCommentDto(
            "Đã kiểm tra phần riêng tư",
            taskId,
            MentionedUserIds: [unrelatedMemberId]));

        result.IsSuccess.Should().BeTrue(result.Error);
        notifications.Verify(
            item => item.CreateAsync(
                unrelatedMemberId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CommentService_RemovedAuthorCannotDeleteHistoricalComment()
    {
        var ownerId = Guid.NewGuid();
        var formerMemberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        SeedUser(ownerId, "Comment Owner");
        SeedUser(formerMemberId, "Former Commenter");
        _context.Projects.Add(new Project { Id = projectId, Name = "Removed membership", Code = "REMOVED-COMMENT", OwnerId = ownerId });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Historical task",
            Status = "Todo"
        });
        _context.TaskComments.Add(new TaskComment
        {
            Id = commentId,
            TaskItemId = taskId,
            AuthorId = formerMemberId,
            Content = "Historical comment"
        });
        await _context.SaveChangesAsync();

        var currentUser = CurrentUser(formerMemberId);
        var result = await CreateCommentService(currentUser, Mock.Of<INotificationService>()).DeleteAsync(commentId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await _context.TaskComments.IgnoreQueryFilters().SingleAsync(item => item.Id == commentId)).IsDeleted.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private NotificationService CreateNotificationService(
        INotificationPublisher? publisher = null,
        Qaly.Application.Common.Interfaces.IPushSender? pushSender = null)
        => new(
            new GenericRepository<Notification>(_context),
            new GenericRepository<PushSubscription>(_context),
            new UnitOfWork(_context),
            publisher ?? Mock.Of<INotificationPublisher>(),
            pushSender ?? Mock.Of<Qaly.Application.Common.Interfaces.IPushSender>(),
            NullLogger<NotificationService>.Instance,
            new NotificationTargetResolver(_context));

    private WikiService CreateWikiService(Mock<ICurrentUserService> currentUser)
    {
        var projectRepo = new GenericRepository<Project>(_context);
        var memberRepo = new GenericRepository<ProjectMember>(_context);
        var accessPolicy = new TaskAccessPolicy(
            currentUser.Object,
            projectRepo,
            memberRepo,
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

        return new WikiService(
            new GenericRepository<WikiPage>(_context),
            projectRepo,
            new GenericRepository<User>(_context),
            accessPolicy,
            new UnitOfWork(_context),
            Mock.Of<IAuditLogService>());
    }

    private CommentService CreateCommentService(
        Mock<ICurrentUserService> currentUser,
        INotificationService notificationService)
    {
        var projectRepo = new GenericRepository<Project>(_context);
        var projectMemberRepo = new GenericRepository<ProjectMember>(_context);
        return new CommentService(
            new GenericRepository<TaskComment>(_context),
            new GenericRepository<TaskItem>(_context),
            projectMemberRepo,
            new GenericRepository<VectorSyncOutbox>(_context),
            new UnitOfWork(_context),
            currentUser.Object,
            notificationService,
            Mock.Of<IAuditLogService>(),
            new TaskAccessPolicy(
                currentUser.Object,
                projectRepo,
                projectMemberRepo,
                new GenericRepository<OrganizationMember>(_context),
                new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context))));
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
