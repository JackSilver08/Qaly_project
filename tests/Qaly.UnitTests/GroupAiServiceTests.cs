using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class GroupAiServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly GenericRepository<GroupMessage> _messageRepo;
    private readonly GenericRepository<GroupMeetingSession> _meetingSessionRepo;
    private readonly Mock<IGroupsService> _groupsService = new();
    private readonly Mock<IChatClient> _chatClient = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();

    public GroupAiServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new QalyDbContext(options);
        _messageRepo = new GenericRepository<GroupMessage>(_context);
        _meetingSessionRepo = new GenericRepository<GroupMeetingSession>(_context);
    }

    [Fact]
    public async Task ExtractActionItemsAsync_WhenUserCannotAccessGroup_ReturnsForbidden()
    {
        var groupId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().ExtractActionItemsAsync(groupId, new GroupAiActionItemsRequest());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ExtractActionItemsAsync_WhenContextIsEmpty_ReturnsWarningWithoutCallingAi()
    {
        var groupId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().ExtractActionItemsAsync(
            groupId,
            new GroupAiActionItemsRequest(Source: "chat"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Items.Should().BeEmpty();
        result.Data.Warnings.Should().ContainSingle().Which.Should().Contain("No group chat");
        _chatClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LinkMeetingSummaryAndTranscriptAsync_WhenUserCannotAccessGroup_ReturnsForbidden()
    {
        var groupId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().LinkMeetingSummaryAndTranscriptAsync(groupId, meetingId, "summary", "source");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task LinkMeetingSummaryAndTranscriptAsync_WhenMeetingNotFound_ReturnsNotFound()
    {
        var groupId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().LinkMeetingSummaryAndTranscriptAsync(groupId, meetingId, "summary", "source");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task LinkMeetingSummaryAndTranscriptAsync_WhenSuccessful_UpdatesMeetingSession()
    {
        var groupId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var meeting = new GroupMeetingSession
        {
            Id = meetingId,
            WorkGroupId = groupId,
            StartedByUserId = Guid.NewGuid(),
            Provider = "Jitsi",
            RoomId = "room-123"
        };
        await _context.GroupMeetingSessions.AddAsync(meeting);
        await _context.SaveChangesAsync();

        var result = await CreateService().LinkMeetingSummaryAndTranscriptAsync(groupId, meetingId, "New Summary", "new-source-id");

        result.IsSuccess.Should().BeTrue();
        result.Data!.Summary.Should().Be("New Summary");
        result.Data.TranscriptSourceId.Should().Be("new-source-id");
        
        var updated = await _context.GroupMeetingSessions.FindAsync(meetingId);
        updated!.Summary.Should().Be("New Summary");
        updated.TranscriptSourceId.Should().Be("new-source-id");
    }

    [Fact]
    public async Task BuildGroupChatContextAsync_WhenNoMessages_ReturnsEmptyString()
    {
        var groupId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().BuildGroupChatContextAsync(groupId);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildGroupChatContextAsync_WhenMessagesExist_ReturnsFormattedContext()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = new User { Id = userId, Email = "test@example.com", FullName = "Alice Tester" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var message = new GroupMessage
        {
            WorkGroupId = groupId,
            UserId = userId,
            Content = "Hello from group!",
            MessageType = "Text",
            User = user
        };
        await _context.GroupMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        var result = await CreateService().BuildGroupChatContextAsync(groupId);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("Alice Tester: Hello from group!");
    }

    [Fact]
    public async Task SummarizeGroupDiscussionAsync_WhenSuccessful_ReturnsSummary()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = new User { Id = userId, Email = "test@example.com", FullName = "Alice Tester" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var message = new GroupMessage
        {
            WorkGroupId = groupId,
            UserId = userId,
            Content = "Discussion text here",
            MessageType = "Text",
            User = user
        };
        await _context.GroupMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        var jsonResponse = "{\"summary\": \"Alice and team discussed scope\", \"keyDecisions\": [\"Scope locked\"], \"unresolvedQuestions\": [\"Who is PM?\"], \"messageSources\": [\"Alice: Let's lock the scope\"]}";
        _chatClient
            .Setup(client => client.CompleteAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletion(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        var result = await CreateService().SummarizeGroupDiscussionAsync(groupId, new GroupAiSummaryRequest());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Summary.Should().Be("Alice and team discussed scope");
        result.Data.KeyDecisions.Should().ContainSingle().Which.Should().Be("Scope locked");
        result.Data.UnresolvedQuestions.Should().ContainSingle().Which.Should().Be("Who is PM?");
        result.Data.MessageSources.Should().ContainSingle().Which.Should().Be("Alice: Let's lock the scope");
    }

    [Fact]
    public async Task GenerateDraftProjectPayloadAsync_WhenSuccessful_ReturnsDraftPayload()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _groupsService
            .Setup(service => service.CanAccessGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = new User { Id = userId, Email = "test@example.com", FullName = "Alice Tester" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var message = new GroupMessage
        {
            WorkGroupId = groupId,
            UserId = userId,
            Content = "Discussion text here",
            MessageType = "Text",
            User = user
        };
        await _context.GroupMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        var jsonResponse = "{\"draftProjectName\": \"New Project\", \"draftProjectDescription\": \"A draft description\", \"draftTasks\": [{\"title\": \"Draft Task 1\", \"description\": \"Desc\", \"priority\": \"P0\", \"estimateDays\": 4, \"suggestedOwnerName\": \"Bob\"}]}";
        _chatClient
            .Setup(client => client.CompleteAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletion(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        var result = await CreateService().GenerateDraftProjectPayloadAsync(groupId, new GroupAiDraftProjectRequest());

        result.IsSuccess.Should().BeTrue();
        result.Data!.DraftProjectName.Should().Be("New Project");
        result.Data.DraftProjectDescription.Should().Be("A draft description");
        result.Data.DraftTasks.Should().ContainSingle();
        result.Data.DraftTasks[0].Title.Should().Be("Draft Task 1");
        result.Data.DraftTasks[0].Priority.Should().Be("P0");
        result.Data.DraftTasks[0].EstimateDays.Should().Be(4);
        result.Data.DraftTasks[0].SuggestedOwnerName.Should().Be("Bob");
    }

    private GroupAiService CreateService()
        => new(
            _chatClient.Object,
            _groupsService.Object,
            _messageRepo,
            _meetingSessionRepo,
            _unitOfWork.Object,
            _auditLogService.Object,
            NullLogger<GroupAiService>.Instance);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
#pragma warning restore CA1707
