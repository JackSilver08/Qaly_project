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
    public async Task ChatFastAsync_WithProjectCustomMessage_CallsAiGatewayWithHistoryAndContext()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "con ai dang lam task do?";
        var history = new List<AiChatMessageDto>
        {
            new("user", "Liá»‡t kÃª cÃ¡c task cá»§a dá»± Ã¡n."),
            new("assistant", "CÃ³ task UI Fix.")
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

        _aiGatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "Nguyá»…n VÄƒn A Ä‘ang phá»¥ trÃ¡ch task Ä‘Ã³.",
                ProviderName = "TestOllama",
                ModelName = "llama3.2",
                IsMock = false
            });

        var result = await _service.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Reply.Should().Be("Nguyá»…n VÄƒn A Ä‘ang phá»¥ trÃ¡ch task Ä‘Ã³.");
        result.Data.UsedAi.Should().BeTrue();
        result.Data.Confidence.Should().Be(0.9);

        _aiGatewayMock.Verify(g => g.ExecuteAsync(It.Is<AiRequest>(r => 
            r.Prompt == message && 
            r.ProjectId == projectId && 
            r.UserId == userId && 
            r.History == history &&
            r.SystemPrompt.Contains("TASKS") &&
            r.SystemPrompt.Contains("Task UI Fix")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChatFastAsync_WithStructuredResponse_ParsesJsonAndPopulatesMetricsTablesAndCharts()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "PhÃ¢n tÃ­ch chi tiáº¿t giÃºp mÃ¬nh";
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

        var jsonResponse = @"
```json
{
  ""reply"": ""ChÃ o PM Khang, tÃ´i Ä‘Ã£ phÃ¢n tÃ­ch dá»± Ã¡n DATN."",
  ""metrics"": [
    { ""label"": ""Chá»‰ sá»‘ 1"", ""value"": ""100%"", ""tone"": ""good"", ""hint"": ""Tá»‘t"" }
  ],
  ""tables"": [
    {
      ""title"": ""Báº£ng tiáº¿n Ä‘á»™"",
      ""description"": ""Báº£ng mÃ´ táº£"",
      ""columns"": [
        { ""key"": ""col1"", ""label"": ""Cá»™t 1"", ""type"": ""text"", ""align"": ""left"" }
      ],
      ""rows"": [
        { ""col1"": ""GiÃ¡ trá»‹ 1"" }
      ]
    }
  ],
  ""charts"": [
    {
      ""type"": ""bar"",
      ""title"": ""Biá»ƒu Ä‘á»“"",
      ""labels"": [""L1""],
      ""values"": [50.0],
      ""unit"": ""giá»""
    }
  ],
  ""actions"": [
    { ""type"": ""suggested_action"", ""label"": ""CÃ¢u há»i tiáº¿p"" }
  ],
  ""files"": [
    { ""label"": ""BÃ¡o cÃ¡o"", ""format"": ""xlsx"", ""url"": ""/files/report.xlsx"", ""description"": ""Táº£i xuá»‘ng"" }
  ]
}
```";

        _aiGatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = jsonResponse,
                ProviderName = "TestOllama",
                ModelName = "llama3.2",
                IsMock = false
            });

        var result = await _service.ChatFastAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Reply.Should().Be("ChÃ o PM Khang, tÃ´i Ä‘Ã£ phÃ¢n tÃ­ch dá»± Ã¡n DATN.");
        result.Data.UsedAi.Should().BeTrue();

        result.Data.Metrics.Should().HaveCount(1);
        result.Data.Metrics[0].Label.Should().Be("Chá»‰ sá»‘ 1");
        result.Data.Metrics[0].Value.Should().Be("100%");
        result.Data.Metrics[0].Tone.Should().Be("good");

        result.Data.Tables.Should().HaveCount(1);
        result.Data.Tables[0].Title.Should().Be("Báº£ng tiáº¿n Ä‘á»™");
        result.Data.Tables[0].Columns.Should().HaveCount(1);
        result.Data.Tables[0].Columns[0].Key.Should().Be("col1");
        result.Data.Tables[0].Rows.Should().HaveCount(1);
        var tableValue = result.Data.Tables[0].Rows[0]["col1"];
        tableValue.Should().NotBeNull();
        tableValue!.ToString().Should().Be("GiÃ¡ trá»‹ 1");

        result.Data.Charts.Should().HaveCount(1);
        result.Data.Charts[0].Title.Should().Be("Biá»ƒu Ä‘á»“");
        result.Data.Charts[0].Labels.Should().ContainSingle().Which.Should().Be("L1");
        result.Data.Charts[0].Values.Should().ContainSingle().Which.Should().Be(50.0);

        result.Data.Actions.Should().HaveCount(1);
        result.Data.Actions[0].Label.Should().Be("CÃ¢u há»i tiáº¿p");

        result.Data.Files.Should().HaveCount(1);
        result.Data.Files[0].Label.Should().Be("BÃ¡o cÃ¡o");
        result.Data.Files[0].Url.Should().Be("/files/report.xlsx");
    }

    [Fact]
    public async Task ChatFastAsync_WithInvalidAiSchema_ReturnsFallbackReplyWithoutCrashing()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupProjectAiContext(projectId, userId);

        _aiGatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = """
                    ```json
                    { "reply": { "unexpected": "object" }, "metrics": "invalid schema" }
                    ```
                    """,
                ProviderName = "TestOllama",
                ModelName = "llama3.2",
                IsMock = false
            });

        var result = await _service.ChatFastAsync(
            new ErumiChatRequestDto(Message: "phan tich rui ro", ProjectId: projectId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data.Should().NotBeNull();
        result.Data!.UsedAi.Should().BeTrue();
        result.Data.Reply.Should().Contain("invalid schema");
        result.Data.Metrics.Should().BeEmpty();
        result.Data.Tables.Should().BeEmpty();
        result.Data.Charts.Should().BeEmpty();
        result.Data.Actions.Should().BeEmpty();
        result.Data.Files.Should().BeEmpty();
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

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}

