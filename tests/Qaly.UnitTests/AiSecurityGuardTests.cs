using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public class AiSecurityGuardTests
{
    [Fact]
    public async Task SmartSearchWithoutProjectIdReturnsGuardMessage()
    {
        var service = CreateAiService();

        var result = await service.SmartSearchAsync("find login issue");

        result.Should().ContainSingle();
        result[0].Should().ContainEquivalentOf("select a project");
    }

    [Fact]
    public async Task ChatWithoutProjectIdReturnsGuardMessage()
    {
        var service = CreateAiService();

        var result = await service.ChatAsync("summarize this");

        result.Should().ContainEquivalentOf("select a project");
    }

    [Fact]
    public async Task StreamingChatWithoutProjectIdReturnsGuardMessage()
    {
        var service = CreateAiService();

        var chunks = new List<string>();
        await foreach (var chunk in service.ChatStreamingAsync("summarize this"))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle();
        chunks[0].Should().ContainEquivalentOf("select a project");
    }

    [Fact]
    public async Task SearchKnowledgeWithoutProjectIdReturnsGuardMessage()
    {
        var tools = CreateAiTools();

        var result = await tools.SearchKnowledge("deployment notes");

        result.Should().ContainEquivalentOf("select a project");
    }

    [Fact]
    public async Task GeneratePlanFallsBackWhenAiReturnsInvalidJson()
    {
        var aiGateway = new Mock<IAiGateway>();
        aiGateway
            .Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse { Content = "not-json" });
        var service = CreateAiService(aiGateway);

        var result = await service.GeneratePlanAsync("Create a payment checkout project", null);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsNewProject.Should().BeTrue();
        result.Data.Tasks.Should().HaveCountGreaterThanOrEqualTo(5);
        result.Data.Tasks.Should().OnlyContain(task => task.EstimatedHours.HasValue && task.EstimatedHours.Value > 0);
    }

    [Fact]
    public async Task GetProjectWithTasksAsync_AppliesCanonicalPrivateTaskVisibilityBeforeExport()
    {
        await using var context = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var currentUserId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var project = new Project
        {
            Name = "Export boundary",
            Code = "EXPORT-BOUNDARY",
            OwnerId = ownerId
        };
        context.Projects.Add(project);
        context.TaskItems.AddRange(
            new TaskItem { ProjectId = project.Id, ReporterId = ownerId, Title = "Public task", Status = "Todo" },
            new TaskItem { ProjectId = project.Id, ReporterId = ownerId, Title = "Private task", Status = "Todo", IsPrivate = true });
        await context.SaveChangesAsync();

        var projectRepo = new GenericRepository<Project>(context);
        var taskRepo = new GenericRepository<TaskItem>(context);
        var memberRepo = new GenericRepository<ProjectMember>(context);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(currentUserId);
        var taskAccessPolicy = new Mock<ITaskAccessPolicy>();
        taskAccessPolicy
            .Setup(policy => policy.CanAccessProjectAsync(project.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        taskAccessPolicy
            .Setup(policy => policy.ApplyVisibilityFilter(It.IsAny<IQueryable<TaskItem>>()))
            .Returns((IQueryable<TaskItem> query) => query.Where(task => !task.IsPrivate));
        var aiTools = new AiTools(
            Mock.Of<ITaskService>(),
            Mock.Of<IProjectService>(),
            Mock.Of<ICommentService>(),
            Mock.Of<ITimeTrackingService>(),
            Mock.Of<IAiExportService>(),
            projectRepo,
            taskRepo,
            memberRepo,
            currentUser.Object,
            Mock.Of<IVectorStorageService>(),
            Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>());
        var service = new AiService(
            Mock.Of<IAiGateway>(),
            Mock.Of<IEmbeddingGenerator<string, Embedding<float>>>(),
            Mock.Of<IVectorStorageService>(),
            projectRepo,
            taskRepo,
            memberRepo,
            currentUser.Object,
            taskAccessPolicy.Object,
            NullLogger<AiService>.Instance,
            aiTools);

        var result = await service.GetProjectWithTasksAsync(project.Id);

        result.Should().NotBeNull();
        result!.Tasks.Should().ContainSingle(task => task.Title == "Public task");
        result.Tasks.Should().NotContain(task => task.Title == "Private task");
    }

    private static AiService CreateAiService(Mock<IAiGateway>? aiGateway = null)
    {
        aiGateway ??= new Mock<IAiGateway>();
        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var vectorStorage = new Mock<IVectorStorageService>();
        var projectRepo = new Mock<IRepository<Project>>();
        var taskRepo = new Mock<IRepository<TaskItem>>();
        var memberRepo = new Mock<IRepository<ProjectMember>>();
        var currentUser = new Mock<ICurrentUserService>();
        var taskAccessPolicy = new Mock<ITaskAccessPolicy>();
        var aiTools = CreateAiTools();

        return new AiService(
            aiGateway.Object,
            embeddingGenerator.Object,
            vectorStorage.Object,
            projectRepo.Object,
            taskRepo.Object,
            memberRepo.Object,
            currentUser.Object,
            taskAccessPolicy.Object,
            NullLogger<AiService>.Instance,
            aiTools);
    }

    private static AiTools CreateAiTools()
    {
        var taskService = new Mock<ITaskService>();
        var projectService = new Mock<IProjectService>();
        var commentService = new Mock<ICommentService>();
        var timeTrackingService = new Mock<ITimeTrackingService>();
        var exportService = new Mock<IAiExportService>();
        var projectRepo = new Mock<IRepository<Project>>();
        var taskRepo = new Mock<IRepository<TaskItem>>();
        var memberRepo = new Mock<IRepository<ProjectMember>>();
        var currentUser = new Mock<ICurrentUserService>();
        var vectorStorage = new Mock<IVectorStorageService>();
        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        return new AiTools(
            taskService.Object,
            projectService.Object,
            commentService.Object,
            timeTrackingService.Object,
            exportService.Object,
            projectRepo.Object,
            taskRepo.Object,
            memberRepo.Object,
            currentUser.Object,
            vectorStorage.Object,
            embeddingGenerator.Object);
    }
}
