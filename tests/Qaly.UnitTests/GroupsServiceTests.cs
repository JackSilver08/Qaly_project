using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Application.Services.Groups;
using Qaly.Domain.Entities;
using Qaly.Domain.Enums;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class GroupsServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly GenericRepository<WorkGroup> _groupRepo;
    private readonly GenericRepository<WorkGroupMember> _memberRepo;
    private readonly GenericRepository<GroupInvitation> _invitationRepo;
    private readonly GenericRepository<GroupMessage> _messageRepo;
    private readonly GenericRepository<Organization> _organizationRepo;
    private readonly GenericRepository<OrganizationMember> _organizationMemberRepo;
    private readonly GenericRepository<User> _userRepo;
    private readonly UnitOfWork _uow;
    private readonly Mock<IProjectService> _projectService = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IGroupInvitationEmailBuilder> _groupInvitationEmailBuilder = new();
    private readonly Mock<ILogger<GroupsService>> _logger = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public GroupsServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new QalyDbContext(options);
        _groupRepo = new GenericRepository<WorkGroup>(_context);
        _memberRepo = new GenericRepository<WorkGroupMember>(_context);
        _invitationRepo = new GenericRepository<GroupInvitation>(_context);
        _messageRepo = new GenericRepository<GroupMessage>(_context);
        _organizationRepo = new GenericRepository<Organization>(_context);
        _organizationMemberRepo = new GenericRepository<OrganizationMember>(_context);
        _userRepo = new GenericRepository<User>(_context);
        _uow = new UnitOfWork(_context);

        _notificationService
            .Setup(service => service.CreateAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _groupInvitationEmailBuilder
            .Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>()))
            .Returns((string groupName, string? inviterName, string invitationToken, DateTimeOffset expiredAt) =>
            {
                var inviterLine = string.IsNullOrWhiteSpace(inviterName) ? string.Empty : $"Inviter: {inviterName}{Environment.NewLine}";
                var encodedToken = Uri.EscapeDataString(invitationToken);
                return new GroupInvitationEmailContent(
                    $"Bạn được mời tham gia nhóm {groupName}",
                    $"Group: {groupName}{Environment.NewLine}{inviterLine}Link: https://frontend.example.com/invitations/accept?token={encodedToken}{Environment.NewLine}Expires: {expiredAt:yyyy-MM-dd HH:mm} UTC");
            });
        _emailService
            .Setup(service => service.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesOwnerMembership()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var service = CreateService();

        var result = await service.CreateAsync(new CreateGroupRequest("Planning Group", "Discuss next project"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Name.Should().Be("Planning Group");
        result.Data.CurrentUserRole.Should().Be(GroupRoleRules.Owner);

        var membership = await _context.WorkGroupMembers.SingleAsync();
        membership.UserId.Should().Be(ownerId);
        membership.Role.Should().Be(GroupRoleRules.Owner);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(outsiderId, "Outsider", "outsider@qaly.dev");

        var group = await AddGroupAsync(ownerId, "Private Group");
        _currentUser.SetupGet(user => user.UserId).Returns(outsiderId);

        var result = await CreateService().GetByIdAsync(group.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenEmailHasNoAccount_CreatesInvitationWithoutNotification()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Invite Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("  NewUser@Qaly.Dev  "));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(201);
        result.Data!.Email.Should().Be("newuser@qaly.dev");
        result.Data.Status.Should().Be(GroupInvitationStatus.Pending.ToString());
        result.Data.GroupId.Should().Be(group.Id);

        var saved = await _context.GroupInvitations.SingleAsync();
        saved.Email.Should().Be("newuser@qaly.dev");
        saved.Status.Should().Be(GroupInvitationStatus.Pending);
        saved.Token.Should().NotBeNullOrWhiteSpace();

        _emailService.Verify(service => service.SendAsync(
            "newuser@qaly.dev",
            It.Is<string>(subject => subject.Contains("Invite Group", StringComparison.Ordinal)),
            It.Is<string>(body =>
                body.Contains("Invite Group", StringComparison.Ordinal) &&
                body.Contains("https://frontend.example.com/invitations/accept?token=", StringComparison.Ordinal) &&
                body.Contains(Uri.EscapeDataString(saved.Token), StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationService.Verify(service => service.CreateAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenEmailBelongsToExistingUser_CreatesNotification()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited User", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Design Guild");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("invited@qaly.dev"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(201);

        _notificationService.Verify(service => service.CreateAsync(
            invitedUserId,
            It.Is<string>(message => message.Contains("Design Guild", StringComparison.Ordinal)),
            "GroupInvitationReceived",
            "info",
            It.IsAny<Guid?>(),
            nameof(GroupInvitation),
            It.Is<string>(key => key.Contains("group:", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenUserIsRegularMember_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");

        var group = await AddGroupAsync(ownerId, "Invite Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("invitee@qaly.dev"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        _emailService.Verify(service => service.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenEmailAlreadyMember_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var existingUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(existingUserId, "Existing", "existing@qaly.dev");

        var group = await AddGroupAsync(ownerId, "Invite Group");
        await AddMemberAsync(group.Id, existingUserId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("existing@qaly.dev"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        _emailService.Verify(service => service.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenPendingInvitationExistsAndNotExpired_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Invite Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        await AddInvitationAsync(group.Id, "pending@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddDays(2));

        var result = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("pending@qaly.dev"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);

        _notificationService.Verify(service => service.CreateAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(service => service.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenPreviousInvitationExpiredOrNotPending_CreatesNewInvitation()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Invite Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        await AddInvitationAsync(group.Id, "expired@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-10));
        await AddInvitationAsync(group.Id, "accepted@qaly.dev", GroupInvitationStatus.Accepted, DateTimeOffset.UtcNow.AddDays(5));

        var expiredResult = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("expired@qaly.dev"));
        var acceptedResult = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("accepted@qaly.dev"));

        expiredResult.IsSuccess.Should().BeTrue(expiredResult.Error);
        expiredResult.StatusCode.Should().Be(201);
        acceptedResult.IsSuccess.Should().BeTrue(acceptedResult.Error);
        acceptedResult.StatusCode.Should().Be(201);

        var expiredCount = await _context.GroupInvitations.CountAsync(invitation => invitation.Email == "expired@qaly.dev");
        var acceptedCount = await _context.GroupInvitations.CountAsync(invitation => invitation.Email == "accepted@qaly.dev");

        expiredCount.Should().Be(2);
        acceptedCount.Should().Be(2);

        _emailService.Verify(service => service.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenEmailServiceFails_StillReturnsCreated()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Invite Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        _emailService
            .Setup(service => service.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var result = await CreateService().CreateInvitationAsync(group.Id, new CreateGroupInvitationRequest("resilient@qaly.dev"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(201);

        var saved = await _context.GroupInvitations.SingleAsync(invitation => invitation.Email == "resilient@qaly.dev");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateMessageAsync_WhenUserIsMember_PersistsMessage()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Chat Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreateMessageAsync(group.Id, new SendGroupMessageRequest("Hello team"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Content.Should().Be("Hello team");

        var saved = await _context.GroupMessages.SingleAsync();
        saved.WorkGroupId.Should().Be(group.Id);
        saved.UserId.Should().Be(ownerId);
    }

    [Fact]
    public async Task CreateProjectFromGroupAsync_MapsGroupRolesToProjectRoles()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Launch Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var captured = new List<(Guid UserId, string Role)>();
        var project = CreateProjectDto(projectId, "Launch Project", group.Id);
        _projectService
            .Setup(service => service.CreateAsync(
                It.Is<CreateProjectDto>(dto => dto.SourceGroupId == group.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Created(project));
        _projectService
            .Setup(service => service.AddMemberAsync(projectId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, CancellationToken>((_, userId, role, _) => captured.Add((userId, role)))
            .ReturnsAsync(Result.Success());
        _projectService
            .Setup(service => service.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(project));

        var result = await CreateService().CreateProjectFromGroupAsync(
            group.Id,
            new CreateProjectFromGroupRequest("Launch Project", "LAUNCH", "From group", null, null));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.MembersAdded.Should().Be(2);
        captured.Should().Contain((adminId, ProjectRoleRules.Manager));
        captured.Should().Contain((memberId, ProjectRoleRules.Member));
    }

    private GroupsService CreateService()
        => new(
            _groupRepo,
            _memberRepo,
            _invitationRepo,
            _messageRepo,
            _organizationRepo,
            _organizationMemberRepo,
            _userRepo,
            _projectService.Object,
            _notificationService.Object,
            _auditLogService.Object,
            _emailService.Object,
            _groupInvitationEmailBuilder.Object,
            _logger.Object,
            _uow,
            _currentUser.Object);

    private async Task AddUserAsync(Guid id, string fullName, string email)
    {
        await _userRepo.AddAsync(new User
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PasswordHash = "test",
            IsActive = true
        });
        await _context.SaveChangesAsync();
    }

    private async Task<WorkGroup> AddGroupAsync(Guid ownerId, string name)
    {
        var group = new WorkGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId
        };
        await _groupRepo.AddAsync(group);
        await _memberRepo.AddAsync(new WorkGroupMember
        {
            WorkGroupId = group.Id,
            UserId = ownerId,
            Role = GroupRoleRules.Owner
        });
        await _context.SaveChangesAsync();
        return group;
    }

    private async Task AddMemberAsync(Guid groupId, Guid userId, string role)
    {
        await _memberRepo.AddAsync(new WorkGroupMember
        {
            WorkGroupId = groupId,
            UserId = userId,
            Role = role
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddInvitationAsync(Guid groupId, string email, GroupInvitationStatus status, DateTimeOffset expiredAt)
    {
        await _invitationRepo.AddAsync(new GroupInvitation
        {
            GroupId = groupId,
            Email = email,
            Status = status,
            ExpiredAt = expiredAt,
            Token = Guid.NewGuid().ToString("N")
        });
        await _context.SaveChangesAsync();
    }

    private static ProjectDto CreateProjectDto(Guid projectId, string name, Guid sourceGroupId)
        => new(
            projectId,
            name,
            "launch",
            "From group",
            null,
            "Active",
            null,
            null,
            Guid.NewGuid(),
            "Owner",
            1,
            0,
            0,
            Array.Empty<ProjectLabelDto>(),
            DateTimeOffset.UtcNow,
            null,
            null,
            sourceGroupId);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
#pragma warning restore CA1707
