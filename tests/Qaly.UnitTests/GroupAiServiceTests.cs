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

    private GroupAiService CreateService()
        => new(
            _chatClient.Object,
            _groupsService.Object,
            _messageRepo,
            _meetingSessionRepo,
            NullLogger<GroupAiService>.Instance);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
#pragma warning restore CA1707
