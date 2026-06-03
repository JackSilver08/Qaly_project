using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public class AiGateway : IAiGateway
{
    private readonly IChatClient _chatClient;
    private readonly IAiCostService _costService;
    private readonly IAiComplianceService _complianceService;
    private readonly QalyDbContext _context;
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

    private readonly ILogger<AiGateway> _logger;

    public AiGateway(
        IChatClient chatClient, 
        IAiCostService costService, 
        IAiComplianceService complianceService, 
        QalyDbContext context,
        ILogger<AiGateway> logger)
    {
        _chatClient = chatClient;
        _costService = costService;
        _complianceService = complianceService;
        _context = context;
        _logger = logger;
    }

    public async Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        // 1. Compliance Check
        bool canProcessInCloud = await _complianceService.CanProcessInCloudAsync(
            request.TenantId, request.ProjectId, request.UserId, request.IsSensitive, cancellationToken);

        if (!canProcessInCloud)
        {
            _aiRequestBlockedComplianceLogger(_logger, null);
            await _complianceService.LogAuditEventAsync(request.TenantId, request.ProjectId, request.UserId, 
                "AI_BLOCKED", "AiRequest", null, null, "Blocked due to sensitive data", cancellationToken);
            
            return CreateMockResponse("Request blocked due to privacy settings.", "ComplianceMock");
        }

        // 2. Budget Check
        bool hasBudget = await _costService.EnsureBudgetAvailableAsync(request.TenantId, request.ProjectId, cancellationToken);
        if (!hasBudget)
        {
            _aiRequestBlockedBudgetLogger(_logger, null);
            return CreateMockResponse("Budget exceeded. Please upgrade your plan.", "BudgetMock");
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
                    request.TenantId, request.ProjectId, request.UserId, request.JobType,
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

        // 4. Execute AI Request
        try
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

            var options = new ChatOptions
            {
                MaxOutputTokens = 1500, // Should be configurable
                Temperature = 0.3f
            };

            // Using Microsoft.Extensions.AI abstraction
            var response = await _chatClient.CompleteAsync(chatMessages, options, cancellationToken);
            var content = response.Message.Text ?? string.Empty;
            
            // Assume input = prompt length / 4, output = content length / 4 as naive estimation if token usage isn't available
            int inputTokens = response.Usage?.InputTokenCount ?? (request.Prompt.Length + request.SystemPrompt.Length) / 4;
            int outputTokens = response.Usage?.OutputTokenCount ?? content.Length / 4;
            decimal cost = CalculateCost(inputTokens, outputTokens);
            
            var providerMetadata = _chatClient.Metadata;
            string providerName = providerMetadata?.ProviderName ?? "Unknown";
            string modelName = providerMetadata?.ModelId ?? "Unknown";

            // Save to Cache
            if (request.UseCache)
            {
                var newCache = new AiPromptCache
                {
                    TenantId = request.TenantId,
                    ProjectId = request.ProjectId,
                    CacheKey = Guid.NewGuid().ToString("N"),
                    JobType = request.JobType,
                    SchemaId = request.ExpectedSchemaId,
                    ProviderName = providerName,
                    ModelName = modelName,
                    RequestHash = requestHash,
                    ResponseJson = content,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
                };
                _context.AiPromptCache.Add(newCache);
            }

            // Log Usage
            await _costService.RecordUsageAsync(
                request.TenantId, request.ProjectId, request.UserId, request.JobType,
                providerName, modelName, inputTokens, outputTokens, cost, 
                (int)sw.ElapsedMilliseconds, "success", false, cancellationToken);

            return new AiResponse
            {
                Content = content,
                ProviderName = providerName,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                EstimatedCostUsd = cost,
                IsMock = false,
                CacheHit = false
            };
        }
        catch (Exception ex)
        {
            _errorCallingAiProviderLogger(_logger, ex);
            
            // Log failed usage
            await _costService.RecordUsageAsync(
                request.TenantId, request.ProjectId, request.UserId, request.JobType,
                "Unknown", "Unknown", 0, 0, 0m, (int)sw.ElapsedMilliseconds, "failed", false, cancellationToken);

            // Fallback Mock for Demo Reliability
            return CreateMockResponse(GetFallbackResponse(request.ExpectedSchemaId), "FallbackMock");
        }
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
        // Provide golden dataset fallback based on expected schema for demo reliability
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

    private static decimal CalculateCost(int inputTokens, int outputTokens)
    {
        // Simple mock cost calculation. Should load from AiProviderConfig.
        return (inputTokens * 0.0001m) + (outputTokens * 0.0002m);
    }
}
