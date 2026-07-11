using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;
using Qaly.Infrastructure.Services.AI;
using Qaly.Infrastructure.Services.AI.Providers;
using Qaly.Application.DTOs.Ai;

namespace Qaly.UnitTests;

public class AiGatewayEvidenceTests : IDisposable
{
    private readonly QalyDbContext _context;

    public AiGatewayEvidenceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_SensitiveRequestWithoutConsent_ReturnsComplianceMockAndAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gateway = CreateGateway();

        var response = await gateway.ExecuteAsync(new AiRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            JobType = "rag_search",
            SystemPrompt = "system",
            Prompt = "contains sensitive transcript",
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = true
        });

        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("ComplianceMock");

        var audit = await _context.AiAuditEvents.SingleAsync();
        audit.TenantId.Should().Be(tenantId);
        audit.ProjectId.Should().Be(projectId);
        audit.ActorUserId.Should().Be(userId);
        audit.EventType.Should().Be("AI_BLOCKED");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDailyBudgetExceeded_ReturnsBudgetMock()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = tenantId,
            ProjectId = projectId,
            DailyBudgetUsd = 0.01m,
            HardStopEnabled = true
        });
        _context.AiUsageLedger.Add(new AiUsageLedger
        {
            TenantId = tenantId,
            ProjectId = projectId,
            JobType = "rag_search",
            ProviderName = "OpenAI",
            ModelName = "gpt-test",
            EstimatedCostUsd = 0.01m,
            Status = "success"
        });
        await _context.SaveChangesAsync();

        var response = await CreateGateway().ExecuteAsync(new AiRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            JobType = "rag_search",
            SystemPrompt = "system",
            Prompt = "next request",
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = false
        });

        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("BudgetMock");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderFailsForTextAnswer_ReturnsUiSafeTextAnswerFallback()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"AiSettings:Provider", "Ollama"}
        }).Build();

        var mockOllamaProvider = new Mock<IAiProvider>();
        mockOllamaProvider.Setup(p => p.ProviderName).Returns("Ollama");
        mockOllamaProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unavailable"));

        var providerFactory = new AiProviderFactory(new List<IAiProvider> { mockOllamaProvider.Object });

        var gateway = new AiGateway(
            new AiCostService(_context),
            new AiComplianceService(_context),
            _context,
            NullLogger<AiGateway>.Instance,
            config,
            providerFactory,
            new AiOutputValidator()
        );

        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "project_analytics_chat",
            SystemPrompt = "system",
            Prompt = "phan tich tien do",
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = false
        });

        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("FallbackMock");

        using var document = JsonDocument.Parse(response.Content);
        var root = document.RootElement;
        root.GetProperty("reply").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("metrics").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("tables").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("charts").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("actions").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("files").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task ExecuteAsync_GoldenDatasetCacheHit_ReturnsCachedResponseAndRecordsCostLedger()
    {
        var fixture = LoadGoldenDataset();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new AiRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            JobType = "meeting_import",
            SystemPrompt = fixture.SystemPrompt,
            Prompt = fixture.Prompt,
            ExpectedSchemaId = fixture.SchemaId,
            UseCache = true
        };

        _context.AiPromptCache.Add(new AiPromptCache
        {
            TenantId = tenantId,
            ProjectId = projectId,
            CacheKey = fixture.Id,
            JobType = request.JobType,
            SchemaId = fixture.SchemaId,
            ProviderName = fixture.ProviderName,
            ModelName = fixture.ModelName,
            RequestHash = ComputeHash(request),
            ResponseJson = fixture.ResponseJson,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _context.SaveChangesAsync();

        var response = await CreateGateway().ExecuteAsync(request);

        response.CacheHit.Should().BeTrue();
        response.Content.Should().Contain("Prepare demo script");
        response.ProviderName.Should().Be("GoldenDataset");

        var ledger = await _context.AiUsageLedger.SingleAsync();
        ledger.CacheHit.Should().BeTrue();
        ledger.EstimatedCostUsd.Should().Be(0m);
        ledger.ProviderName.Should().Be("GoldenDataset");
    }

    [Fact]
    public async Task QdrantSearchAsync_WithoutProjectFilter_ThrowsBeforeProviderCall()
    {
        using var service = CreateQdrantService();

        var act = () => service.SearchAsync([0.1f, 0.2f], "qaly", new VectorFilter
        {
            OwnerId = Guid.NewGuid()
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*project filter*");
    }

    [Fact]
    public async Task QdrantSearchAsync_WithoutCurrentUserFilter_ThrowsBeforeProviderCall()
    {
        using var service = CreateQdrantService();

        var act = () => service.SearchAsync([0.1f, 0.2f], "qaly", new VectorFilter
        {
            ProjectId = Guid.NewGuid()
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*current user filter*");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenConfidenceIsUnderPointSix_MarksStatusAsNeedsManualReview()
    {
        var draft = new AiGeneratedDraft
        {
            ProjectId = Guid.NewGuid(),
            AiJobId = Guid.NewGuid(),
            DraftType = "TaskDraft",
            PayloadJson = "{\"confidence\": 0.55, \"schema_id\": \"task_draft.v3.2\"}",
            Status = "Pending"
        };

        _context.AiGeneratedDrafts.Add(draft);
        await _context.SaveChangesAsync();

        draft.Status.Should().Be("needs_manual_review");
        draft.SchemaId.Should().Be("task_draft.v3.2");
        draft.Confidence.Should().Be(0.55m);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTimeoutAndRetry_AppliesConfiguration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"AiSettings:Provider", "Ollama"},
            {"AiSettings:TimeoutSeconds", "1"},
            {"AiSettings:MaxRetries", "3"}
        }).Build();

        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("Ollama");
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var providerFactory = new AiProviderFactory(new List<IAiProvider> { mockProvider.Object });

        var gateway = new AiGateway(
            new AiCostService(_context),
            new AiComplianceService(_context),
            _context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AiGateway>.Instance,
            config,
            providerFactory,
            new AiOutputValidator()
        );

        var request = new AiRequest
        {
            JobType = "test_timeout",
            Prompt = "test",
            UseCache = false
        };

        var response = await gateway.ExecuteAsync(request);

        mockProvider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("FallbackMock");
    }

    [Fact]
    public async Task ChatFastAsync_WithLongHistory_PrunesHistoryBeforeCallingGateway()
    {
        var gatewayMock = new Mock<IAiGateway>();
        gatewayMock.Setup(g => g.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse { Content = "{}", ProviderName = "Mock" });

        var analyticsMock = new Mock<IAnalyticsService>();
        analyticsMock.Setup(x => x.GetWorkspaceAnalyticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new Qaly.Application.DTOs.Analytics.WorkspaceAnalyticsDto(1, 1, 1, 1, 1.0)));

        var projectMock = new Mock<IProjectService>();
        var taskMock = new Mock<ITaskService>();
        var memberRepoMock = new Mock<IRepository<ProjectMember>>();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var chatService = new ErumiChatService(
            analyticsMock.Object,
            projectMock.Object,
            taskMock.Object,
            memberRepoMock.Object,
            currentUserServiceMock.Object,
            gatewayMock.Object
        );

        var history = new List<AiChatMessageDto>();
        for (int i = 0; i < 100; i++)
        {
            history.Add(new AiChatMessageDto("user", "This is a very long chat message that will be pruned because it exceeds the maximum character limit. We want to make sure it gets truncated."));
        }

        var request = new ErumiChatRequestDto(
            Message: "analyze progress",
            ProjectId: null,
            Mode: "erumi",
            History: history
        );

        await chatService.ChatFastAsync(request);

        gatewayMock.Verify(g => g.ExecuteAsync(It.Is<AiRequest>(r => r.History != null && r.History.Count < 100), It.IsAny<CancellationToken>()));
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private AiGateway CreateGateway()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"AiSettings:Provider", "Ollama"}
        }).Build();

        var mockOllamaProvider = new Mock<IAiProvider>();
        mockOllamaProvider.Setup(p => p.ProviderName).Returns("Ollama");
        mockOllamaProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse { Content = "{}", ProviderName = "Ollama" });

        var providerFactory = new AiProviderFactory(new List<IAiProvider> { mockOllamaProvider.Object });

        return new AiGateway(
            new AiCostService(_context),
            new AiComplianceService(_context),
            _context,
            NullLogger<AiGateway>.Instance,
            config,
            providerFactory,
            new AiOutputValidator()
        );
    }

    private static QdrantVectorStorageService CreateQdrantService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:QdrantUrl"] = "http://localhost:6333"
            })
            .Build();

        return new QdrantVectorStorageService(configuration);
    }

    private static GoldenDataset LoadGoldenDataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-golden-dataset.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GoldenDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static string ComputeHash(AiRequest request)
    {
        var rawData = request.SystemPrompt + "|" + request.Prompt + "|" + request.ExpectedSchemaId;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var item in bytes)
        {
            builder.Append(item.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private sealed record GoldenDataset(
        string Id,
        string SchemaId,
        string ProviderName,
        string ModelName,
        string SystemPrompt,
        string Prompt,
        string ResponseJson);
}
