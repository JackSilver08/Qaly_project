#pragma warning disable CA1822 // Mark members as static

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    public async Task ExecuteAsync_WhenSchemaValidationFails_RetriesAndEventuallyFallsBack()
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

        // Should fallback to Mock response
        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("FallbackMock");
        response.Content.Should().Contain("dang tam thoi khong phan hoi");
        
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
