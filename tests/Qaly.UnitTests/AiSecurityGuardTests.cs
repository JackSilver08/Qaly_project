using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

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

    private static AiService CreateAiService()
    {
        var chatClient = new Mock<IChatClient>();
        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var vectorStorage = new Mock<IVectorStorageService>();
        var projectRepo = new Mock<IRepository<Project>>();
        var taskRepo = new Mock<IRepository<TaskItem>>();
        var memberRepo = new Mock<IRepository<ProjectMember>>();
        var currentUser = new Mock<ICurrentUserService>();
        var aiTools = CreateAiTools();

        return new AiService(
            chatClient.Object,
            embeddingGenerator.Object,
            vectorStorage.Object,
            projectRepo.Object,
            taskRepo.Object,
            memberRepo.Object,
            currentUser.Object,
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
