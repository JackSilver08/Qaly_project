#pragma warning disable CA1848 // LoggerMessage delegates
#pragma warning disable CA1305 // Formatted string behavior

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI.Providers;

namespace Qaly.Infrastructure.Services.AI;

public class AiGateway : IAiGateway
{
    private readonly IAiCostService _costService;
    private readonly IAiComplianceService _complianceService;
    private readonly QalyDbContext _context;
    private readonly ILogger<AiGateway> _logger;
    private readonly IConfiguration _configuration;
    private readonly IVectorStorageService? _vectorStorage;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private readonly AiProviderFactory _providerFactory;
    private readonly AiOutputValidator _outputValidator;

    private static readonly Action<ILogger, Exception?> _aiRequestBlockedComplianceLogger = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1001, "AiRequestBlockedCompliance"),
        "AI Request blocked due to compliance (sensitive data without cloud processing consent).");

    private static readonly Action<ILogger, Exception?> _aiRequestBlockedBudgetLogger = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1002, "AiRequestBlockedBudget"),
        "AI Request blocked due to budget limits.");

    private static readonly Action<ILogger, Exception?> _errorCallingAiProviderLogger = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1003, "ErrorCallingAiProvider"),
        "Error calling AI Provider.");

    // Primary constructor for dependency injection
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public AiGateway(
        IAiCostService costService,
        IAiComplianceService complianceService,
        QalyDbContext context,
        ILogger<AiGateway> logger,
        IConfiguration configuration,
        AiProviderFactory providerFactory,
        AiOutputValidator outputValidator,
        IVectorStorageService? vectorStorage = null,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null)
    {
        _costService = costService;
        _complianceService = complianceService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _providerFactory = providerFactory;
        _outputValidator = outputValidator;
        _vectorStorage = vectorStorage;
        _embeddingGenerator = embeddingGenerator;
    }

    // Backwards-compatible constructor for testing
    public AiGateway(
        IChatClient chatClient, 
        IAiCostService costService, 
        IAiComplianceService complianceService, 
        QalyDbContext context,
        ILogger<AiGateway> logger)
        : this(
              costService, 
              complianceService, 
              context, 
              logger, 
              CreateMockConfiguration(), 
              CreateMockProviderFactory(chatClient), 
              new AiOutputValidator(), 
              null, 
              null)
    {
    }

    public async Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        // 1. Compliance Check
        bool canProcessInCloud = await _complianceService.CanProcessInCloudAsync(
            request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.IsSensitive, cancellationToken);

        if (!canProcessInCloud)
        {
            _aiRequestBlockedComplianceLogger(_logger, null);
            await _complianceService.LogAuditEventAsync(request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, 
                "AI_BLOCKED", "AiRequest", null, null, "Blocked due to sensitive data", cancellationToken);
            
            return CreateMockResponse(GetFallbackResponse(request.ExpectedSchemaId), "ComplianceMock");
        }

        // 2. Budget Check
        bool hasBudget = await _costService.EnsureBudgetAvailableAsync(request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, cancellationToken);
        if (!hasBudget)
        {
            _aiRequestBlockedBudgetLogger(_logger, null);
            return CreateMockResponse(GetFallbackResponse(request.ExpectedSchemaId), "BudgetMock");
        }

        // Proactive RAG (Context Retrieval)
        if (request.ProjectId.HasValue && request.UserId.HasValue && _vectorStorage != null && _embeddingGenerator != null)
        {
            try
            {
                var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { request.Prompt }, null, cancellationToken);
                var vector = queryEmbedding[0].Vector.ToArray();

                var filter = new VectorFilter
                {
                    ProjectId = request.ProjectId.Value,
                    OwnerId = request.UserId.Value
                };

                var results = await _vectorStorage.SearchAsync(vector, "qaly_context", filter, limit: 3);
                if (results.Count > 0)
                {
                    var contextBuilder = new StringBuilder();
                    contextBuilder.AppendLine("\n--- THÔNG TIN NGỮ CẢNH DỰ ÁN ĐƯỢC TRÍCH XUẤT (RAG) ---");
                    foreach (var res in results)
                    {
                        contextBuilder.AppendLine($"- [{res.Payload.GetValueOrDefault("ContentType") ?? "Dữ liệu"}]: {res.Payload.GetValueOrDefault("Content")}");
                    }
                    contextBuilder.AppendLine("------------------------------------------------------\n");

                    request.SystemPrompt = request.SystemPrompt + "\n" + contextBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to perform proactive RAG context retrieval in AiGateway.");
            }
        }

        // 3. Cache Check
        var hashInput = new StringBuilder();
        hashInput.Append(request.SystemPrompt).Append('|').Append(request.Prompt).Append('|').Append(request.ExpectedSchemaId);
        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                hashInput.Append('|').Append(msg.Role).Append(':').Append(msg.Content);
            }
        }
        string requestHash = ComputeSha256Hash(hashInput.ToString());
        
        if (request.UseCache)
        {
            var cachedPrompt = await _context.AiPromptCache
                .FirstOrDefaultAsync(c => c.RequestHash == requestHash, cancellationToken);

            if (cachedPrompt != null && (cachedPrompt.ExpiresAt == null || cachedPrompt.ExpiresAt > DateTimeOffset.UtcNow))
            {
                cachedPrompt.HitCount++;
                await _context.SaveChangesAsync(cancellationToken);

                await _costService.RecordUsageAsync(
                    request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.JobType,
                    cachedPrompt.ProviderName ?? "Cache", cachedPrompt.ModelName ?? "Cache",
                    0, 0, 0m, (int)sw.ElapsedMilliseconds, "success", true, cancellationToken);

                return new AiResponse
                {
                    Content = cachedPrompt.ResponseJson,
                    ProviderName = cachedPrompt.ProviderName ?? "Cache",
                    ModelName = cachedPrompt.ModelName ?? "Cache",
                    InputTokens = 0,
                    OutputTokens = 0,
                    EstimatedCostUsd = 0m,
                    IsMock = false,
                    CacheHit = true
                };
            }
        }

        // Load dynamic configuration
        var settings = new AiGatewaySettings();
        _configuration.GetSection(AiGatewaySettings.SectionName).Bind(settings);

        // 4. Execute AI Request with Schema Validation & Retry
        string currentPrompt = request.Prompt;
        int maxRetries = 2;
        int attempt = 0;
        AiResponse? finalResponse = null;
        string? validationError = null;

        while (attempt <= maxRetries)
        {
            if (attempt > 0)
            {
                // Instruct provider to fix schema issues
                request.Prompt = currentPrompt + $"\n\n[Warning]: Your previous response was invalid. It failed validation with error: '{validationError}'. Please return a valid JSON format complying with the expected schema: '{request.ExpectedSchemaId}'. Do not include markdown blocks or any conversational text around the JSON.";
            }

            try
            {
                var provider = _providerFactory.GetProvider(settings.Provider);
                AiProviderSetting providerConfig = settings.Provider.ToLowerInvariant() switch
                {
                    "openai" => settings.OpenAI,
                    "gemini" => settings.Gemini,
                    _ => settings.Ollama
                };

                finalResponse = await provider.CompleteAsync(request, providerConfig, cancellationToken);
                
                // Validate output schema if requested
                if (string.IsNullOrWhiteSpace(request.ExpectedSchemaId) || _outputValidator.Validate(finalResponse.Content, request.ExpectedSchemaId, out validationError))
                {
                    // Validation success or schema validation not required
                    break;
                }

                _logger.LogWarning("AI output validation failed for schema {SchemaId} on attempt {Attempt}. Error: {ValidationError}", 
                    request.ExpectedSchemaId, attempt + 1, validationError);
            }
            catch (Exception ex)
            {
                _errorCallingAiProviderLogger(_logger, ex);
                validationError = ex.Message;
            }

            attempt++;
        }

        if (finalResponse != null && (string.IsNullOrWhiteSpace(request.ExpectedSchemaId) || validationError == null))
        {
            // Save to Cache
            if (request.UseCache)
            {
                var newCache = new AiPromptCache
                {
                    TenantId = request.TenantId ?? Guid.Empty,
                    ProjectId = request.ProjectId ?? Guid.Empty,
                    CacheKey = Guid.NewGuid().ToString("N"),
                    JobType = request.JobType,
                    SchemaId = request.ExpectedSchemaId,
                    ProviderName = finalResponse.ProviderName,
                    ModelName = finalResponse.ModelName,
                    RequestHash = requestHash,
                    ResponseJson = finalResponse.Content,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
                };
                _context.AiPromptCache.Add(newCache);
            }

            // Log Usage
            await _costService.RecordUsageAsync(
                request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.JobType,
                finalResponse.ProviderName, finalResponse.ModelName, finalResponse.InputTokens, finalResponse.OutputTokens, finalResponse.EstimatedCostUsd, 
                (int)sw.ElapsedMilliseconds, "success", false, cancellationToken);

            return finalResponse;
        }

        // Log failed usage
        await _costService.RecordUsageAsync(
            request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.JobType,
            settings.Provider, "Unknown", 0, 0, 0m, (int)sw.ElapsedMilliseconds, "failed", false, cancellationToken);

        // Fallback Mock for Demo Reliability
        return CreateMockResponse(GetFallbackResponse(request.ExpectedSchemaId), "FallbackMock");
    }

    private static AiResponse CreateMockResponse(string content, string provider)
    {
        return new AiResponse
        {
            Content = content,
            ProviderName = provider,
            ModelName = "Mock-1.0",
            InputTokens = 0,
            OutputTokens = 0,
            EstimatedCostUsd = 0m,
            IsMock = true,
            CacheHit = false
        };
    }

    private static string GetFallbackResponse(string schemaId)
    {
        if (schemaId.Contains("MeetingActionItem", StringComparison.OrdinalIgnoreCase))
        {
            return "[\"Fix login issue\", \"Update documentation\"]";
        }

        if (schemaId.Contains("TextAnswer", StringComparison.OrdinalIgnoreCase))
        {
            return """
                {
                  "reply": "AI provider dang tam thoi khong phan hoi. Day la cau tra loi fallback de UI khong bi vo; hay thu lai sau hoac kiem tra cau hinh provider.",
                  "metrics": [],
                  "tables": [],
                  "charts": [],
                  "actions": [
                    { "type": "suggested_action", "label": "Thu lai cau hoi sau" }
                  ],
                  "files": []
                }
                """;
        }

        return "{\"result\": \"Mock response due to AI failure.\"}";
    }

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    private static IConfiguration CreateMockConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?> {
            {"AiSettings:Provider", "Ollama"}
        };
        return new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
    }

    private static AiProviderFactory CreateMockProviderFactory(IChatClient chatClient)
    {
        var mockOllamaProvider = new MockChatClientProvider(chatClient);
        return new AiProviderFactory(new List<IAiProvider> { mockOllamaProvider });
    }

    private sealed class MockChatClientProvider : IAiProvider
    {
        private readonly IChatClient _chatClient;

        public MockChatClientProvider(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public string ProviderName => "Ollama";

        public async Task<AiResponse> CompleteAsync(AiRequest request, AiProviderSetting config, CancellationToken cancellationToken = default)
        {
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, request.SystemPrompt)
            };

            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                        ? ChatRole.Assistant : ChatRole.User;
                    chatMessages.Add(new ChatMessage(role, msg.Content));
                }
            }

            chatMessages.Add(new ChatMessage(ChatRole.User, request.Prompt));

            var response = await _chatClient.CompleteAsync(chatMessages, cancellationToken: cancellationToken);
            var content = response.Message.Text ?? string.Empty;

            int inputTokens = response.Usage?.InputTokenCount ?? (request.Prompt.Length + request.SystemPrompt.Length) / 4;
            int outputTokens = response.Usage?.OutputTokenCount ?? content.Length / 4;

            return new AiResponse
            {
                Content = content,
                ProviderName = ProviderName,
                ModelName = "mock-model",
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                EstimatedCostUsd = 0m,
                IsMock = false,
                CacheHit = false
            };
        }
    }
}
