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
    private readonly GenericRepository<GroupPoll> _pollRepo;
    private readonly GenericRepository<GroupPollOption> _pollOptionRepo;
    private readonly GenericRepository<GroupPollVote> _pollVoteRepo;
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
    private readonly Mock<IGroupPollRealtimePublisher> _groupPollRealtimePublisher = new();
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
        _pollRepo = new GenericRepository<GroupPoll>(_context);
        _pollOptionRepo = new GenericRepository<GroupPollOption>(_context);
        _pollVoteRepo = new GenericRepository<GroupPollVote>(_context);
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

        _groupPollRealtimePublisher
            .Setup(publisher => publisher.PublishPollUpdatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<GroupPollResultsDto>(),
                It.IsAny<DateTimeOffset>(),
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
    public async Task CreatePollAsync_WhenOwnerCreatesPoll_ReturnsCreatedAndPersistsPoll()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);
        var request = CreateValidPollRequest(allowMultiple: true, expiredAt: DateTimeOffset.UtcNow.AddHours(2));

        var result = await CreateService().CreatePollAsync(group.Id, request);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(201);
        result.Data.Should().NotBeNull();
        result.Data!.GroupId.Should().Be(group.Id);
        result.Data.Question.Should().Be("Favorite framework?");
        result.Data.Status.Should().Be(GroupPollStatus.Open.ToString());
        result.Data.CreatedByUserId.Should().Be(ownerId);
        result.Data.AllowMultiple.Should().BeTrue();
        result.Data.Options.Should().HaveCount(3);
        result.Data.Options.Select(option => option.SortOrder).Should().ContainInOrder(1, 2, 3);
        result.Data.Options.Select(option => option.Content).Should().ContainInOrder("ASP.NET Core", "Spring Boot", "NestJS");

        var poll = await _context.GroupPolls.Include(item => item.Options).SingleAsync();
        poll.Status.Should().Be(GroupPollStatus.Open);
        poll.CreatedByUserId.Should().Be(ownerId);
        poll.AllowMultiple.Should().BeTrue();
        poll.Options.Should().HaveCount(3);
        poll.Options.OrderBy(option => option.SortOrder).Select(option => option.Content).Should().ContainInOrder("ASP.NET Core", "Spring Boot", "NestJS");
    }

    [Fact]
    public async Task CreatePollAsync_WhenAdminCreatesPoll_ReturnsCreated()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var result = await CreateService().CreatePollAsync(group.Id, CreateValidPollRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreatePollAsync_WhenMemberCreatesPoll_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().CreatePollAsync(group.Id, CreateValidPollRequest());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreatePollAsync_WhenUserNotInGroup_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(outsiderId, "Outsider", "outsider@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(outsiderId);

        var result = await CreateService().CreatePollAsync(group.Id, CreateValidPollRequest());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreatePollAsync_WhenGroupNotFound_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreatePollAsync(Guid.NewGuid(), CreateValidPollRequest());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreatePollAsync_WhenQuestionEmpty_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreatePollAsync(group.Id, CreateValidPollRequest(question: "  "));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreatePollAsync_WhenOptionCountLessThanTwo_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var request = new CreateGroupPollRequest(
            "Question",
            new List<CreateGroupPollOptionRequest> { new("Only one option") },
            false,
            null);

        var result = await CreateService().CreatePollAsync(group.Id, request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreatePollAsync_WhenOptionIsEmpty_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var request = new CreateGroupPollRequest(
            "Question",
            new List<CreateGroupPollOptionRequest>
            {
                new("Valid"),
                new("   ")
            },
            false,
            null);

        var result = await CreateService().CreatePollAsync(group.Id, request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreatePollAsync_WhenOptionsDuplicatedAfterNormalize_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var request = new CreateGroupPollRequest(
            "Question",
            new List<CreateGroupPollOptionRequest>
            {
                new("Option A"),
                new(" option a ")
            },
            false,
            null);

        var result = await CreateService().CreatePollAsync(group.Id, request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreatePollAsync_WhenExpiredAtInPast_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreatePollAsync(
            group.Id,
            CreateValidPollRequest(expiredAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreatePollAsync_WhenAllowMultipleFalse_PersistsFalseValue()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Poll Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().CreatePollAsync(group.Id, CreateValidPollRequest(allowMultiple: false));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.AllowMultiple.Should().BeFalse();

        var poll = await _context.GroupPolls.SingleAsync();
        poll.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public async Task VotePollAsync_WhenMemberVotesSingleChoice_Succeeds()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id }));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.TotalVotes.Should().Be(1);
        result.Data.TotalVoters.Should().Be(1);
        result.Data.CurrentUserOptionIds.Should().ContainSingle().Which.Should().Be(options[0].Id);

        var savedVotes = await _context.GroupPollVotes.Where(vote => vote.UserId == memberId).ToListAsync();
        savedVotes.Should().HaveCount(1);
        savedVotes[0].OptionId.Should().Be(options[0].Id);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            group.Id,
            poll.Id,
            It.Is<GroupPollResultsDto>(dto =>
                dto.PollId == poll.Id &&
                dto.GroupId == group.Id &&
                dto.TotalVotes == 1 &&
                dto.CurrentUserOptionIds.Contains(options[0].Id)),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VotePollAsync_WhenRealtimePublishFails_StillReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);
        _groupPollRealtimePublisher
            .Setup(publisher => publisher.PublishPollUpdatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<GroupPollResultsDto>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("signalr down"));

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id }));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task VotePollAsync_WhenSingleChoiceRevote_ReplacesOldVote()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id }));
        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[1].Id }));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.CurrentUserOptionIds.Should().ContainSingle().Which.Should().Be(options[1].Id);

        var savedVotes = await _context.GroupPollVotes.Where(vote => vote.PollId == poll.Id && vote.UserId == memberId).ToListAsync();
        savedVotes.Should().HaveCount(1);
        savedVotes[0].OptionId.Should().Be(options[1].Id);
    }

    [Fact]
    public async Task VotePollAsync_WhenSingleChoiceSelectsMultiple_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id, options[1].Id }));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task VotePollAsync_WhenMultipleChoiceSelectsMany_Succeeds()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id, options[1].Id }));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.CurrentUserOptionIds.Should().BeEquivalentTo(new[] { options[0].Id, options[1].Id });

        var savedVotes = await _context.GroupPollVotes.Where(vote => vote.PollId == poll.Id && vote.UserId == memberId).ToListAsync();
        savedVotes.Should().HaveCount(2);
    }

    [Fact]
    public async Task VotePollAsync_WhenMultipleChoiceRevote_ReplacesAllSelections()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id, options[1].Id }));
        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[1].Id, options[2].Id }));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.CurrentUserOptionIds.Should().BeEquivalentTo(new[] { options[1].Id, options[2].Id });

        var savedVotes = await _context.GroupPollVotes
            .Where(vote => vote.PollId == poll.Id && vote.UserId == memberId)
            .Select(vote => vote.OptionId)
            .ToListAsync();
        savedVotes.Should().BeEquivalentTo(new[] { options[1].Id, options[2].Id });
    }

    [Fact]
    public async Task VotePollAsync_WhenOptionDoesNotBelongToPoll_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        var (_, otherPollOptions) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { otherPollOptions[0].Id }));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<GroupPollResultsDto>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VotePollAsync_WhenOptionIdsEmpty_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid>()));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<GroupPollResultsDto>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VotePollAsync_WhenOptionIdsDuplicated_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id, options[0].Id }));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task VotePollAsync_WhenUserIsNotGroupMember_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(outsiderId, "Outsider", "outsider@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(outsiderId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id }));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<GroupPollResultsDto>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VotePollAsync_WhenPollClosed_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false, status: GroupPollStatus.Closed);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id }));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<GroupPollResultsDto>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VotePollAsync_WhenPollExpired_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Vote Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false, expiredAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().VotePollAsync(group.Id, poll.Id, new VoteGroupPollRequest(new List<Guid> { options[0].Id }));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<GroupPollResultsDto>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClosePollAsync_WhenOwnerClosesPoll_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Close Poll Group");
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().ClosePollAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(GroupPollStatus.Closed.ToString());

        var savedPoll = await _context.GroupPolls.SingleAsync(item => item.Id == poll.Id);
        savedPoll.Status.Should().Be(GroupPollStatus.Closed);

        _groupPollRealtimePublisher.Verify(publisher => publisher.PublishPollUpdatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<GroupPollResultsDto>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClosePollAsync_WhenAdminClosesPoll_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Close Poll Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var result = await CreateService().ClosePollAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(GroupPollStatus.Closed.ToString());
    }

    [Fact]
    public async Task ClosePollAsync_WhenCreatorIsMember_CanClosePoll()
    {
        var ownerId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(creatorId, "Creator", "creator@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Close Poll Group");
        await AddMemberAsync(group.Id, creatorId, GroupRoleRules.Member);
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, creatorId, allowMultiple: true);
        _currentUser.SetupGet(user => user.UserId).Returns(creatorId);

        var result = await CreateService().ClosePollAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(GroupPollStatus.Closed.ToString());
    }

    [Fact]
    public async Task ClosePollAsync_WhenMemberIsNotCreator_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Close Poll Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().ClosePollAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ClosePollAsync_WhenPollAlreadyClosed_ReturnsSuccessIdempotent()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Close Poll Group");
        var (poll, _) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: false, status: GroupPollStatus.Closed);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().ClosePollAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(GroupPollStatus.Closed.ToString());
    }

    [Fact]
    public async Task GetPollResultsAsync_ReturnsVoteCountPerOption()
    {
        var ownerId = Guid.NewGuid();
        var voter1Id = Guid.NewGuid();
        var voter2Id = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(voter1Id, "Voter1", "voter1@qaly.dev");
        await AddUserAsync(voter2Id, "Voter2", "voter2@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Results Group");
        await AddMemberAsync(group.Id, voter1Id, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, voter2Id, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        await AddPollVoteAsync(poll.Id, options[0].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter2Id);
        _currentUser.SetupGet(user => user.UserId).Returns(voter1Id);

        var result = await CreateService().GetPollResultsAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Options.Single(option => option.OptionId == options[0].Id).VoteCount.Should().Be(1);
        result.Data.Options.Single(option => option.OptionId == options[1].Id).VoteCount.Should().Be(2);
        result.Data.Options.Single(option => option.OptionId == options[2].Id).VoteCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPollResultsAsync_ReturnsTotalVotes()
    {
        var ownerId = Guid.NewGuid();
        var voter1Id = Guid.NewGuid();
        var voter2Id = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(voter1Id, "Voter1", "voter1@qaly.dev");
        await AddUserAsync(voter2Id, "Voter2", "voter2@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Results Group");
        await AddMemberAsync(group.Id, voter1Id, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, voter2Id, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        await AddPollVoteAsync(poll.Id, options[0].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter2Id);
        _currentUser.SetupGet(user => user.UserId).Returns(voter1Id);

        var result = await CreateService().GetPollResultsAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.TotalVotes.Should().Be(3);
    }

    [Fact]
    public async Task GetPollResultsAsync_ReturnsTotalVoters()
    {
        var ownerId = Guid.NewGuid();
        var voter1Id = Guid.NewGuid();
        var voter2Id = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(voter1Id, "Voter1", "voter1@qaly.dev");
        await AddUserAsync(voter2Id, "Voter2", "voter2@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Results Group");
        await AddMemberAsync(group.Id, voter1Id, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, voter2Id, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        await AddPollVoteAsync(poll.Id, options[0].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter2Id);
        _currentUser.SetupGet(user => user.UserId).Returns(voter1Id);

        var result = await CreateService().GetPollResultsAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.TotalVoters.Should().Be(2);
    }

    [Fact]
    public async Task GetPollResultsAsync_ReturnsCurrentUserOptionIds()
    {
        var ownerId = Guid.NewGuid();
        var voter1Id = Guid.NewGuid();
        var voter2Id = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(voter1Id, "Voter1", "voter1@qaly.dev");
        await AddUserAsync(voter2Id, "Voter2", "voter2@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Results Group");
        await AddMemberAsync(group.Id, voter1Id, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, voter2Id, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);
        await AddPollVoteAsync(poll.Id, options[0].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[1].Id, voter1Id);
        await AddPollVoteAsync(poll.Id, options[2].Id, voter2Id);
        _currentUser.SetupGet(user => user.UserId).Returns(voter1Id);

        var result = await CreateService().GetPollResultsAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.CurrentUserOptionIds.Should().BeEquivalentTo(new[] { options[0].Id, options[1].Id });
    }

    [Fact]
    public async Task GetPollResultsAsync_SortsOptionsBySortOrder()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Results Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        var (poll, options) = await AddPollWithOptionsAsync(group.Id, ownerId, allowMultiple: true);

        options[0].SortOrder = 3;
        options[1].SortOrder = 1;
        options[2].SortOrder = 2;
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(user => user.UserId).Returns(memberId);
        var result = await CreateService().GetPollResultsAsync(group.Id, poll.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Options.Select(option => option.SortOrder).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WithValidPendingInvitation_AddsMemberAndMarksAccepted()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Accept Group");
        const string token = "accept-token-valid";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddDays(2), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().AcceptInvitationAsync(token);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(GroupInvitationStatus.Accepted.ToString());

        var member = await _context.WorkGroupMembers.SingleOrDefaultAsync(item => item.WorkGroupId == group.Id && item.UserId == invitedUserId);
        member.Should().NotBeNull();
        member!.Role.Should().Be(GroupRoleRules.Member);

        var invitation = await _context.GroupInvitations.SingleAsync(item => item.Token == token);
        invitation.Status.Should().Be(GroupInvitationStatus.Accepted);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenTokenNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        await AddUserAsync(userId, "User", "user@qaly.dev");
        _currentUser.SetupGet(user => user.UserId).Returns(userId);

        var result = await CreateService().AcceptInvitationAsync("missing-token");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenTokenIsEmpty_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        await AddUserAsync(userId, "User", "user@qaly.dev");
        _currentUser.SetupGet(user => user.UserId).Returns(userId);

        var result = await CreateService().AcceptInvitationAsync("   ");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        var result = await CreateService().AcceptInvitationAsync("token");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenInvitationExpired_DoesNotAddMemberAndMarksExpired()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Accept Group");
        const string token = "accept-token-expired";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-1), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().AcceptInvitationAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);

        var memberCount = await _context.WorkGroupMembers.CountAsync(item => item.WorkGroupId == group.Id && item.UserId == invitedUserId);
        memberCount.Should().Be(0);

        var invitation = await _context.GroupInvitations.SingleAsync(item => item.Token == token);
        invitation.Status.Should().Be(GroupInvitationStatus.Expired);
    }

    [Theory]
    [InlineData(GroupInvitationStatus.Accepted)]
    [InlineData(GroupInvitationStatus.Rejected)]
    [InlineData(GroupInvitationStatus.Revoked)]
    public async Task AcceptInvitationAsync_WhenInvitationAlreadyProcessed_ReturnsConflict(GroupInvitationStatus status)
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Accept Group");
        const string token = "accept-token-processed";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", status, DateTimeOffset.UtcNow.AddDays(3), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().AcceptInvitationAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenCurrentUserEmailMismatch_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "another@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Accept Group");
        const string token = "accept-token-email-mismatch";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddDays(3), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().AcceptInvitationAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenUserAlreadyMember_DoesNotCreateDuplicateMember()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Accept Group");
        await AddMemberAsync(group.Id, invitedUserId, GroupRoleRules.Member);
        const string token = "accept-token-existing-member";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddDays(2), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().AcceptInvitationAsync(token);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);

        var memberCount = await _context.WorkGroupMembers.CountAsync(item => item.WorkGroupId == group.Id && item.UserId == invitedUserId);
        memberCount.Should().Be(1);

        var invitation = await _context.GroupInvitations.SingleAsync(item => item.Token == token);
        invitation.Status.Should().Be(GroupInvitationStatus.Accepted);
    }

    [Fact]
    public async Task RejectInvitationAsync_WithValidPendingInvitation_MarksRejectedAndDoesNotAddMember()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Reject Group");
        const string token = "reject-token-valid";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddDays(1), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().RejectInvitationAsync(token);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(GroupInvitationStatus.Rejected.ToString());

        var memberCount = await _context.WorkGroupMembers.CountAsync(item => item.WorkGroupId == group.Id && item.UserId == invitedUserId);
        memberCount.Should().Be(0);
    }

    [Fact]
    public async Task RejectInvitationAsync_WhenTokenNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        await AddUserAsync(userId, "User", "user@qaly.dev");
        _currentUser.SetupGet(user => user.UserId).Returns(userId);

        var result = await CreateService().RejectInvitationAsync("missing-token");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task RejectInvitationAsync_WhenTokenIsEmpty_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        await AddUserAsync(userId, "User", "user@qaly.dev");
        _currentUser.SetupGet(user => user.UserId).Returns(userId);

        var result = await CreateService().RejectInvitationAsync(" ");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RejectInvitationAsync_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        var result = await CreateService().RejectInvitationAsync("token");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData(GroupInvitationStatus.Pending, true)]
    [InlineData(GroupInvitationStatus.Accepted, false)]
    [InlineData(GroupInvitationStatus.Rejected, false)]
    [InlineData(GroupInvitationStatus.Revoked, false)]
    [InlineData(GroupInvitationStatus.Expired, false)]
    public async Task RejectInvitationAsync_WhenInvitationCannotBeActioned_ReturnsConflict(GroupInvitationStatus status, bool shouldMarkExpired)
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "invited@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Reject Group");
        const string token = "reject-token-conflict";
        var expiredAt = status == GroupInvitationStatus.Pending
            ? DateTimeOffset.UtcNow.AddMinutes(-5)
            : DateTimeOffset.UtcNow.AddDays(2);
        await AddInvitationAsync(group.Id, "invited@qaly.dev", status, expiredAt, token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().RejectInvitationAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);

        var invitation = await _context.GroupInvitations.SingleAsync(item => item.Token == token);
        if (shouldMarkExpired)
        {
            invitation.Status.Should().Be(GroupInvitationStatus.Expired);
        }
        else
        {
            invitation.Status.Should().Be(status);
        }
    }

    [Fact]
    public async Task RejectInvitationAsync_WhenCurrentUserEmailMismatch_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(invitedUserId, "Invited", "another@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Reject Group");
        const string token = "reject-token-email-mismatch";
        await AddInvitationAsync(group.Id, "invited@qaly.dev", GroupInvitationStatus.Pending, DateTimeOffset.UtcNow.AddDays(2), token);
        _currentUser.SetupGet(user => user.UserId).Returns(invitedUserId);

        var result = await CreateService().RejectInvitationAsync(token);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenOwnerUpdatesMemberToAdmin_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, memberId, new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);

        var updated = await _context.WorkGroupMembers.SingleAsync(item => item.WorkGroupId == group.Id && item.UserId == memberId);
        updated.Role.Should().Be(GroupRoleRules.Admin);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        var result = await CreateService().UpdateMemberRoleAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenAdminUpdatesMemberToAdmin_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, memberId, new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenRegularMemberUpdatesOthers_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        await AddUserAsync(targetId, "Target", "target@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, targetId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, targetId, new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenCurrentUserNotInGroup_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(outsiderId, "Outsider", "outsider@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(outsiderId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, memberId, new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenTargetNotInGroup_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(targetId, "Target", "target@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, targetId, new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenDowngradeLastOwner_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, ownerId, new UpdateGroupMemberRoleRequest("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenRoleInvalid_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, memberId, new UpdateGroupMemberRoleRequest("InvalidRole"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenAdminTriesToUpdateOwner_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var result = await CreateService().UpdateMemberRoleAsync(group.Id, ownerId, new UpdateGroupMemberRoleRequest("Member"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenOwnerRemovesMember_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().RemoveMemberAsync(group.Id, memberId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);

        var memberExists = await _context.WorkGroupMembers.AnyAsync(item => item.WorkGroupId == group.Id && item.UserId == memberId);
        memberExists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        var result = await CreateService().RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenAdminRemovesMember_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var result = await CreateService().RemoveMemberAsync(group.Id, memberId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRegularMemberRemovesOthers_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        await AddUserAsync(targetId, "Target", "target@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, targetId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().RemoveMemberAsync(group.Id, targetId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenCurrentUserNotInGroup_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(outsiderId, "Outsider", "outsider@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(outsiderId);

        var result = await CreateService().RemoveMemberAsync(group.Id, memberId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenTargetNotInGroup_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(targetId, "Target", "target@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().RemoveMemberAsync(group.Id, targetId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRemovingLastOwner_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        _currentUser.SetupGet(user => user.UserId).Returns(ownerId);

        var result = await CreateService().RemoveMemberAsync(group.Id, ownerId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenAdminTriesToRemoveOwner_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(adminId, "Admin", "admin@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, adminId, GroupRoleRules.Admin);
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var result = await CreateService().RemoveMemberAsync(group.Id, ownerId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRemovingSelfMember_DoesNotAffectOtherMembers()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var anotherMemberId = Guid.NewGuid();
        await AddUserAsync(ownerId, "Owner", "owner@qaly.dev");
        await AddUserAsync(memberId, "Member", "member@qaly.dev");
        await AddUserAsync(anotherMemberId, "Another", "another@qaly.dev");
        var group = await AddGroupAsync(ownerId, "Manage Group");
        await AddMemberAsync(group.Id, memberId, GroupRoleRules.Member);
        await AddMemberAsync(group.Id, anotherMemberId, GroupRoleRules.Member);
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().RemoveMemberAsync(group.Id, memberId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.StatusCode.Should().Be(200);

        var removedExists = await _context.WorkGroupMembers.AnyAsync(item => item.WorkGroupId == group.Id && item.UserId == memberId);
        var anotherExists = await _context.WorkGroupMembers.AnyAsync(item => item.WorkGroupId == group.Id && item.UserId == anotherMemberId);
        removedExists.Should().BeFalse();
        anotherExists.Should().BeTrue();
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
            _pollRepo,
            _pollOptionRepo,
            _pollVoteRepo,
            _messageRepo,
            _organizationRepo,
            _organizationMemberRepo,
            _userRepo,
            _projectService.Object,
            _notificationService.Object,
            _auditLogService.Object,
            _emailService.Object,
            _groupInvitationEmailBuilder.Object,
            _groupPollRealtimePublisher.Object,
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

    private async Task<GroupInvitation> AddInvitationAsync(Guid groupId, string email, GroupInvitationStatus status, DateTimeOffset expiredAt, string? token = null)
    {
        var invitation = new GroupInvitation
        {
            GroupId = groupId,
            Email = email,
            Status = status,
            ExpiredAt = expiredAt,
            Token = token ?? Guid.NewGuid().ToString("N")
        };

        await _invitationRepo.AddAsync(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    private async Task<(GroupPoll Poll, List<GroupPollOption> Options)> AddPollWithOptionsAsync(
        Guid groupId,
        Guid createdByUserId,
        bool allowMultiple,
        GroupPollStatus status = GroupPollStatus.Open,
        DateTimeOffset? expiredAt = null,
        params string[] optionContents)
    {
        var poll = new GroupPoll
        {
            GroupId = groupId,
            CreatedByUserId = createdByUserId,
            Question = "Favorite stack?",
            AllowMultiple = allowMultiple,
            Status = status,
            ExpiredAt = expiredAt
        };

        await _pollRepo.AddAsync(poll);

        if (optionContents == null || optionContents.Length == 0)
        {
            optionContents = new[] { "Option A", "Option B", "Option C" };
        }

        var options = optionContents
            .Select((content, index) => new GroupPollOption
            {
                PollId = poll.Id,
                Content = content,
                SortOrder = index + 1
            })
            .ToList();

        await _pollOptionRepo.AddRangeAsync(options);
        await _context.SaveChangesAsync();

        return (poll, options);
    }

    private async Task AddPollVoteAsync(Guid pollId, Guid optionId, Guid userId)
    {
        await _pollVoteRepo.AddAsync(new GroupPollVote
        {
            PollId = pollId,
            OptionId = optionId,
            UserId = userId
        });

        await _context.SaveChangesAsync();
    }

    private static CreateGroupPollRequest CreateValidPollRequest(
        string question = "Favorite framework?",
        bool allowMultiple = false,
        DateTimeOffset? expiredAt = null)
        => new(
            question,
            new List<CreateGroupPollOptionRequest>
            {
                new("ASP.NET Core"),
                new("Spring Boot"),
                new("NestJS")
            },
            allowMultiple,
            expiredAt);

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
