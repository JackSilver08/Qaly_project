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
using Qaly.Domain.Interfaces;
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
    private readonly AiTools? _aiTools;
    private readonly ToolParameterGuard? _parameterGuard;
    private readonly IRepository<AiJob>? _aiJobRepo;
    private readonly IRepository<AiGeneratedDraft>? _aiDraftRepo;
    private readonly IUnitOfWork? _unitOfWork;

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
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        AiTools? aiTools = null,
        ToolParameterGuard? parameterGuard = null,
        IRepository<AiJob>? aiJobRepo = null,
        IRepository<AiGeneratedDraft>? aiDraftRepo = null,
        IUnitOfWork? unitOfWork = null)
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
        _aiTools = aiTools;
        _parameterGuard = parameterGuard;
        _aiJobRepo = aiJobRepo;
        _aiDraftRepo = aiDraftRepo;
        _unitOfWork = unitOfWork;
    }



    public async Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Load dynamic configuration
        var settings = new AiGatewaySettings();
        _configuration.GetSection(AiGatewaySettings.SectionName).Bind(settings);

        // Offline / Demo Fallback Mode
        if (settings.OfflineMode)
        {
            _logger.LogWarning("AI Gateway is in OfflineMode/DemoMode. Returning offline fallback mock.");
            return CreateMockResponse(GetFallbackResponse(request.ExpectedSchemaId), "OfflineMock");
        }
        
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

        if (request.Tools != null && request.Tools.Count > 0 && !request.SystemPrompt.Contains("--- BẠN CÓ QUYỀN TRUY CẬP VÀO CÁC CÔNG CỤ SAU ---"))
        {
            request.SystemPrompt = InjectToolsPrompt(request.SystemPrompt, request.Tools);
        }

        // 4. Execute AI Request with Schema Validation & Retry & Tool Calling
        string currentPrompt = request.Prompt;
        int maxRetries = settings.MaxRetries > 0 ? settings.MaxRetries : 2;
        int timeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 30;
        int attempt = 0;
        AiResponse? finalResponse = null;
        string? validationError = null;

        while (attempt <= maxRetries)
        {
            if (attempt > 0 && finalResponse != null && validationError != null)
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

                // Apply timeout to the provider call
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                try
                {
                    finalResponse = await provider.CompleteAsync(request, providerConfig, cts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"AI provider '{settings.Provider}' call timed out after {timeoutSeconds} seconds.");
                }

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("AI Provider '{ProviderName}' successfully returned response. Tokens: In={InputTokens}, Out={OutputTokens}, Latency={LatencyMs}ms",
                        finalResponse.ProviderName, finalResponse.InputTokens, finalResponse.OutputTokens, sw.ElapsedMilliseconds);
                }

                // Check for tool call
                if (request.Tools != null && request.Tools.Count > 0 && TryParseToolCall(finalResponse.Content, out var toolName, out var rawParams))
                {
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                    {
                        _logger.LogInformation("AI requested tool call: {ToolName}", toolName);
                    }

                    if (_parameterGuard != null)
                    {
                        var guardResult = await _parameterGuard.GuardAsync(toolName, rawParams, cancellationToken);
                        if (!guardResult.Success)
                        {
                            _logger.LogWarning("Tool parameter guard failed for tool {ToolName}. Error: {Error}, Hint: {Hint}", 
                                toolName, guardResult.UserMessage, guardResult.RetryHint);

                            validationError = $"ToolParameterGuard failed for '{toolName}': {guardResult.UserMessage}. Hint: {guardResult.RetryHint}";
                            attempt++;
                            continue;
                        }
                    }

                    // Check if it is a write action
                    if (IsWriteAction(toolName))
                    {
                        // Create draft in database instead of direct execution!
                        if (_aiJobRepo != null && _aiDraftRepo != null && _unitOfWork != null)
                        {
                            var job = new AiJob
                            {
                                JobType = "DraftChange",
                                ProjectId = request.ProjectId ?? Guid.Empty,
                                SourceType = "Chat",
                                Status = "DraftReady",
                                RequestedById = request.UserId ?? Guid.Empty
                            };
                            await _aiJobRepo.AddAsync(job, cancellationToken);
                            await _unitOfWork.SaveChangesAsync(cancellationToken);

                            var draft = new AiGeneratedDraft
                            {
                                AiJobId = job.Id,
                                ProjectId = request.ProjectId ?? Guid.Empty,
                                DraftType = toolName,
                                PayloadJson = System.Text.Json.JsonSerializer.Serialize(rawParams),
                                Status = "Pending"
                            };
                            await _aiDraftRepo.AddAsync(draft, cancellationToken);
                            await _unitOfWork.SaveChangesAsync(cancellationToken);

                            await _complianceService.LogAuditEventAsync(
                                request.TenantId ?? Guid.Empty,
                                request.ProjectId ?? Guid.Empty,
                                request.UserId ?? Guid.Empty,
                                "AI_DRAFT_CREATED",
                                "AiGeneratedDraft",
                                null,
                                null,
                                System.Text.Json.JsonSerializer.Serialize(new
                                {
                                    draft.Id,
                                    draft.DraftType,
                                    draft.PayloadJson,
                                    draft.Status
                                }),
                                cancellationToken
                            );

                            // Return the draft_change action response immediately!
                            var draftResponseContent = $$"""
                            {
                              "reply": "Erumi đã chuẩn bị bản nháp cho hành động thay đổi dữ liệu của bạn. Vui lòng xác nhận bên dưới.",
                              "metrics": [],
                              "tables": [],
                              "charts": [],
                              "actions": [
                                {
                                  "type": "draft_change",
                                  "label": "Xác nhận thực hiện",
                                  "payload": {
                                    "draftId": "{{draft.Id}}",
                                    "confirmAction": "execute_action"
                                  },
                                  "requiresConfirmation": true
                                }
                              ],
                              "files": []
                            }
                            """;
                            
                            finalResponse = new AiResponse
                            {
                                Content = draftResponseContent,
                                ProviderName = finalResponse.ProviderName,
                                ModelName = finalResponse.ModelName,
                                InputTokens = finalResponse.InputTokens,
                                OutputTokens = finalResponse.OutputTokens,
                                EstimatedCostUsd = finalResponse.EstimatedCostUsd,
                                IsMock = finalResponse.IsMock,
                                CacheHit = finalResponse.CacheHit
                            };
                            validationError = null; // Mark as valid to bypass schema checks
                            break; // break the retry loop and return the draft action!
                        }
                    }
                    else
                    {
                        // Execute Read Tool
                        var tool = request.Tools
                            .OfType<AIFunction>()
                            .FirstOrDefault(t => string.Equals(t.Metadata.Name, toolName, System.StringComparison.OrdinalIgnoreCase));
                        if (tool != null)
                        {
                            try
                            {
                                var boundArgs = BindArguments(tool, rawParams);
                                
                                // Direct safety check for project/member data leakage in read tools
                                if (_parameterGuard != null)
                                {
                                    var pgResult = await _parameterGuard.GuardAsync(toolName, rawParams, cancellationToken);
                                    if (!pgResult.Success)
                                    {
                                        throw new UnauthorizedAccessException($"Access denied: {pgResult.UserMessage}");
                                    }
                                }

                                var invokeResult = await tool.InvokeAsync(boundArgs, cancellationToken);
                                var resultText = invokeResult?.ToString() ?? "Success";

                                if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                                {
                                    _logger.LogInformation("Tool {ToolName} executed successfully. Result length: {Length}", toolName, resultText.Length);
                                }

                                // Append to request history
                                request.History ??= new List<Qaly.Application.DTOs.Ai.AiChatMessageDto>();
                                request.History.Add(new Qaly.Application.DTOs.Ai.AiChatMessageDto(
                                    "assistant", 
                                    $"<tool_call name=\"{toolName}\">{SerializeParameters(rawParams)}</tool_call>"
                                ));
                                request.History.Add(new Qaly.Application.DTOs.Ai.AiChatMessageDto(
                                    "user", 
                                    $"<tool_result name=\"{toolName}\">{resultText}</tool_result>"
                                ));

                                // Loop back to model for next response
                                continue; 
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error executing read tool {ToolName}", toolName);
                                validationError = $"Error executing tool '{toolName}': {ex.Message}";
                                attempt++;
                                continue;
                            }
                        }
                        else
                        {
                            validationError = $"Tool '{toolName}' is not defined/available.";
                            attempt++;
                            continue;
                        }
                    }
                }

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
            return "[\"Fix login issue (Fallback Warning: AI Offline)\", \"Update documentation (Fallback Warning: AI Offline)\"]";
        }

        if (schemaId.Contains("TextAnswer", StringComparison.OrdinalIgnoreCase))
        {
            return """
                {
                  "reply": "AI provider đang tạm thời không phản hồi hoặc hệ thống đang ở chế độ ngoại tuyến. Đây là câu trả lời dự phòng (Fallback) để giao diện không bị lỗi. Vui lòng kiểm tra lại cấu hình AI hoặc thử lại sau.",
                  "metrics": [],
                  "tables": [],
                  "charts": [],
                  "actions": [
                    { "type": "suggested_action", "label": "Thử lại câu hỏi sau" }
                  ],
                  "files": []
                }
                """;
        }

        return "{\"result\": \"Mock response due to AI failure.\", \"warning\": \"Chế độ dự phòng (AI Fallback Mock)\"}";
    }

    private static string InjectToolsPrompt(string systemPrompt, IList<AITool> tools)
    {
        var sb = new StringBuilder(systemPrompt);
        sb.AppendLine("\n\n--- BẠN CÓ QUYỀN TRUY CẬP VÀO CÁC CÔNG CỤ SAU ---");
        sb.AppendLine("Nếu cần gọi công cụ để lấy thông tin hoặc thực hiện hành động, hãy viết một khối XML duy nhất:");
        sb.AppendLine("<tool_call name=\"TênCôngCụ\">");
        sb.AppendLine("  <TênThamSố>GiáTrị</TênThamSố>");
        sb.AppendLine("</tool_call>");
        sb.AppendLine("Lưu ý: Không viết bất kỳ văn bản nào khác ngoài XML khi gọi công cụ. Nếu bạn đã có đủ thông tin, hãy trả lời bình thường mà không gọi công cụ.");
        sb.AppendLine("Danh sách các công cụ khả dụng:");
        
        foreach (var tool in tools)
        {
            if (tool is AIFunction function)
            {
                sb.AppendLine($"- Tên: {function.Metadata.Name}");
                sb.AppendLine($"  Mô tả: {function.Metadata.Description}");
                sb.AppendLine("  Tham số:");
                foreach (var param in function.Metadata.Parameters)
                {
                    var req = param.IsRequired ? "(Bắt buộc)" : "(Tùy chọn)";
                    sb.AppendLine($"    + {param.Name} ({param.ParameterType?.Name}): {param.Description} {req}");
                }
            }
        }
        sb.AppendLine("--------------------------------------------------\n");
        return sb.ToString();
    }

    private static bool TryParseToolCall(string content, out string toolName, out Dictionary<string, object?> parameters)
    {
        toolName = string.Empty;
        parameters = new Dictionary<string, object?>();

        if (string.IsNullOrWhiteSpace(content)) return false;

        // Try XML first
        var xmlMatch = System.Text.RegularExpressions.Regex.Match(content, @"<tool_call\s+name=""([^""]+)""\s*>(.*?)</tool_call>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (xmlMatch.Success)
        {
            toolName = xmlMatch.Groups[1].Value.Trim();
            var innerContent = xmlMatch.Groups[2].Value;

            var paramMatches = System.Text.RegularExpressions.Regex.Matches(innerContent, @"<([^>]+)>(.*?)</\1>", System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match paramMatch in paramMatches)
            {
                var key = paramMatch.Groups[1].Value.Trim();
                var val = paramMatch.Groups[2].Value.Trim();
                parameters[key] = val;
            }
            return true;
        }

        // Try JSON
        var trimmed = content.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.TryGetProperty("tool_call", out var toolCallEl) && toolCallEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (toolCallEl.TryGetProperty("name", out var nameEl))
                    {
                        toolName = nameEl.GetString() ?? string.Empty;
                    }
                    if (toolCallEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var prop in paramsEl.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                parameters[prop.Name] = prop.Value.GetDouble();
                            else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.True || prop.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                                parameters[prop.Name] = prop.Value.GetBoolean();
                            else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
                                parameters[prop.Name] = null;
                            else
                                parameters[prop.Name] = prop.Value.GetString();
                        }
                    }
                    return !string.IsNullOrWhiteSpace(toolName);
                }
            }
            catch
            {
                // Ignore and return false
            }
        }

        return false;
    }

    private static Dictionary<string, object?> BindArguments(AIFunction function, Dictionary<string, object?> rawParams)
    {
        var bound = new Dictionary<string, object?>();
        foreach (var param in function.Metadata.Parameters)
        {
            if (rawParams.TryGetValue(param.Name, out var rawVal) && rawVal != null)
            {
                bound[param.Name] = ConvertType(rawVal, param.ParameterType);
            }
            else if (param.IsRequired)
            {
                bound[param.Name] = null;
            }
        }
        return bound;
    }

    private static object? ConvertType(object val, Type? targetType)
    {
        if (targetType == null) return val;
        
        var valStr = val.ToString();
        if (string.IsNullOrWhiteSpace(valStr)) return null;

        if (targetType == typeof(Guid) || targetType == typeof(Guid?))
        {
            if (Guid.TryParse(valStr, out var g)) return g;
        }
        if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
        {
            if (DateTimeOffset.TryParse(valStr, out var dto)) return dto;
        }
        if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
        {
            if (DateTime.TryParse(valStr, out var dt)) return dt;
        }
        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            if (int.TryParse(valStr, out var i)) return i;
        }
        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            if (double.TryParse(valStr, out var d)) return d;
        }
        if (targetType == typeof(string))
        {
            return valStr;
        }

        return Convert.ChangeType(val, targetType);
    }

    private static string SerializeParameters(Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            sb.Append($"<{kvp.Key}>{kvp.Value}</{kvp.Key}>");
        }
        return sb.ToString();
    }

    private static bool IsWriteAction(string toolName)
    {
        return toolName switch
        {
            "CreateTask" => true,
            "UpdateTaskStatus" => true,
            "AssignTask" => true,
            "SetTaskPriority" => true,
            "AddDueDate" => true,
            "AddComment" => true,
            "StartTimeTracking" => true,
            "StopTimeTracking" => true,
            _ => false
        };
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

}
