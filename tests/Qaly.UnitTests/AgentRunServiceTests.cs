using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Xunit;

namespace Qaly.UnitTests;

public sealed class AgentRunServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<IAiWorkflowService> _workflow = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AgentRunService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public AgentRunServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _currentUser.SetupGet(user => user.UserId).Returns(_userId);
        _service = new AgentRunService(
            new GenericRepository<AiJobItem>(_db),
            _workflow.Object,
            _currentUser.Object,
            new UnitOfWork(_db));
    }

    [Fact]
    public async Task StartAsync_PersistsCheckpointAndWaitsForApproval()
    {
        var projectId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        _workflow
            .Setup(workflow => workflow.CreateJobAsync(
                It.Is<CreateAiJobDto>(dto => dto.ProjectId == projectId && dto.SourceText == "Lập kế hoạch release"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Created(new AiJobCreatedDto(Guid.NewGuid(), "DraftReady", 0.01m, "key", draftId)));

        var result = await _service.StartAsync(new StartAgentRunDto(projectId, "Lập kế hoạch release"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Status.Should().Be("awaiting_approval");
        result.Data.RequiresApproval.Should().BeTrue();
        result.Data.DraftId.Should().Be(draftId);
        result.Data.Events.Should().Contain(item => item.Type == "approval.required");
        (await _db.AiJobQueue.SingleAsync()).RequestedBy.Should().Be(_userId);
    }

    [Fact]
    public async Task ApproveAsync_ExecutesDraftAndCompletesRun()
    {
        var projectId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        _workflow
            .Setup(workflow => workflow.CreateJobAsync(It.IsAny<CreateAiJobDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Created(new AiJobCreatedDto(Guid.NewGuid(), "DraftReady", 0.01m, "key", draftId)));
        _workflow
            .Setup(workflow => workflow.ConfirmDraftAsync(
                draftId,
                It.Is<ConfirmAiDraftDto>(dto => dto.ConfirmAction == "execute_action"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AiDraftConfirmResultDto(
                draftId, "Confirmed", "execute_action", 2, [Guid.NewGuid(), Guid.NewGuid()])));

        var started = await _service.StartAsync(new StartAgentRunDto(projectId, "Tạo task release"));
        var approved = await _service.ApproveAsync(started.Data!.Id, new ApproveAgentRunDto());
        var persisted = await _service.GetAsync(started.Data.Id);

        approved.IsSuccess.Should().BeTrue(approved.Error);
        persisted.Data!.Status.Should().Be("succeeded");
        persisted.Data.Progress.Should().Be(100);
        persisted.Data.RequiresApproval.Should().BeFalse();
        persisted.Data.Events.Should().Contain(item => item.Type == "verification.completed");
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
