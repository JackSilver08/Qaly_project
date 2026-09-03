using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
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
    private static readonly string[] ResearchCapacityAssumptions = ["Capacity chưa được xác minh."];
    private static readonly string[] ResearchTradeOffs = ["Lùi việc phụ."];
    private static readonly string[] ResearchWarnings = ["Cần human review."];
    private static readonly string[] ResearchPrivacyNotes = ["model_claim"];

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

    [Theory]
    [InlineData("Bạn có thể giúp cho tôi những gì?")]
    [InlineData("Tôi có thể hỏi gì?")]
    [InlineData("Nên bắt đầu từ đâu với trợ lý AI?")]
    public async Task ChatFastAsync_WithCapabilityOverviewQuestion_ReturnsConsistentNavigationMenu(string message)
    {
        var result = await _service.ChatFastAsync(
            new ErumiChatRequestDto(Message: message, ProjectId: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Intent.Should().Be("capability_overview");
        result.Data.UsedAi.Should().BeFalse();
        result.Data.Model.Should().NotBeNull();
        result.Data.Model!.Id.Should().Be("qaly-native");
        result.Data.Actions.Should().HaveCount(5);
        result.Data.Actions.Should().OnlyContain(action => action.Type == "assistant_navigation");
        var payloads = result.Data.Actions
            .Select(action => JsonSerializer.Serialize(action.Payload))
            .ToArray();
        payloads.Should().Contain(payload => payload.Contains("/projects", StringComparison.Ordinal));
        payloads.Should().Contain(payload => payload.Contains("/analytics", StringComparison.Ordinal));
        _aiGatewayMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChatFastAsync_P04SessionMemory_AcknowledgesAndRecallsServerHistoryWithoutWorkspaceQuery()
    {
        const string memory = "Ghi nhớ rằng trong phiên này “MVP” nghĩa là ba chức năng: đăng ký, đặt lịch và thanh toán. Chưa tạo dữ liệu; chỉ xác nhận ngắn.";
        var acknowledged = await _service.ChatFastAsync(
            new ErumiChatRequestDto(memory, ProjectId: null),
            CancellationToken.None);

        acknowledged.IsSuccess.Should().BeTrue(acknowledged.Error);
        acknowledged.Data!.Intent.Should().Be("session_memory_ack");
        acknowledged.Data.Reply.Should().Contain("đăng ký, đặt lịch và thanh toán");
        acknowledged.Data.Reply.Should().Contain("chưa tạo hoặc thay đổi dữ liệu Qaly");

        var recalled = await _service.ChatFastAsync(
            new ErumiChatRequestDto(
                "Trong phiên này MVP nghĩa là gì?",
                ProjectId: null,
                History:
                [
                    new AiChatMessageDto("user", memory),
                    new AiChatMessageDto("assistant", acknowledged.Data.Reply)
                ]),
            CancellationToken.None);

        recalled.IsSuccess.Should().BeTrue(recalled.Error);
        recalled.Data!.Intent.Should().Be("session_memory_recall");
        recalled.Data.Reply.Should().Contain("đăng ký, đặt lịch và thanh toán");
        _analyticsServiceMock.VerifyNoOtherCalls();
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
            MemberProductivity: new List<MemberProductivityDto>
            {
                new(userId, "PM Khang", AssignedTasks: 1, DoneTasks: 0, LoggedHours: 2)
            },
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
    public async Task ChatFastAsync_CustomRoleInheritingManager_ReceivesFullAiToolTier()
    {
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var roleKey = $"ai-delivery-lead-{Guid.NewGuid():N}";
        var projectDto = new ProjectDto(
            projectId,
            "Custom AI project",
            "CUSTOM-AI",
            "Custom role authorization",
            null,
            "Active",
            null,
            null,
            ownerId,
            "Project owner",
            2,
            0,
            0,
            [],
            DateTimeOffset.UtcNow,
            organizationId,
            "AI tenant");
        _projectServiceMock.Setup(service => service.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projectDto));
        _analyticsServiceMock.Setup(service => service.GetProjectAnalyticsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ProjectAnalyticsDto(0, 0, 0, 0, 0, 0, [], [])));
        _taskServiceMock.Setup(service => service.GetByProjectAsync(
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
            .ReturnsAsync(Result.Success(new PagedResult<TaskItemDto>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 100
            }));
        _currentUserServiceMock.SetupGet(service => service.UserId).Returns(userId);
        _currentUserServiceMock.SetupGet(service => service.Role).Returns("User");
        _context.Users.AddRange(
            new User { Id = userId, FullName = "Custom manager", Email = $"custom-manager-{userId:N}@qaly.test", IsActive = true },
            new User { Id = ownerId, FullName = "Project owner", Email = $"project-owner-{ownerId:N}@qaly.test", IsActive = true });
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "AI tenant",
            Code = $"AI-TENANT-{organizationId:N}",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            OwnerId = ownerId,
            Name = projectDto.Name,
            Code = projectDto.Code
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = OrganizationRoleRules.Member
        });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Role = roleKey });
        _context.ProjectRoleDefinitions.Add(new ProjectRoleDefinition
        {
            OrganizationId = organizationId,
            Key = roleKey,
            DisplayName = "AI Delivery Lead",
            BaseRole = ProjectRoleRules.Manager,
            CreatedByUserId = ownerId,
            IsActive = true
        });
        await _context.SaveChangesAsync();
        (await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => member.Role)
            .SingleAsync()).Should().Be(roleKey);
        var roleCatalog = new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context));
        (await roleCatalog.ResolveAsync(roleKey, organizationId))!.BaseRole.Should().Be(ProjectRoleRules.Manager);
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
            Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>());
        var service = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            aiTools: aiTools,
            projectRoleCatalog: roleCatalog);

        var tools = await service.GetFilteredToolsForProjectAsync(projectId, userId, CancellationToken.None);

        tools.Should().NotBeNull();
        tools!.OfType<AIFunction>().Select(tool => tool.Name)
            .Should().Contain(["AssignTask", "SuggestTaskAssignment", "GetMemberWorkload"]);
    }

    [Fact]
    public async Task ChatFastAsync_WithExplicitDeepSeek_UsesGatewayAndReturnsActualModel()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupProjectAiContext(projectId, userId);

        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(
                It.Is<AiRequest>(request =>
                    request.ProviderHint == "deepseek" && request.StrictProvider),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "{\"reply\":\"DeepSeek response\",\"metrics\":[],\"tables\":[],\"charts\":[],\"actions\":[{\"type\":\"assistant_navigation\",\"label\":\"Injected navigation\"}],\"files\":[{\"label\":\"Fake report\",\"format\":\"pdf\",\"url\":\"https://provider.invalid/report.pdf\"}]}",
                ProviderName = "DeepSeek",
                ModelName = "deepseek-v4-pro"
            });

        var result = await _service.ChatFastAsync(new ErumiChatRequestDto(
            "Analyze this project with DeepSeek",
            projectId,
            Mode: "agent",
            ProviderHint: "deepseek"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Reply.Should().Be("DeepSeek response");
        result.Data.UsedAi.Should().BeTrue();
        result.Data.Model.Should().NotBeNull();
        result.Data.Model!.Provider.Should().Be("DeepSeek");
        result.Data.Model.Status.Should().Be("live");
        result.Data.Actions.Should().ContainSingle(action =>
            action.Type == "suggested_action" &&
            action.Label == "Injected navigation" &&
            action.Payload == null &&
            !action.RequiresConfirmation);
        result.Data.Files.Should().BeEmpty("only deterministic Qaly export flows may issue download cards");
        _aiGatewayMock.VerifyAll();
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
    public async Task ChatFastAsync_ForNaturalTaskWritePrompt_RoutesToCanonicalActionComposer()
    {
        var projectId = Guid.NewGuid();
        var workflow = new Mock<IAiWorkflowService>();

        var service = new ErumiChatService(
            _analyticsServiceMock.Object,
            _projectServiceMock.Object,
            _taskServiceMock.Object,
            _memberRepo,
            _currentUserServiceMock.Object,
            _aiGatewayMock.Object,
            aiWorkflowService: workflow.Object);

        var result = await service.ChatFastAsync(
            new ErumiChatRequestDto("Tạo 3 task kiểm thử tính năng thanh toán", projectId, Mode: "agent"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Intent.Should().Be("task_action_composer");
        result.Data.UsedAi.Should().BeFalse();
        result.Data.Actions.Should().ContainSingle(action =>
            action.Type == "compose_task_plan" && !action.RequiresConfirmation);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(result.Data.Actions[0].Payload);
        payloadJson.Should().Contain("assistant_turn.v1");
        payloadJson.Should().Contain("task.create.v1");
        payloadJson.Should().Contain(projectId.ToString());
        workflow.Verify(
            x => x.CreateJobAsync(It.IsAny<CreateAiJobDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _aiGatewayMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AssistantTurnAsync_WithCompleteTaskIntent_ReturnsRegisteredArtifact()
    {
        var projectId = Guid.NewGuid();
        _projectServiceMock
            .Setup(service => service.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(CreateProject(projectId, "Qaly Native AI")));

        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo 3 task frontend, backend và QA cho đăng nhập",
            new AiAssistantClientContextDto(ProjectId: projectId)),
            FullAssistantContext());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.SchemaId.Should().Be(AiAssistantTurnContract.SchemaId);
        result.Data.Disposition.Should().Be("registered_action");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.TaskCreateIntent);
        result.Data.ExecutionPolicy.Should().Be("draft_then_confirm");
        result.Data.Artifact.Should().NotBeNull();
        result.Data.Artifact!.ProjectId.Should().Be(projectId);
        result.Data.Clarification.Should().BeNull();
        _aiGatewayMock.Verify(
            gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AssistantTurnAsync_WithoutProject_ReturnsOneStructuredClarification()
    {
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        _projectServiceMock
            .Setup(service => service.GetAllAsync(1, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PagedResult<ProjectDto>
            {
                Items = new[]
                {
                    CreateProject(firstProjectId, "Alpha"),
                    CreateProject(secondProjectId, "Beta"),
                },
                TotalCount = 2,
                PageNumber = 1,
                PageSize = 100,
            }));

        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo task frontend và backend cho đăng nhập"),
            FullAssistantContext());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("clarification_required");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.ClarificationIntent);
        result.Data.Clarification.Should().NotBeNull();
        result.Data.Clarification!.Field.Should().Be("projectId");
        result.Data.Clarification.Choices.Should().HaveCount(2);
        result.Data.Clarification.MaxTurns.Should().Be(3);
        result.Data.Artifact.Should().BeNull();
    }

    [Fact]
    public async Task AssistantTurnAsync_ForUnregisteredMutation_ReturnsUnsupportedWithoutArtifact()
    {
        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo dự án landing page và tự triển khai luôn"),
            FullAssistantContext());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("unsupported");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.UnsupportedIntent);
        result.Data.ExecutionPolicy.Should().Be("none");
        result.Data.Artifact.Should().BeNull();
        result.Data.Clarification.Should().BeNull();
        _projectServiceMock.VerifyNoOtherCalls();
        _aiGatewayMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AssistantPlannedTurnAsync_WithoutExecutor_ReturnsDeepSeekGuidanceAndNeverCallsTools()
    {
        var request = new AiAssistantTurnRequestDto(
            "chạy tự động để test các CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            Mode: "agent");
        var fullContext = FullAssistantContext();
        var context = fullContext with
        {
            Capabilities = fullContext.Capabilities
                .Where(item => item.CapabilityId != AiProjectLaunchContract.CapabilityId)
                .ToArray()
        };
        var planning = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            request,
            context,
            "known_missing_execution_capability");

        _analyticsServiceMock
            .Setup(service => service.GetWorkspaceAnalyticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkspaceAnalyticsDto(4, 3, 24, 6, 18)));
        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(
                It.Is<AiRequest>(aiRequest =>
                    aiRequest.ExpectedSchemaId == "TextAnswer.v1" &&
                    aiRequest.ProviderHint == "deepseek-chat" &&
                    !aiRequest.StrictProvider &&
                    aiRequest.Tools == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "{\"reply\":\"Mình có thể lập ma trận kiểm thử CAND theo unit, integration và E2E. Bạn muốn ưu tiên smoke test hay regression?\",\"metrics\":[],\"tables\":[],\"charts\":[],\"actions\":[],\"files\":[],\"confidence\":0.9}",
                ProviderName = "DeepSeek",
                ModelName = "deepseek-chat"
            });

        var result = await _service.AssistantPlannedTurnAsync(request, context, planning);

        Console.WriteLine($"[DEBUG] SelectedCapabilityId: {planning.SelectedCapabilityId ?? "NULL"} Disposition: {planning.GoalAnalysis.Disposition}");
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("guided_answer");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.GuidedAnswerIntent);
        result.Data.ExecutionPolicy.Should().Be("analyze_only");
        result.Data.Artifact.Should().BeNull();
        result.Data.Answer.Should().NotBeNull();
        result.Data.Answer!.Model!.Provider.Should().Be("DeepSeek");
        result.Data.AssistantMessage.Should().Contain("ma trận kiểm thử CAND");
        result.Data.AssistantMessage.Should().Contain("chưa thể tự thực hiện trực tiếp");
        result.Data.AssistantMessage.Should().NotContain("schema");
        result.Data.AssistantMessage.Should().NotContain("adapter");
        _aiGatewayMock.VerifyAll();
    }

    [Fact]
    public async Task AssistantPlannedTurnAsync_AdvisoryProviderFailure_DegradesToManualGuidance()
    {
        var request = new AiAssistantTurnRequestDto(
            "Tạo một dự án web SPA",
            new AiAssistantClientContextDto("/dashboard", "projects"),
            Mode: "agent");
        var fullContext = FullAssistantContext();
        var context = fullContext with
        {
            Capabilities = fullContext.Capabilities
                .Where(item => item.CapabilityId != AiProjectLaunchContract.CapabilityId)
                .ToArray()
        };
        var planning = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            request,
            context,
            "goal_provider_unavailable");

        _analyticsServiceMock
            .Setup(service => service.GetWorkspaceAnalyticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkspaceAnalyticsDto(0, 0, 0, 0, 0)));
        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = false,
                ErrorCode = AiErrorCodes.ProviderUnavailable,
                ErrorMessage = "Provider unavailable"
            });

        var result = await _service.AssistantPlannedTurnAsync(request, context, planning);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("guided_answer");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.GuidedAnswerIntent);
        result.Data.Answer.Should().NotBeNull();
        result.Data.Answer!.UsedAi.Should().BeFalse();
        result.Data.Answer.Model.Should().NotBeNull();
        result.Data.Answer.Model!.Provider.Should().Be("Qaly");
        result.Data.Answer.Model.Id.Should().Be("qaly-native");
        result.Data.Answer.Model.Status.Should().Be("server_fallback");
        result.Data.ActualProvider.Should().Be("Qaly");
        result.Data.ActualModel.Should().Be("qaly-native");
        result.Data.Artifact.Should().BeNull();
        result.Data.AssistantMessage.Should().Contain("hướng dẫn dự phòng trên máy chủ");
        result.Data.AssistantMessage.Should().Contain("chưa có dữ liệu nào được thay đổi");
    }

    [Fact]
    public async Task AssistantPlannedTurnAsync_GroundedProviderFailure_UsesCanonicalServerReaderInsteadOfCannedPitch()
    {
        var request = new AiAssistantTurnRequestDto(
            "Tóm tắt tình hình workspace bằng dữ liệu thật",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            Mode: "agent");
        var context = FullAssistantContext();
        var planning = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            request,
            context,
            "goal_provider_unavailable");
        planning.SelectedCapabilityId.Should().Be(AiAssistantContextContract.GroundedReadCapability);

        _analyticsServiceMock
            .Setup(service => service.GetWorkspaceAnalyticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkspaceAnalyticsDto(0, 0, 0, 0, 0)));
        _projectServiceMock
            .Setup(service => service.GetAllAsync(1, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PagedResult<ProjectDto>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 100
            }));
        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = false,
                ErrorCode = AiErrorCodes.ProviderUnavailable,
                ErrorMessage = "Provider unavailable"
            });

        var result = await _service.AssistantPlannedTurnAsync(request, context, planning);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.AssistantMessage.Should().Contain("chưa thấy dự án");
        result.Data.AssistantMessage.Should().NotContain("Mình có thể đọc dữ liệu Qaly để trả lời");
        result.Data.ActualProvider.Should().Be("Qaly");
        result.Data.ActualModel.Should().Be("qaly-native");
        result.Data.Answer!.Intent.Should().Be("workspace_summary");
        result.Data.Answer.Model!.Status.Should().Be("server_fallback");
    }

    [Fact]
    public async Task AssistantPlannedTurnAsync_P26ResearchProviderFailure_ContinuesWithCanonicalProjectReader()
    {
        var projectId = Guid.NewGuid();
        var sourceRef = $"qaly://project/{projectId:D}/project/summary@abc123";
        var request = new AiAssistantTurnRequestDto(
            "Phân tích Project hiện tại và đề xuất bước tiếp theo. Nếu model lỗi, dùng fallback server có ích, ghi actual provider/model và tiếp tục; không báo thành công cho thao tác chưa ghi.",
            new AiAssistantClientContextDto($"/projects/{projectId:D}", "project", projectId, "project", projectId),
            Mode: "agent");
        var context = ResearchAssistantContext(projectId, sourceRef);
        var planning = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            request,
            context,
            "goal_provider_unavailable");
        planning.SelectedCapabilityId.Should().Be(AiAssistantContextContract.ResearchPlanCapability);

        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = false,
                ErrorCode = AiErrorCodes.ProviderUnavailable,
                ErrorMessage = "Provider unavailable",
                Retryable = true
            });
        _projectServiceMock
            .Setup(service => service.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(CreateProject(projectId, "P26 canonical project")));
        _analyticsServiceMock
            .Setup(service => service.GetProjectAnalyticsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ProjectAnalyticsDto(
                8, 3, 2, 1, 40, 18,
                [new MemberProductivityDto(Guid.NewGuid(), "Mai", 4, 2, 12)],
                [])));

        var result = await _service.AssistantPlannedTurnAsync(request, context, planning);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("grounded_fallback");
        result.Data.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        result.Data.ExecutionPolicy.Should().Be("read_only");
        result.Data.ActualProvider.Should().Be("Qaly");
        result.Data.ActualModel.Should().Be("qaly-native");
        result.Data.Answer!.Model!.Status.Should().Be("server_fallback");
        result.Data.Answer.ConfidenceReason.Should().Contain("Research model không phản hồi");
        result.Data.AssistantMessage.Should().Contain("P26 canonical project");
        result.Data.Artifact.Should().BeNull();
    }

    [Fact]
    public async Task AssistantTurnAsync_ForExistingTaskAssignment_DoesNotMisrouteToTaskCreate()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Giao task đăng nhập hiện có cho Minh",
            new AiAssistantClientContextDto(
                ProjectId: projectId,
                EntityType: "task",
                EntityId: taskId)),
            FullAssistantContext());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("registered_action");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.TaskAssignmentScheduleIntent);
        result.Data.Artifact.Should().BeNull();
        result.Data.Answer!.Actions.Should().ContainSingle(action =>
            action.Type == "assistant_navigation" &&
            JsonSerializer.Serialize(action.Payload).Contains($"/projects/{projectId}/tasks/{taskId}?assignmentPlanner=1", StringComparison.Ordinal));
        _projectServiceMock.VerifyNoOtherCalls();
        _aiGatewayMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AssistantTurnAsync_ForResearchIntent_ReturnsGroundedPlanAndModelTruth()
    {
        var projectId = Guid.NewGuid();
        var sourceRef = $"qaly://project/{projectId:D}/project/summary@abc123";
        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(
                It.Is<AiRequest>(request =>
                    request.ExpectedSchemaId == AiAssistantResearchPlanContract.SchemaId &&
                    request.Tools == null &&
                    request.ValidationContextJson != null &&
                    request.ValidationContextJson.Contains(sourceRef, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = ResearchPlanJson(projectId, sourceRef),
                ProviderName = "DeepSeek",
                ModelName = "deepseek-reasoner"
            });

        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Phân tích rủi ro và đề xuất phương án xử lý",
            new AiAssistantClientContextDto(ProjectId: projectId),
            ProviderHint: "deepseek"),
            ResearchAssistantContext(projectId, sourceRef));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Disposition.Should().Be("research_plan");
        result.Data.Intent.Should().Be(AiAssistantTurnContract.ResearchPlanIntent);
        result.Data.ExecutionPolicy.Should().Be("read_only_proposal");
        result.Data.ResearchPlan.Should().NotBeNull();
        result.Data.ResearchPlan!.Findings.Should().OnlyContain(finding => finding.SourceRefs.Contains(sourceRef));
        result.Data.ResearchPlan.ActualProvider.Should().Be("DeepSeek");
        result.Data.ResearchPlan.ProposedActions.Should().ContainSingle(action =>
            action.CapabilityId == "project.create.v1" && !action.ExecutionEligible);
        result.Data.Answer!.Model!.Provider.Should().Be("DeepSeek");
    }

    [Fact]
    public async Task AssistantTurnAsync_ForResearchIntent_RejectsSchemaInvalidProviderPayload()
    {
        var projectId = Guid.NewGuid();
        var sourceRef = $"qaly://project/{projectId:D}/project/summary@abc123";
        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "{\"schemaId\":\"assistant_research_plan.v1\"}",
                ProviderName = "DeepSeek",
                ModelName = "deepseek-reasoner"
            });

        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Đánh giá tiến độ và đề xuất kế hoạch",
            new AiAssistantClientContextDto(ProjectId: projectId)),
            ResearchAssistantContext(projectId, sourceRef));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(422);
        result.ErrorCode.Should().Be("assistant_research_schema_invalid");
    }

    [Fact]
    public async Task AssistantTurnAsync_ForResearchIntent_PreservesProviderUnavailableFailure()
    {
        var projectId = Guid.NewGuid();
        var sourceRef = $"qaly://project/{projectId:D}/project/summary@abc123";
        _aiGatewayMock
            .Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = false,
                ErrorCode = AiErrorCodes.ProviderUnavailable,
                ErrorMessage = "Provider unavailable",
                Retryable = true
            });

        var result = await _service.AssistantTurnAsync(new AiAssistantTurnRequestDto(
            "Phân tích tiến độ và đề xuất phương án",
            new AiAssistantClientContextDto(ProjectId: projectId)),
            ResearchAssistantContext(projectId, sourceRef));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.ErrorCode.Should().Be(AiErrorCodes.ProviderUnavailable);
    }

    private static AiAssistantExecutionContextDto FullAssistantContext()
        => new(AiAssistantCapabilityCatalog.All.ToList(), [], []);

    private static AiAssistantExecutionContextDto ResearchAssistantContext(Guid projectId, string sourceRef)
        => new(
            AiAssistantCapabilityCatalog.All.ToList(),
            [new AiAssistantContextSourceEnvelopeDto(
                AiAssistantContextContract.ProjectSummarySource,
                sourceRef,
                "project",
                "Qaly Native AI",
                DateTimeOffset.UtcNow,
                "qaly_domain_record",
                "project_private",
                "sha256:abc123",
                new Dictionary<string, object?> { ["projectId"] = projectId, ["name"] = "Qaly Native AI" },
                [],
                "deterministic")],
            [new AiAssistantSourceDisclosureDto(
                AiAssistantContextContract.ProjectSummarySource,
                "read",
                "Authorized",
                sourceRef)]);

    private static string ResearchPlanJson(Guid projectId, string sourceRef)
        => JsonSerializer.Serialize(new
        {
            schemaId = AiAssistantResearchPlanContract.SchemaId,
            promptId = AiAssistantResearchPlanContract.PromptId,
            promptVersion = AiAssistantResearchPlanContract.PromptVersion,
            objective = "Phân tích rủi ro và đề xuất phương án xử lý",
            scope = new { scopeType = "project", projectId, label = "Qaly Native AI", sourceRefs = new[] { sourceRef } },
            findings = new[]
            {
                new { findingId = "F1", statement = "Dự án cần ưu tiên xử lý rủi ro.", severity = "high", confidence = 0.9, sourceRefs = new[] { sourceRef } }
            },
            unknowns = new[] { new { unknownId = "U1", question = "Capacity sắp tới là bao nhiêu?", blocking = false } },
            assumptions = ResearchCapacityAssumptions,
            options = new[]
            {
                new { optionId = "O1", title = "Ưu tiên rủi ro", outcome = "Giảm rủi ro chính.", tradeOffs = ResearchTradeOffs, estimatedEffort = "1 ngày", risk = "Chậm hạng mục phụ." }
            },
            recommendedOptionId = "O1",
            recommendationRationale = "Phương án dùng fact có nguồn.",
            proposedActions = new[]
            {
                new { actionId = "A1", capabilityId = "project.create.v1", title = "Tạo project khác", dependencyIds = Array.Empty<string>(), draftInput = new { name = "Draft" }, sourceRefs = new[] { sourceRef }, executionEligible = true, eligibilityReason = "model_claim" }
            },
            warnings = ResearchWarnings,
            privacyNotes = ResearchPrivacyNotes,
            freshnessAt = DateTimeOffset.UnixEpoch,
            generatedAt = DateTimeOffset.UnixEpoch,
            actualProvider = "model_claim",
            actualModel = "model_claim"
        });

    private static ProjectDto CreateProject(Guid id, string name)
        => new(
            id,
            name,
            name.ToUpperInvariant().Replace(' ', '-'),
            null,
            null,
            "Active",
            null,
            null,
            Guid.NewGuid(),
            "Project owner",
            1,
            0,
            0,
            Array.Empty<ProjectLabelDto>(),
            DateTimeOffset.UtcNow,
            null,
            null);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}

