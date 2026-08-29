#pragma warning disable CA1822 // Mark members as static

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;
using Qaly.Infrastructure.Services.AI.Providers;

namespace Qaly.UnitTests;

public class AiGatewayRouterTests : IDisposable
{
    private readonly QalyDbContext _context;

    public AiGatewayRouterTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public void AiOutputValidator_WithValidTextAnswer_ReturnsTrue()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "reply": "All good!",
              "metrics": [],
              "tables": [],
              "charts": [],
              "actions": [],
              "files": []
            }
            """;

        var result = validator.Validate(json, "TextAnswer.v1", out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void AiOutputValidator_WithConcreteWorkspaceStrategy_ReturnsTrue()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "summary": "Có 14 nhiệm vụ quá hạn trong 6 dự án đang hoạt động.",
              "riskAnalysis": ["5 dự án cần được kiểm tra nguyên nhân chậm tiến độ."],
              "recommendations": ["Mở danh sách nhiệm vụ quá hạn và xác nhận người phụ trách."],
              "priorityPlan": ["Xử lý nhóm 14 nhiệm vụ quá hạn trước khi nhận thêm việc mới."]
            }
            """;

        var result = validator.Validate(json, "WorkspaceStrategy.v1", out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void AiOutputValidator_WithWorkspacePlaceholder_ReturnsFalse()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "summary": "nhận định ngắn",
              "riskAnalysis": ["rủi ro có căn cứ từ dữ liệu"],
              "recommendations": ["hành động cụ thể người dùng có thể làm"],
              "priorityPlan": ["tối đa 3 ưu tiên có thể thực hiện"]
            }
            """;

        var result = validator.Validate(json, "WorkspaceStrategy.v1", out var error);

        result.Should().BeFalse();
        error.Should().Contain("placeholder");
    }

    [Fact]
    public void AiOutputValidator_WithOnlyOneWorkspaceMetric_ReturnsFalse()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "summary": "Có 14 nhiệm vụ đang cần xử lý.",
              "riskAnalysis": ["Nhiệm vụ quá hạn có thể làm chậm tiến độ dự án."],
              "recommendations": ["Người dùng cần kiểm tra người phụ trách."],
              "priorityPlan": ["Ưu tiên xử lý nhiệm vụ quá hạn trước."]
            }
            """;

        var result = validator.Validate(json, "WorkspaceStrategy.v1", out var error);

        result.Should().BeFalse();
        error.Should().Contain("two supplied numeric metrics");
    }

    [Fact]
    public void AiOutputValidator_WithValidChatSummary_ReturnsTrue()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "room_id": "room-123",
              "message_range": { "from": 1, "to": 10 },
              "summary": "Tóm tắt thảo luận",
              "key_points": ["Point 1"],
              "open_questions": [],
              "action_candidates": []
            }
            """;

        var result = validator.Validate(json, "chat_summary.v3.2", out var error);
        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void AiOutputValidator_WithValidTaskDraft_ReturnsTrue()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "title": "Task Title",
              "description": "Task description details",
              "priority": "medium",
              "deadline": null,
              "assignee_suggestion": "user-1",
              "source_refs": [
                { "source_type": "chat", "source_id": "msg-123" }
              ],
              "confidence": 0.95
            }
            """;

        var result = validator.Validate(json, "task_draft.v3.2", out var error);
        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void AiOutputValidator_WithInvalidTaskBreakdown_ReturnsFalse()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "parent_task": "Parent",
              "subtasks": []
            }
            """;

        var result = validator.Validate(json, "task_breakdown.v3.2", out var error);
        result.Should().BeFalse();
        error.Should().Contain("subtasks");
    }

    [Fact]
    public void AiOutputValidator_WithMissingReplyTextAnswer_ReturnsFalse()
    {
        var validator = new AiOutputValidator();
        var json = """
            {
              "metrics": [],
              "tables": [],
              "charts": [],
              "actions": [],
              "files": []
            }
            """;

        var result = validator.Validate(json, "TextAnswer.v1", out var error);

        result.Should().BeFalse();
        error.Should().Contain("HasReply");
    }

    [Fact]
    public void AiOutputValidator_WithInvalidJson_ReturnsFalse()
    {
        var validator = new AiOutputValidator();
        var json = "invalid-json";

        var result = validator.Validate(json, "TextAnswer.v1", out var error);

        result.Should().BeFalse();
        error.Should().Contain("JSON");
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderFactoryRouting_SelectsCorrectProvider()
    {
        var config = CreateConfiguration("OpenAI");
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.SetupGet(p => p.ProviderName).Returns("OpenAI");
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "OpenAI response",
                ProviderName = "OpenAI",
                ModelName = "gpt-4o"
            });

        var factory = new AiProviderFactory(new[] { mockProvider.Object });

        var gateway = CreateGateway(config, factory);

        var request = new AiRequest
        {
            JobType = "test",
            SystemPrompt = "system",
            Prompt = "prompt",
            UseCache = false
        };

        var response = await gateway.ExecuteAsync(request);

        // Verify it routed correctly to OpenAI
        response.Content.Should().Be("OpenAI response");
        response.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public async Task ExecuteAsync_WithDeepSeekHint_SelectsDeepSeekProvider()
    {
        var config = CreateConfiguration("Ollama");
        var deepSeek = new Mock<IAiProvider>();
        deepSeek.SetupGet(provider => provider.ProviderName).Returns("DeepSeek");
        deepSeek.Setup(provider => provider.CompleteAsync(
                It.IsAny<AiRequest>(),
                It.IsAny<AiProviderSetting>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "DeepSeek response",
                ProviderName = "DeepSeek",
                ModelName = "deepseek-v4-pro"
            });

        var gateway = CreateGateway(config, new AiProviderFactory([deepSeek.Object]));
        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "test",
            ProviderHint = "deepseek",
            SystemPrompt = "system",
            Prompt = "prompt",
            UseCache = false
        });

        response.IsSuccess.Should().BeTrue();
        response.ProviderName.Should().Be("DeepSeek");
        response.ModelName.Should().Be("deepseek-v4-pro");
    }

    [Theory]
    [InlineData("deepseek-v4-pro")]
    [InlineData("deepseek-v4")]
    [InlineData("DeepSeek-V4-Pro")]
    [InlineData("deepseek-reasoner")]
    [InlineData("deepseek-chat")]
    public async Task ExecuteAsync_WithPublishedDeepSeekModelId_SelectsDeepSeekProvider(string providerHint)
    {
        // The v4.0 UI catalogue and API docs publish `deepseek-v4-pro` as the strong model id.
        // An unmapped hint used to fall through to the Ollama provider settings under a strict
        // provider request, silently routing a cloud request at the local model.
        var config = CreateConfiguration("Ollama");
        var deepSeek = new Mock<IAiProvider>();
        deepSeek.SetupGet(provider => provider.ProviderName).Returns("DeepSeek");
        deepSeek.Setup(provider => provider.CompleteAsync(
                It.IsAny<AiRequest>(),
                It.IsAny<AiProviderSetting>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "DeepSeek response",
                ProviderName = "DeepSeek",
                ModelName = "deepseek-v4-pro"
            });

        var gateway = CreateGateway(config, new AiProviderFactory([deepSeek.Object]));
        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "test",
            ProviderHint = providerHint,
            StrictProvider = true,
            SystemPrompt = "system",
            Prompt = "prompt",
            UseCache = false
        });

        response.IsSuccess.Should().BeTrue();
        response.ProviderName.Should().Be("DeepSeek");
    }

    [Fact]
    public async Task ExecuteAsync_WithStrictDeepSeekFailure_DoesNotUseFallbackProvider()
    {
        var config = CreateConfiguration("Ollama");
        var deepSeek = new Mock<IAiProvider>();
        deepSeek.SetupGet(provider => provider.ProviderName).Returns("DeepSeek");
        deepSeek.Setup(provider => provider.CompleteAsync(
                It.IsAny<AiRequest>(),
                It.IsAny<AiProviderSetting>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Insufficient Balance"));
        var ollama = new Mock<IAiProvider>();
        ollama.SetupGet(provider => provider.ProviderName).Returns("Ollama");

        var gateway = CreateGateway(config, new AiProviderFactory([deepSeek.Object, ollama.Object]));
        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "test",
            ProviderHint = "deepseek",
            StrictProvider = true,
            SystemPrompt = "system",
            Prompt = "prompt",
            UseCache = false
        });

        response.IsSuccess.Should().BeFalse();
        response.ErrorMessage.Should().Contain("Insufficient Balance");
        ollama.Verify(provider => provider.CompleteAsync(
            It.IsAny<AiRequest>(),
            It.IsAny<AiProviderSetting>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreferredProviderFails_UsesNextConfiguredProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiSettings:Provider"] = "OpenAI",
            ["AiSettings:FallbackProviders:0"] = "Gemini",
            ["AiSettings:OpenAI:ApiKey"] = "sk-test",
            ["AiSettings:Gemini:ApiKey"] = "gemini-test"
        }).Build();
        var primary = new Mock<IAiProvider>();
        primary.SetupGet(provider => provider.ProviderName).Returns("OpenAI");
        primary.Setup(provider => provider.CompleteAsync(
                It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("primary unavailable"));
        var fallback = new Mock<IAiProvider>();
        fallback.SetupGet(provider => provider.ProviderName).Returns("Gemini");
        fallback.Setup(provider => provider.CompleteAsync(
                It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "fallback response",
                ProviderName = "Gemini",
                ModelName = "gemini-test"
            });

        var gateway = CreateGateway(configuration, new AiProviderFactory([primary.Object, fallback.Object]));
        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "test",
            SystemPrompt = "system",
            Prompt = "prompt",
            UseCache = false
        });

        response.IsSuccess.Should().BeTrue();
        response.ProviderName.Should().Be("Gemini");
        primary.Verify(provider => provider.CompleteAsync(
            It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(provider => provider.CompleteAsync(
            It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllProvidersFailAndMockPolicyIsExplicit_ReturnsLabeledDegradedMock()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiSettings:Provider"] = "Ollama",
            ["AiSettings:FallbackProviders:0"] = "Ollama",
            ["AiSettings:AllowProviderDegradedMock"] = "true"
        }).Build();
        var provider = new Mock<IAiProvider>();
        provider.SetupGet(candidate => candidate.ProviderName).Returns("Ollama");
        provider.Setup(candidate => candidate.CompleteAsync(
                It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("provider unavailable"));

        var gateway = CreateGateway(configuration, new AiProviderFactory([provider.Object]));
        var response = await gateway.ExecuteAsync(new AiRequest
        {
            JobType = "test",
            SystemPrompt = "system",
            Prompt = "prompt",
            ExpectedSchemaId = "TextAnswer.v1",
            UseCache = false,
            AllowMockFallback = true
        });

        response.IsSuccess.Should().BeTrue();
        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("ProviderDegradedMock");
        response.MockReason.Should().Be("provider_degraded");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSchemaValidationFails_RetriesAndReturnsSchemaError()
    {
        var config = CreateConfiguration("Ollama");
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.SetupGet(p => p.ProviderName).Returns("Ollama");
        
        // Return invalid JSON on all attempts
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                Content = "invalid-json",
                ProviderName = "Ollama",
                ModelName = "llama3"
            });

        var factory = new AiProviderFactory(new[] { mockProvider.Object });
        var validator = new AiOutputValidator();

        var gateway = CreateGateway(config, factory, validator);

        var request = new AiRequest
        {
            JobType = "test",
            SystemPrompt = "system",
            Prompt = "prompt",
            ExpectedSchemaId = "TextAnswer.v1",
            UseCache = false
        };

        var response = await gateway.ExecuteAsync(request);

        response.IsSuccess.Should().BeFalse();
        response.IsMock.Should().BeFalse();
        response.ErrorCode.Should().Be(AiErrorCodes.SchemaInvalid);
        
        // Verify CompleteAsync was called 3 times (1 initial + 2 retries)
        mockProvider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSchemaValidationFailsFirstAttemptButSucceedsOnSecondAttempt_DoesNotRetryAgain()
    {
        var config = CreateConfiguration("Ollama");
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.SetupGet(p => p.ProviderName).Returns("Ollama");

        var attemptCount = 0;
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<AiProviderSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    return new AiResponse
                    {
                        Content = "invalid-json",
                        ProviderName = "Ollama",
                        ModelName = "llama3"
                    };
                }
                return new AiResponse
                {
                    Content = """{"reply":"Success","metrics":[],"tables":[],"charts":[],"actions":[],"files":[]}""",
                    ProviderName = "Ollama",
                    ModelName = "llama3"
                };
            });

        var factory = new AiProviderFactory(new[] { mockProvider.Object });
        var validator = new AiOutputValidator();

        var gateway = CreateGateway(config, factory, validator);

        var request = new AiRequest
        {
            JobType = "test",
            SystemPrompt = "system",
            Prompt = "prompt",
            ExpectedSchemaId = "TextAnswer.v1",
            UseCache = false
        };

        var response = await gateway.ExecuteAsync(request);

        response.IsMock.Should().BeFalse();
        response.Content.Should().Contain("Success");
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_OpenAIProvider_WithDummyKey_ThrowsInvalidOperationException()
    {
        var provider = new OpenAIProvider(Mock.Of<IHttpClientFactory>());
        var request = new AiRequest { Prompt = "hi" };
        var config = new AiProviderSetting { ApiKey = "YOUR_OPENAI_KEY" };

        var act = () => provider.CompleteAsync(request, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task CompleteAsync_DeepSeekProvider_WithDummyKey_ThrowsInvalidOperationException()
    {
        var provider = new DeepSeekProvider(Mock.Of<IHttpClientFactory>());
        var request = new AiRequest { Prompt = "hi" };
        var config = new AiProviderSetting { ApiKey = "YOUR_DEEPSEEK_KEY" };

        var act = () => provider.CompleteAsync(request, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task CompleteAsync_DeepSeekProvider_WithPlaceholder_UsesEnvironmentKey()
    {
        const string variableName = "DEEPSEEK_API_KEY";
        var previousValue = Environment.GetEnvironmentVariable(variableName);
        HttpRequestMessage? capturedRequest = null;
        try
        {
            Environment.SetEnvironmentVariable(variableName, "deepseek-test-key");
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"choices":[{"message":{"content":"ok"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""")
                });
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(item => item.CreateClient("AiDeepSeek"))
                .Returns(new HttpClient(handler.Object));
            var provider = new DeepSeekProvider(factory.Object);

            var response = await provider.CompleteAsync(
                new AiRequest { Prompt = "hi" },
                new AiProviderSetting
                {
                    ApiKey = "YOUR_DEEPSEEK_KEY",
                    BaseUrl = "https://api.deepseek.test",
                    Model = "deepseek-test-model"
                });

            response.Content.Should().Be("ok");
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Headers.Authorization!.Parameter.Should().Be("deepseek-test-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
        }
    }

    [Fact]
    public async Task CompleteAsync_GeminiProvider_WithDummyKey_ThrowsInvalidOperationException()
    {
        var provider = new GeminiProvider(Mock.Of<IHttpClientFactory>());
        var request = new AiRequest { Prompt = "hi" };
        var config = new AiProviderSetting { ApiKey = "YOUR_GEMINI_KEY" };

        var act = () => provider.CompleteAsync(request, config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private IConfiguration CreateConfiguration(string providerName)
    {
        var settings = new Dictionary<string, string?>
        {
            { "AiSettings:Provider", providerName },
            { "AiSettings:Ollama:Model", "llama3" },
            { "AiSettings:DeepSeek:ApiKey", "sk-deepseek-test" },
            { "AiSettings:DeepSeek:Model", "deepseek-v4-pro" },
            { "AiSettings:OpenAI:ApiKey", "sk-test" },
            { "AiSettings:Gemini:ApiKey", "gemini-test" }
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private AiGateway CreateGateway(IConfiguration configuration, AiProviderFactory factory, AiOutputValidator? validator = null)
    {
        var costMock = new Mock<IAiCostService>();
        costMock.Setup(c => c.EnsureBudgetAvailableAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var complianceMock = new Mock<IAiComplianceService>();
        complianceMock.Setup(c => c.CanProcessInCloudAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new AiGateway(
            costMock.Object,
            complianceMock.Object,
            _context,
            NullLogger<AiGateway>.Instance,
            configuration,
            factory,
            validator ?? new AiOutputValidator(),
            null,
            null);
    }
}
