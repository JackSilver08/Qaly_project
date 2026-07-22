using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class MeetilyImportTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly Mock<ITaskService> _taskService = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly Mock<IAiComplianceService> _complianceServiceMock = new();
    private readonly Mock<IAiGateway> _aiGateway = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public MeetilyImportTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
        _currentUser.SetupGet(user => user.UserId).Returns(_userId);
        _currentUser.SetupGet(user => user.Role).Returns("Member");
        _complianceServiceMock
            .Setup(service => service.CanProcessInCloudAsync(
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _context.Users.Add(new User { Id = _userId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = _projectId, Name = "Qaly MVP", Code = "qaly-mvp", OwnerId = _userId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = _projectId, UserId = _userId, Role = "Owner" });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ImportMeetilyAsync_CreatesMeetingDraftWithSourceHash_AndSkipsDuplicate()
    {
        var service = CreateMeetingImportService();
        var request = CreateRequest();

        var first = await service.ImportMeetilyAsync(request);
        var duplicate = await service.ImportMeetilyAsync(request);

        first.IsSuccess.Should().BeTrue(first.Error);
        first.Data!.Duplicate.Should().BeFalse();
        first.Data.SourceHash.Should().HaveLength(64);
        first.Data.Extraction.ActionItems.Should().ContainSingle(item => item.Title == "Prepare demo script");
        duplicate.IsSuccess.Should().BeTrue(duplicate.Error);
        duplicate.Data!.Duplicate.Should().BeTrue();
        duplicate.Data.MeetingImportId.Should().Be(first.Data.MeetingImportId);
        (await _context.MeetingImports.CountAsync()).Should().Be(1);
        (await _context.AiGeneratedDrafts.CountAsync()).Should().Be(1);
        (await _context.TaskItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConfirmDraftAsync_ForMeetingActionItems_CreatesTasksOnlyAfterConfirm()
    {
        var importResult = await CreateMeetingImportService().ImportMeetilyAsync(CreateRequest());
        importResult.IsSuccess.Should().BeTrue(importResult.Error);
        (await _context.TaskItems.CountAsync()).Should().Be(0);

        var workflow = CreateAiWorkflowService();
        var confirmResult = await workflow.ConfirmDraftAsync(
            importResult.Data!.DraftId!.Value,
            new ConfirmAiDraftDto(null, "create_tasks", "approved", IdempotencyKey: "meetily-confirm-1"));
        var replay = await workflow.ConfirmDraftAsync(
            importResult.Data.DraftId.Value,
            new ConfirmAiDraftDto(null, "create_tasks", "approved", IdempotencyKey: "meetily-confirm-1"));

        confirmResult.IsSuccess.Should().BeTrue(confirmResult.Error);
        confirmResult.Data!.CreatedTaskCount.Should().Be(1);
        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Data!.CreatedTaskIds.Should().Equal(confirmResult.Data.CreatedTaskIds);
        (await _context.TaskItems.CountAsync()).Should().Be(1);
        var task = await _context.TaskItems.SingleAsync();
        task.Title.Should().Be("Prepare demo script");
        task.ProjectId.Should().Be(_projectId);
        var mapping = await _context.MeetingActionItemMappings.SingleAsync();
        mapping.TaskId.Should().Be(task.Id);
        mapping.ActionItemIndex.Should().Be(0);
        mapping.Status.Should().Be("Linked");

        _complianceServiceMock.Verify(c => c.LogAuditEventAsync(
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            "AI_DRAFT_CONFIRMED",
            "AiGeneratedDraft",
            null,
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _complianceServiceMock.Verify(c => c.LogAuditEventAsync(
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            "AI_TOOL_EXECUTED",
            It.IsAny<string>(),
            null,
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmDraftAsync_WithStaleRowVersion_ReturnsConcurrencyConflictWithoutMutation()
    {
        var importResult = await CreateMeetingImportService().ImportMeetilyAsync(CreateRequest());
        importResult.IsSuccess.Should().BeTrue(importResult.Error);
        var draft = await _context.AiGeneratedDrafts.SingleAsync(item => item.Id == importResult.Data!.DraftId);
        draft.RowVersion = [1, 2, 3, 4];
        await _context.SaveChangesAsync();

        var result = await CreateAiWorkflowService().ConfirmDraftAsync(
            draft.Id,
            new ConfirmAiDraftDto(
                null,
                "create_tasks",
                "stale edit",
                RowVersion: Convert.ToBase64String([9, 9, 9, 9]),
                IdempotencyKey: "meetily-confirm-stale"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.ErrorCode.Should().Be(AiErrorCodes.DraftConcurrencyConflict);
        (await _context.TaskItems.CountAsync()).Should().Be(0);
        (await _context.AiGeneratedDrafts.SingleAsync(item => item.Id == draft.Id))
            .ConfirmationIdempotencyKey.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmDraftAsync_WithRejectAction_MarksDraftAsRejectedAndLogsAuditEvent()
    {
        var importResult = await CreateMeetingImportService().ImportMeetilyAsync(CreateRequest());
        importResult.IsSuccess.Should().BeTrue(importResult.Error);
        var draftId = importResult.Data!.DraftId!.Value;

        var confirmResult = await CreateAiWorkflowService().ConfirmDraftAsync(
            draftId,
            new ConfirmAiDraftDto(null, "reject", "not interested", IdempotencyKey: "meetily-reject-1"));

        confirmResult.IsSuccess.Should().BeTrue(confirmResult.Error);
        confirmResult.Data!.Status.Should().Be(AiDraftStatuses.Rejected);

        var draftInDb = await _context.AiGeneratedDrafts
            .Include(d => d.AiJob)
            .FirstOrDefaultAsync(d => d.Id == draftId);
        
        draftInDb.Should().NotBeNull();
        draftInDb!.Status.Should().Be(AiDraftStatuses.Rejected);
        draftInDb.AiJob.Status.Should().Be(AiJobStatuses.Succeeded);

        _complianceServiceMock.Verify(c => c.LogAuditEventAsync(
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            "AI_TOOL_REJECTED",
            "AiGeneratedDraft",
            null,
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmDraftAsync_ExecuteActionReferencingAnotherProject_DoesNotClaimOrMutateDraft()
    {
        var otherProject = new Project { Name = "Other", Code = "OTHER", OwnerId = _userId };
        var otherTask = new TaskItem
        {
            ProjectId = otherProject.Id,
            ReporterId = _userId,
            Title = "Do not mutate",
            Status = "Todo"
        };
        var job = new AiJob
        {
            ProjectId = _projectId,
            RequestedById = _userId,
            JobType = "agent_action",
            SourceType = "manual",
            SchemaId = "agent_action.v4",
            RequestHash = new string('a', 64),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CacheKey = Guid.NewGuid().ToString("N"),
            Status = AiJobStatuses.Succeeded
        };
        var payload = JsonSerializer.Serialize(new { taskId = otherTask.Id, status = "Done" });
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = _projectId,
            DraftType = "UpdateTaskStatus",
            PayloadJson = payload,
            OriginalPayloadJson = payload,
            WorkingPayloadJson = payload,
            Status = AiDraftStatuses.PendingReview
        };
        _context.AddRange(otherProject, otherTask, job, draft);
        await _context.SaveChangesAsync();

        var result = await CreateAiWorkflowService().ConfirmDraftAsync(
            draft.Id,
            new ConfirmAiDraftDto(null, "execute_action", "approve", IdempotencyKey: "cross-project-action"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await _context.TaskItems.SingleAsync(task => task.Id == otherTask.Id)).Status.Should().Be("Todo");
        var reloadedDraft = await _context.AiGeneratedDrafts.SingleAsync(item => item.Id == draft.Id);
        reloadedDraft.Status.Should().Be(AiDraftStatuses.PendingReview);
        reloadedDraft.ConfirmationIdempotencyKey.Should().BeNull();
    }

    [Fact]
    public async Task ImportMeetilyAsync_RejectsInvalidRawPayload()
    {
        var service = CreateMeetingImportService();
        var request = CreateRequest(rawPayloadJson: "{not-json");

        var result = await service.ImportMeetilyAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        (await _context.MeetingImports.CountAsync()).Should().Be(0);
    }

    private MeetilyImportRequest CreateRequest(string? rawPayloadJson = null)
        => new(
            ProjectId: _projectId,
            Title: "Sprint planning",
            SourceId: "meetily-001",
            MeetingStartedAt: new DateTimeOffset(2026, 5, 21, 8, 0, 0, TimeSpan.Zero),
            Summary: "Team planned the demo and risk review.",
            TranscriptText: "Action: Prepare demo script before Friday.",
            Participants: ["PM", "Dev"],
            ActionItems:
            [
                new MeetilyActionItemInput(
                    "Prepare demo script",
                    "Dev",
                    new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero),
                    "High",
                    "Action: Prepare demo script before Friday.")
            ],
            RawPayloadJson: rawPayloadJson ?? JsonSerializer.Serialize(new { source = "meetily" }));

    private MeetingImportService CreateMeetingImportService()
        => new(
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            new GenericRepository<MeetingImport>(_context),
            new GenericRepository<AiJob>(_context),
            new GenericRepository<AiGeneratedDraft>(_context),
            new GenericRepository<MeetingActionItemMapping>(_context),
            new GenericRepository<TaskItem>(_context),
            new GenericRepository<GroupMeetingSession>(_context),
            _aiGateway.Object,
            _taskService.Object,
            new UnitOfWork(_context),
            _currentUser.Object,
            _auditLog.Object);

    private AiWorkflowService CreateAiWorkflowService()
        => new(
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            new GenericRepository<AiJob>(_context),
            new GenericRepository<AiGeneratedDraft>(_context),
            new GenericRepository<MeetingImport>(_context),
            new GenericRepository<MeetingActionItemMapping>(_context),
            new GenericRepository<TaskItem>(_context),
            new GenericRepository<TaskAssignment>(_context),
            new GenericRepository<User>(_context),
            new UnitOfWork(_context),
            _currentUser.Object,
            _auditLog.Object,
            complianceService: _complianceServiceMock.Object);
}
#pragma warning restore CA1707
