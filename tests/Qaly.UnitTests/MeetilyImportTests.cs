using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
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

        var confirmResult = await CreateAiWorkflowService().ConfirmDraftAsync(
            importResult.Data!.DraftId!.Value,
            new ConfirmAiDraftDto(null, "create_tasks", "approved"));

        confirmResult.IsSuccess.Should().BeTrue(confirmResult.Error);
        confirmResult.Data!.CreatedTaskCount.Should().Be(1);
        var task = await _context.TaskItems.SingleAsync();
        task.Title.Should().Be("Prepare demo script");
        task.ProjectId.Should().Be(_projectId);
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
            new GenericRepository<TaskItem>(_context),
            new GenericRepository<TaskAssignment>(_context),
            new GenericRepository<User>(_context),
            new UnitOfWork(_context),
            _currentUser.Object,
            _auditLog.Object);
}
#pragma warning restore CA1707
