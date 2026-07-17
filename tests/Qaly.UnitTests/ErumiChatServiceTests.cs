using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.AI;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Analytics;
using Qaly.Application.DTOs.Project;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Xunit;

namespace Qaly.UnitTests;

public class ErumiChatServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly GenericRepository<ProjectMember> _memberRepo;

    private readonly Mock<IAnalyticsService> _analyticsServiceMock = new();
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<ITaskService> _taskServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IAiGateway> _aiGatewayMock = new();

    private readonly ErumiChatService _service;

    public ErumiChatServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
        _memberRepo = new GenericRepository<ProjectMember>(_context);

        _service = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object);
    }

    [Fact]
    public async Task ChatFastAsync_WithGreeting_ReturnsStaticGreetingResponseWithoutAi()
    {
        var request = new ErumiChatRequestDto(Message: "Xin chao", ProjectId: null);

        var result = await _service.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Intent.Should().Be("greeting");
        result.Data.UsedAi.Should().BeFalse();
        result.Data.Reply.Should().Contain("Erumi");
        _aiGatewayMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChatFastAsync_WithProjectCustomMessage_ReturnsLocalProjectSummaryWithoutAi()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "con ai dang lam task do?";
        var history = new List<AiChatMessageDto>
        {
            new("user", "Liệt kê các task của dự án."),
            new("assistant", "Có task UI Fix.")
        };

        var request = new ErumiChatRequestDto(
            Message: message,
            ProjectId: projectId,
            History: history);

        var projectDto = new ProjectDto(
            Id: projectId,
            Name: "DATN",
            Code: "DATN",
            Description: "DATN project",
            LogoUrl: null,
            Status: "Active",
            StartDate: null,
            EndDate: null,
            OwnerId: userId,
            OwnerName: "PM Khang",
            MemberCount: 1,
            TaskCount: 1,
            ProgressPercentage: 0,
            Labels: Array.Empty<ProjectLabelDto>(),
            CreatedAt: DateTimeOffset.UtcNow,
            OrganizationId: null,
            OrganizationName: null);

        _projectServiceMock.Setup(s => s.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projectDto));

        var analyticsDto = new ProjectAnalyticsDto(
            TotalTasks: 1,
            DoneTasks: 0,
            InProgressTasks: 1,
            OverdueTasks: 0,
            TotalEstimatedHours: 10,
            TotalActualHours: 2,
            MemberProductivity: new List<MemberProductivityDto>(),
            DailyProductivity: new List<DailyProductivityDto>());

        _analyticsServiceMock.Setup(s => s.GetProjectAnalyticsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(analyticsDto));

        var taskDto = new TaskItemDto(
            Id: Guid.NewGuid(),
            Title: "Task UI Fix",
            Description: "Fix UI",
            Status: "InProgress",
            Priority: "High",
            DueDate: DateTimeOffset.UtcNow.AddDays(1),
            EstimatedHours: 10,
            ActualHours: 2,
            IsPrivate: false,
            IsRestricted: false,
            IsPinned: false,
            ContributesToProgress: true,
            UpvoteCount: 0,
            DownvoteCount: 0,
            ProjectId: projectId,
            ProjectName: "DATN",
            AssigneeId: null,
            AssigneeName: "PM Khang",
            Assignees: Array.Empty<TaskAssigneeDto>(),
            Labels: Array.Empty<TaskLabelDto>(),
            ReporterId: userId,
            ReporterName: "PM Khang",
            CommentCount: 0,
            AttachmentCount: 0,
            AiPrioritySuggestion: null,
            CreatedAt: DateTimeOffset.UtcNow,
            SortOrder: 0,
            RowVersion: "1"
        );

        var pagedTasks = new PagedResult<TaskItemDto>
        {
            Items = new List<TaskItemDto> { taskDto },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 100
        };

        _taskServiceMock.Setup(s => s.GetByProjectAsync(
                projectId, 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<string>(), 
                It.IsAny<Guid?>(), 
                It.IsAny<Guid?>(), 
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedTasks));

        _currentUserServiceMock.SetupGet(u => u.UserId).Returns(userId);

        var result = await _service.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Reply.Should().Contain("DATN");
        result.Data.Reply.Should().Contain("Task UI Fix");
        result.Data.Reply.Should().Contain("PM Khang");
        result.Data.Intent.Should().Be("task_assignee_lookup");
        result.Data.UsedAi.Should().BeFalse();
        result.Data.Confidence.Should().BeGreaterThan(0.8);
        result.Data.Sources.Should().Contain("AnalyticsService");

        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_WithProjectAnalysis_PopulatesLocalMetricsAndCharts()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "Phân tích chi tiết giúp mình";
        var request = new ErumiChatRequestDto(Message: message, ProjectId: projectId);

        var projectDto = new ProjectDto(
            Id: projectId,
            Name: "DATN",
            Code: "DATN",
            Description: "DATN project",
            LogoUrl: null,
            Status: "Active",
            StartDate: null,
            EndDate: null,
            OwnerId: userId,
            OwnerName: "PM Khang",
            MemberCount: 1,
            TaskCount: 1,
            ProgressPercentage: 0,
            Labels: Array.Empty<ProjectLabelDto>(),
            CreatedAt: DateTimeOffset.UtcNow,
            OrganizationId: null,
            OrganizationName: null);

        _projectServiceMock.Setup(s => s.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projectDto));

        var analyticsDto = new ProjectAnalyticsDto(
            TotalTasks: 1,
            DoneTasks: 0,
            InProgressTasks: 1,
            OverdueTasks: 0,
            TotalEstimatedHours: 10,
            TotalActualHours: 2,
            MemberProductivity: new List<MemberProductivityDto>(),
            DailyProductivity: new List<DailyProductivityDto>());

        _analyticsServiceMock.Setup(s => s.GetProjectAnalyticsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(analyticsDto));

        var pagedTasks = new PagedResult<TaskItemDto>
        {
            Items = new List<TaskItemDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 100
        };

        _taskServiceMock.Setup(s => s.GetByProjectAsync(
                projectId, 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<string>(), 
                It.IsAny<Guid?>(), 
                It.IsAny<Guid?>(), 
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedTasks));

        _currentUserServiceMock.SetupGet(u => u.UserId).Returns(userId);

        var result = await _service.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Reply.Should().Contain("DATN");
        result.Data.UsedAi.Should().BeFalse();
        result.Data.Metrics.Should().NotBeEmpty();
        result.Data.Metrics.Should().Contain(metric => metric.Value.Contains('1'));
        result.Data.Charts.Should().NotBeEmpty();
        result.Data.Actions.Should().NotBeEmpty();
        result.Data.Files.Should().BeEmpty();
        result.Data.Sources.Should().Contain("AnalyticsService");

        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_WithPlainProjectSummary_DoesNotAttachVisualPayloads()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupProjectAiContext(projectId, userId);

        var result = await _service.ChatFastAsync(
            new ErumiChatRequestDto(Message: "tom tat du an", ProjectId: projectId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data.Should().NotBeNull();
        result.Data!.UsedAi.Should().BeFalse();
        result.Data.Intent.Should().Be("project_summary");
        result.Data.Reply.Should().Contain("DATN");
        result.Data.Metrics.Should().BeEmpty();
        result.Data.Tables.Should().BeEmpty();
        result.Data.Charts.Should().BeEmpty();

        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_WithInvalidAiSchema_ReturnsFallbackReplyWithoutCrashing()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupProjectAiContext(projectId, userId);

        var result = await _service.ChatFastAsync(
            new ErumiChatRequestDto(Message: "phan tich rui ro", ProjectId: projectId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data.Should().NotBeNull();
        result.Data!.UsedAi.Should().BeFalse();
        result.Data.Reply.Should().Contain("DATN");
        result.Data.Metrics.Should().NotBeEmpty();
        result.Data.Charts.Should().BeEmpty();
        result.Data.Actions.Should().NotBeEmpty();
        result.Data.Files.Should().BeEmpty();

        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_WhenProjectAccessForbidden_ReturnsForbiddenAndDoesNotCallAiGateway()
    {
        var projectId = Guid.NewGuid();

        _projectServiceMock.Setup(s => s.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<ProjectDto>("Current user cannot access project."));

        var result = await _service.ChatFastAsync(
            new ErumiChatRequestDto(Message: "xem analytics cua du an", ProjectId: projectId),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Error.Should().Contain("Current user cannot access project");
        _aiGatewayMock.Verify(
            gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupProjectAiContext(Guid projectId, Guid userId)
    {
        var projectDto = new ProjectDto(
            Id: projectId,
            Name: "DATN",
            Code: "DATN",
            Description: "DATN project",
            LogoUrl: null,
            Status: "Active",
            StartDate: null,
            EndDate: null,
            OwnerId: userId,
            OwnerName: "PM Khang",
            MemberCount: 1,
            TaskCount: 0,
            ProgressPercentage: 0,
            Labels: Array.Empty<ProjectLabelDto>(),
            CreatedAt: DateTimeOffset.UtcNow,
            OrganizationId: null,
            OrganizationName: null);

        _projectServiceMock.Setup(s => s.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projectDto));

        var analyticsDto = new ProjectAnalyticsDto(
            TotalTasks: 0,
            DoneTasks: 0,
            InProgressTasks: 0,
            OverdueTasks: 0,
            TotalEstimatedHours: 0,
            TotalActualHours: 0,
            MemberProductivity: new List<MemberProductivityDto>(),
            DailyProductivity: new List<DailyProductivityDto>());

        _analyticsServiceMock.Setup(s => s.GetProjectAnalyticsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(analyticsDto));

        var pagedTasks = new PagedResult<TaskItemDto>
        {
            Items = new List<TaskItemDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 100
        };

        _taskServiceMock.Setup(s => s.GetByProjectAsync(
                projectId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedTasks));

        _currentUserServiceMock.SetupGet(u => u.UserId).Returns(userId);
    }

    [Fact]
    public async Task ChatFastAsync_ForProjectPM_ReturnsLocalResponseWithoutAiGateway()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        SetupProjectAiContext(projectId, userId);
        _currentUserServiceMock.SetupGet(u => u.Role).Returns("Member");

        _aiGatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "Hello",
                ProviderName = "Test",
                ModelName = "test",
                IsMock = false
            });

        var aiTools = new AiTools(
            _taskServiceMock.Object,
            _projectServiceMock.Object,
            Mock.Of<ICommentService>(),
            Mock.Of<ITimeTrackingService>(),
            Mock.Of<IAiExportService>(),
            Mock.Of<IRepository<Project>>(),
            Mock.Of<IRepository<TaskItem>>(),
            _memberRepo,
            _currentUserServiceMock.Object,
            Mock.Of<IVectorStorageService>(),
            Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>()
        );

        var serviceWithTools = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            aiTools);

        var request = new ErumiChatRequestDto(Message: "Show project summary", ProjectId: projectId);
        var result = await serviceWithTools.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.UsedAi.Should().BeFalse();
        result.Data.Sources.Should().Contain("AnalyticsService");
        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_ForProjectMember_ReturnsLocalResponseWithoutAiGateway()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        
        SetupProjectAiContext(projectId, userId);
        
        var projectDto = new ProjectDto(
            Id: projectId,
            Name: "Member Project",
            Code: "MP",
            Description: "Project description",
            LogoUrl: null,
            Status: "Active",
            StartDate: null,
            EndDate: null,
            OwnerId: ownerId,
            OwnerName: "PM Owner",
            MemberCount: 2,
            TaskCount: 0,
            ProgressPercentage: 0,
            Labels: Array.Empty<ProjectLabelDto>(),
            CreatedAt: DateTimeOffset.UtcNow,
            OrganizationId: null,
            OrganizationName: null);

        _projectServiceMock.Setup(s => s.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projectDto));

        var memberEntity = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = "Member"
        };
        _context.ProjectMembers.Add(memberEntity);
        await _context.SaveChangesAsync();

        _currentUserServiceMock.SetupGet(u => u.Role).Returns("Member");

        _aiGatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "Hello",
                ProviderName = "Test",
                ModelName = "test",
                IsMock = false
            });

        var aiTools = new AiTools(
            _taskServiceMock.Object,
            _projectServiceMock.Object,
            Mock.Of<ICommentService>(),
            Mock.Of<ITimeTrackingService>(),
            Mock.Of<IAiExportService>(),
            Mock.Of<IRepository<Project>>(),
            Mock.Of<IRepository<TaskItem>>(),
            _memberRepo,
            _currentUserServiceMock.Object,
            Mock.Of<IVectorStorageService>(),
            Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>()
        );

        var serviceWithTools = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            aiTools);

        var request = new ErumiChatRequestDto(Message: "Show project summary", ProjectId: projectId);
        var result = await serviceWithTools.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.UsedAi.Should().BeFalse();
        result.Data.Sources.Should().Contain("AnalyticsService");
        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_ForProjectGuest_ReturnsLocalResponseWithoutAiGateway()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        
        SetupProjectAiContext(projectId, userId);

        var projectDto = new ProjectDto(
            Id: projectId,
            Name: "Guest Project",
            Code: "GP",
            Description: "Project description",
            LogoUrl: null,
            Status: "Active",
            StartDate: null,
            EndDate: null,
            OwnerId: ownerId,
            OwnerName: "PM Owner",
            MemberCount: 1,
            TaskCount: 0,
            ProgressPercentage: 0,
            Labels: Array.Empty<ProjectLabelDto>(),
            CreatedAt: DateTimeOffset.UtcNow,
            OrganizationId: null,
            OrganizationName: null);

        _projectServiceMock.Setup(s => s.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projectDto));

        _currentUserServiceMock.SetupGet(u => u.Role).Returns("Member");

        _aiGatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "Hello",
                ProviderName = "Test",
                ModelName = "test",
                IsMock = false
            });

        var aiTools = new AiTools(
            _taskServiceMock.Object,
            _projectServiceMock.Object,
            Mock.Of<ICommentService>(),
            Mock.Of<ITimeTrackingService>(),
            Mock.Of<IAiExportService>(),
            Mock.Of<IRepository<Project>>(),
            Mock.Of<IRepository<TaskItem>>(),
            _memberRepo,
            _currentUserServiceMock.Object,
            Mock.Of<IVectorStorageService>(),
            Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>()
        );

        var serviceWithTools = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            aiTools);

        var request = new ErumiChatRequestDto(Message: "Show project summary", ProjectId: projectId);
        var result = await serviceWithTools.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.UsedAi.Should().BeFalse();
        result.Data.Sources.Should().Contain("AnalyticsService");
        _aiGatewayMock.Verify(
            g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_InAgentMode_UsesAgentFrameworkOrchestrator()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupProjectAiContext(projectId, userId);

        var orchestrator = new Mock<IAiAgentOrchestrator>();
        orchestrator.SetupGet(x => x.IsEnabled).Returns(true);
        orchestrator
            .Setup(x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "{\"reply\":\"Agent response\",\"metrics\":[],\"tables\":[],\"charts\":[],\"actions\":[],\"files\":[],\"confidence\":0.9}",
                ProviderName = "MicrosoftAgentFramework",
                ModelName = "test-agent"
            });

        var service = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            agentOrchestrator: orchestrator.Object);

        var result = await service.ChatFastAsync(
            new ErumiChatRequestDto("Hãy lập kế hoạch cải thiện dự án", projectId, Mode: "agent"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Reply.Should().Be("Agent response");
        result.Data.UsedAi.Should().BeTrue();
        orchestrator.Verify(
            x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _aiGatewayMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatFastAsync_WhenAgentFrameworkFails_FallsBackToAiGateway()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupProjectAiContext(projectId, userId);

        var orchestrator = new Mock<IAiAgentOrchestrator>();
        orchestrator.SetupGet(x => x.IsEnabled).Returns(true);
        orchestrator
            .Setup(x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Agent provider unavailable"));

        _aiGatewayMock
            .Setup(x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "{\"reply\":\"Gateway fallback\",\"metrics\":[],\"tables\":[],\"charts\":[],\"actions\":[],\"files\":[],\"confidence\":0.8}",
                ProviderName = "Fallback",
                ModelName = "fallback-model"
            });

        var service = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            agentOrchestrator: orchestrator.Object);

        var result = await service.ChatFastAsync(
            new ErumiChatRequestDto("Hãy lập kế hoạch cải thiện dự án", projectId, Mode: "agent"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Reply.Should().Be("Gateway fallback");
        _aiGatewayMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatFastAsync_ForWritePrompt_CreatesApprovalGatedAutonomousDraft()
    {
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var workflow = new Mock<IAiWorkflowService>();
        workflow
            .Setup(x => x.CreateJobAsync(
                It.Is<CreateAiJobDto>(dto =>
                    dto.ProjectId == projectId &&
                    dto.JobType == "erumi_autonomous_tasks" &&
                    dto.SourceText == "Tao task kiem thu tinh nang thanh toan"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Created(new AiJobCreatedDto(jobId, "DraftReady", 0.001m, "cache", draftId)));

        var service = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            aiWorkflowService: workflow.Object);

        var result = await service.ChatFastAsync(
            new ErumiChatRequestDto("Tao task kiem thu tinh nang thanh toan", projectId, Mode: "agent"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Intent.Should().Be("autonomous_task_plan");
        result.Data.UsedAi.Should().BeTrue();
        result.Data.Actions.Should().ContainSingle(action =>
            action.Type == "draft_change" && action.RequiresConfirmation);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(result.Data.Actions[0].Payload);
        payloadJson.Should().Contain(draftId.ToString());
        workflow.VerifyAll();
        _aiGatewayMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}

