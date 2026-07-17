using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;
using Qaly.Infrastructure.Services.AI;
using Qaly.Infrastructure.Services.AI.Providers;

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
    public async Task ExecuteAsync_SensitiveRequestWithoutConsent_ReturnsExplicitComplianceErrorAndAuditEvent()
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

        response.IsSuccess.Should().BeFalse();
        response.IsMock.Should().BeFalse();
        response.ErrorCode.Should().Be(AiErrorCodes.SensitiveBlocked);

        var audit = await _context.AiAuditEvents.SingleAsync();
        audit.TenantId.Should().Be(tenantId);
        audit.ProjectId.Should().Be(projectId);
        audit.ActorUserId.Should().Be(userId);
        audit.EventType.Should().Be("AI_PROCESSING_BLOCKED");
        audit.Outcome.Should().Be("blocked");
        audit.FailureCode.Should().Be(PrivacyErrorCodes.CloudBlocked);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDailyBudgetExceeded_ReturnsExplicitBudgetError()
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

        response.IsSuccess.Should().BeFalse();
        response.IsMock.Should().BeFalse();
        response.ErrorCode.Should().Be(AiErrorCodes.BudgetExceeded);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderFailsWithoutDegradedMockPolicy_ReturnsProviderUnavailable()
    {
        var mockProvider = CreateMockProvider("Ollama");
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unavailable"));

        var gateway = CreateGatewayWithProvider(mockProvider);

        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "project_analytics_chat",
            SystemPrompt = "system",
            Prompt = "phan tich tien do",
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = false
        });

        response.IsSuccess.Should().BeFalse();
        response.IsMock.Should().BeFalse();
        response.ErrorCode.Should().Be(AiErrorCodes.ProviderUnavailable);
        response.Retryable.Should().BeTrue();
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
    public async Task SaveChangesAsync_WhenConfidenceIsUnderPointSix_KeepsPendingReviewAndAddsWarning()
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

        draft.Status.Should().Be(AiDraftStatuses.PendingReview);
        draft.WarningsJson.Should().Contain("low_confidence");
        draft.SchemaId.Should().Be("task_draft.v3.2");
        draft.Confidence.Should().Be(0.55m);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTimeoutAndRetry_AppliesConfiguration()
    {
        var mockProvider = CreateMockProvider("Ollama");
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var gateway = CreateGatewayWithProvider(mockProvider, new Dictionary<string, string?>
        {
            {"AiSettings:TimeoutSeconds", "1"},
            {"AiSettings:MaxRetries", "3"},
            {"AiSettings:AllowProviderDegradedMock", "true"}
        });

        var request = new AiRequest
        {
            JobType = "test_timeout",
            Prompt = "test",
            UseCache = false,
            AllowMockFallback = true
        };

        var response = await gateway.ExecuteAsync(request);

        mockProvider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("ProviderDegradedMock");
    }

    [Fact]
    public void PruneChatHistory_WithLongHistory_TruncatesOldestMessages()
    {
        // Arrange: 100 messages, each ~150 chars → ~15,000 chars total (over default 12,000 limit)
        var history = new List<AiChatMessageDto>();
        for (int i = 0; i < 100; i++)
        {
            history.Add(new AiChatMessageDto("user",
                "This is a very long chat message that will be pruned because it exceeds the maximum character limit. We want to make sure it gets truncated."));
        }

        // Act
        var pruned = ErumiChatService.PruneChatHistory(history);

        // Assert: should have fewer messages and total chars within limit
        pruned.Should().NotBeNull();
        pruned!.Count.Should().BeLessThan(100);
        pruned.Sum(m => m.Content?.Length ?? 0).Should().BeLessThanOrEqualTo(12000);
    }

    [Fact]
    public void PruneChatHistory_WithShortHistory_ReturnsAllMessages()
    {
        var history = new List<AiChatMessageDto>
        {
            new("user", "Hello"),
            new("assistant", "Hi there!")
        };

        var pruned = ErumiChatService.PruneChatHistory(history);

        pruned.Should().NotBeNull();
        pruned!.Count.Should().Be(2);
    }

    [Fact]
    public void PruneChatHistory_WithNull_ReturnsNull()
    {
        var result = ErumiChatService.PruneChatHistory(null);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a default AiGateway with an Ollama mock provider that returns empty JSON.
    /// Used by tests that only exercise compliance/budget/cache logic.
    /// </summary>
    private AiGateway CreateGateway()
    {
        var mockProvider = CreateMockProvider("Ollama");
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse { Content = "{}", ProviderName = "Ollama" });
        return CreateGatewayWithProvider(mockProvider);
    }

    /// <summary>
    /// Creates an AiGateway with a specific mock provider and optional extra config overrides.
    /// </summary>
    private AiGateway CreateGatewayWithProvider(
        Mock<IAiProvider> mockProvider,
        Dictionary<string, string?>? extraConfig = null)
    {
        var settings = new Dictionary<string, string?>
        {
            {"AiSettings:Provider", mockProvider.Object.ProviderName}
        };
        if (extraConfig != null)
        {
            foreach (var kvp in extraConfig)
                settings[kvp.Key] = kvp.Value;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var factory = new AiProviderFactory(new List<IAiProvider> { mockProvider.Object });

        return new AiGateway(
            new AiCostService(_context),
            new AiComplianceService(_context),
            _context,
            NullLogger<AiGateway>.Instance,
            config,
            factory,
            new AiOutputValidator()
        );
    }

    /// <summary>
    /// Creates a mock IAiProvider with the given provider name. Caller must set up CompleteAsync behavior.
    /// </summary>
    private static Mock<IAiProvider> CreateMockProvider(string providerName)
    {
        var mock = new Mock<IAiProvider>();
        mock.Setup(p => p.ProviderName).Returns(providerName);
        return mock;
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
