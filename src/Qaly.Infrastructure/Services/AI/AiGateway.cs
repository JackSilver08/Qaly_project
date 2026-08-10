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

        // 1. Compliance check. Sensitive v4 requests carry the exact consent and policy IDs.
        PrivacyProcessingDecision? privacyDecision = null;
        if (request.IsSensitive && request.RetentionPolicyId.HasValue)
        {
            privacyDecision = await _complianceService.EvaluateProcessingAsync(new PrivacyProcessingRequest
            {
                TenantId = request.TenantId ?? Guid.Empty,
                ProjectId = request.ProjectId ?? Guid.Empty,
                UserId = request.UserId ?? Guid.Empty,
                Purpose = string.IsNullOrWhiteSpace(request.Purpose)
                    ? PrivacyPurposes.AiCloudProcessing
                    : request.Purpose,
                DataClassification = string.IsNullOrWhiteSpace(request.DataClassification)
                    ? PrivacyDataClasses.SensitiveCollaboration
                    : request.DataClassification,
                ProviderClass = string.IsNullOrWhiteSpace(request.ProviderClass)
                    ? PrivacyProviderClasses.Any
                    : request.ProviderClass,
                ConsentId = request.ConsentId,
                RetentionPolicyId = request.RetentionPolicyId,
                RetentionDays = request.RetentionDays,
                SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "ai_job" : request.SourceType,
                SourceEntityId = request.SourceEntityId
            }, cancellationToken);
        }

        var canProcessInCloud = privacyDecision?.CloudEligible ?? await _complianceService.CanProcessInCloudAsync(
            request.TenantId ?? Guid.Empty,
            request.ProjectId ?? Guid.Empty,
            request.UserId ?? Guid.Empty,
            request.IsSensitive,
            cancellationToken);
        var localPolicyEligible = privacyDecision?.LocalEligible ?? true;
        var canUseLocalSensitiveProvider = localPolicyEligible &&
            (settings.OfflineMode || settings.AllowLocalSensitiveProcessing);
        if (privacyDecision?.Allowed == false || (!canProcessInCloud && !canUseLocalSensitiveProvider))
        {
            _aiRequestBlockedComplianceLogger(_logger, null);
            await _complianceService.LogPrivacyAuditEventAsync(new PrivacyAuditRecord
            {
                TenantId = request.TenantId,
                ProjectId = request.ProjectId,
                ActorUserId = request.UserId,
                EventType = "AI_PROCESSING_BLOCKED",
                EntityType = "AiRequest",
                EntityId = request.SourceEntityId,
                AiJobId = request.JobId,
                ProviderAttemptId = request.ProviderAttemptId,
                PrivacyConsentId = request.ConsentId,
                RetentionPolicyId = request.RetentionPolicyId,
                Purpose = request.Purpose,
                PolicyVersion = privacyDecision?.PolicyVersion,
                DataClassification = request.DataClassification,
                ProviderClass = request.ProviderClass,
                Outcome = "blocked",
                FailureCode = privacyDecision?.ErrorCode ?? PrivacyErrorCodes.CloudBlocked,
                Metadata = new Dictionary<string, string?>
                {
                    ["jobType"] = request.JobType,
                    ["reason"] = privacyDecision?.Reason ?? "No eligible provider class"
                }
            }, cancellationToken);

            return CreateFailureResponse(
                privacyDecision?.ErrorCode is PrivacyErrorCodes.ConsentRequired or
                    PrivacyErrorCodes.ConsentInvalid or
                    PrivacyErrorCodes.ConsentRevoked or
                    PrivacyErrorCodes.ConsentExpired
                    ? AiErrorCodes.ConsentRequired
                    : AiErrorCodes.SensitiveBlocked,
                privacyDecision?.Reason ?? "Sensitive input has no eligible provider.",
                retryable: false);
        }

        // 2. Budget Check
        bool hasBudget = await _costService.EnsureBudgetAvailableAsync(request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, cancellationToken);
        if (!hasBudget)
        {
            _aiRequestBlockedBudgetLogger(_logger, null);
            return CreateFailureResponse(
                AiErrorCodes.BudgetExceeded,
                "The effective AI budget has been exceeded.",
                retryable: false);
        }

        // Offline/demo output is still subject to compliance and budget policy.
        if (settings.OfflineMode)
        {
            _logger.LogWarning("AI Gateway is in OfflineMode/DemoMode. Returning offline fallback mock.");
            return CreateMockResponse(GetFallbackResponse(request), "OfflineMock", "offline_mode");
        }

        // Proactive RAG (Context Retrieval)
        if (request.UseRetrievalAugmentation &&
            request.ProjectId.HasValue &&
            request.UserId.HasValue &&
            _vectorStorage != null &&
            _embeddingGenerator != null)
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
        var legacyHashInput = new StringBuilder()
            .Append(request.SystemPrompt).Append('|')
            .Append(request.Prompt).Append('|')
            .Append(request.ExpectedSchemaId);
        var hashInput = new StringBuilder();
        hashInput
            .Append(request.SystemPrompt).Append('|')
            .Append(request.Prompt).Append('|')
            .Append(request.ExpectedSchemaId).Append('|')
            .Append(request.ProviderHint).Append('|')
            .Append(request.StrictProvider).Append('|')
            .Append(settings.Provider).Append('|')
            .Append(settings.Ollama.Model).Append('|')
            .Append(settings.DeepSeek.Model).Append('|')
            .Append(settings.OpenAI.Model).Append('|')
            .Append(settings.Gemini.Model);
        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                legacyHashInput.Append('|').Append(msg.Role).Append(':').Append(msg.Content);
                hashInput.Append('|').Append(msg.Role).Append(':').Append(msg.Content);
            }
        }
        string requestHash = ComputeSha256Hash(hashInput.ToString());
        string legacyRequestHash = ComputeSha256Hash(legacyHashInput.ToString());
        
        if (request.UseCache && !request.BypassCacheRead)
        {
            var cachedPrompt = await _context.AiPromptCache
                .FirstOrDefaultAsync(c => c.RequestHash == requestHash, cancellationToken);
            if (cachedPrompt == null && legacyRequestHash != requestHash)
            {
                cachedPrompt = await _context.AiPromptCache
                    .FirstOrDefaultAsync(c => c.RequestHash == legacyRequestHash, cancellationToken);
            }

            if (cachedPrompt != null && (cachedPrompt.ExpiresAt == null || cachedPrompt.ExpiresAt > DateTimeOffset.UtcNow))
            {
                if (string.IsNullOrWhiteSpace(request.ExpectedSchemaId) ||
                    _outputValidator.Validate(
                        cachedPrompt.ResponseJson,
                        request.ExpectedSchemaId,
                        request.ValidationContextJson,
                        out var cacheValidationError))
                {
                    cachedPrompt.HitCount++;
                    await _context.SaveChangesAsync(cancellationToken);

                    await _costService.RecordJobUsageAsync(
                        request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.JobType,
                        cachedPrompt.ProviderName ?? "Cache", cachedPrompt.ModelName ?? "Cache",
                        0, 0, 0m, (int)sw.ElapsedMilliseconds, "success", true,
                        request.JobId, request.ProviderAttemptId,
                        cancellationToken: cancellationToken);

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

                _logger.LogWarning(
                    "Ignoring invalid AI cache entry {CacheId} for schema {SchemaId}: {ValidationError}",
                    cachedPrompt.Id,
                    request.ExpectedSchemaId,
                    cacheValidationError);
                cachedPrompt.ExpiresAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        if (request.Tools != null && request.Tools.Count > 0 && !request.SystemPrompt.Contains("--- BẠN CÓ QUYỀN TRUY CẬP VÀO CÁC CÔNG CỤ SAU ---"))
        {
            request.SystemPrompt = InjectToolsPrompt(request.SystemPrompt, request.Tools);
        }

        // 4. Execute AI Request with Schema Validation & Retry & Tool Calling
        string originalPrompt = request.Prompt;
        int maxRetries = Math.Max(0, settings.SchemaRepairAttempts);
        AiResponse? finalResponse = null;
        string? validationError = null;
        bool anyProviderResponse = false;
        string lastProviderName = settings.Provider;
        var providerOrder = ResolveProviderOrder(
            settings,
            request.ProviderHint,
            request.StrictProvider,
            canProcessInCloud || !request.IsSensitive,
            canUseLocalSensitiveProvider || !request.IsSensitive);

        if (providerOrder.Count == 0)
        {
            return CreateFailureResponse(
                AiErrorCodes.SensitiveBlocked,
                "Sensitive input has no eligible local provider.",
                retryable: false);
        }

        var availableProviderOrder = providerOrder
            .Where(providerName =>
                _providerFactory.TryGetProvider(providerName, out var provider) && provider != null)
            .ToList();

        for (var providerIndex = 0; providerIndex < availableProviderOrder.Count; providerIndex++)
        {
            var providerName = availableProviderOrder[providerIndex];
            if (!_providerFactory.TryGetProvider(providerName, out var provider) || provider == null)
            {
                continue;
            }

            var hasFallbackProvider = providerIndex < availableProviderOrder.Count - 1;
            lastProviderName = providerName;
            request.Prompt = originalPrompt;
            finalResponse = null;
            validationError = null;
            int attempt = 0;

            while (attempt <= maxRetries)
            {
                if (attempt > 0 && finalResponse != null && validationError != null)
                {
                    request.Prompt = originalPrompt + $"\n\n[Warning]: Your previous response was invalid. It failed validation with error: '{validationError}'. Please return a valid JSON format complying with the expected schema: '{request.ExpectedSchemaId}'. Do not include markdown blocks or any conversational text around the JSON.";
                }

                try
                {
                    var providerConfig = GetProviderSetting(settings, providerName);
                    var timeoutSeconds = Math.Max(1, settings.ProviderTimeoutSeconds);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    try
                    {
                        finalResponse = await provider.CompleteAsync(request, providerConfig, cts.Token);
                        anyProviderResponse = true;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"AI provider '{providerName}' call timed out after {timeoutSeconds} seconds.");
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
                        if (_aiJobRepo != null && _aiDraftRepo != null && _unitOfWork != null &&
                            request.ProjectId.HasValue && request.UserId.HasValue)
                        {
                            var job = request.JobId.HasValue
                                ? await _aiJobRepo.GetByIdAsync(request.JobId.Value, cancellationToken)
                                : null;
                            if (job == null)
                            {
                                var legacyKey = $"legacy-gateway:{Guid.NewGuid():N}";
                                job = new AiJob
                                {
                                    TenantId = request.TenantId,
                                    JobType = "DraftChange",
                                    ProjectId = request.ProjectId.Value,
                                    SourceType = "Chat",
                                    SchemaId = string.IsNullOrWhiteSpace(request.ExpectedSchemaId) ? "draft_change.v4" : request.ExpectedSchemaId,
                                    SchemaVersion = "4.0",
                                    RequestJson = "{}",
                                    RequestHash = ComputeSha256Hash(request.SystemPrompt + "|" + request.Prompt),
                                    IdempotencyKey = legacyKey,
                                    Status = AiJobStatuses.Succeeded,
                                    ProgressPercent = 100,
                                    AvailableAt = DateTimeOffset.UtcNow,
                                    FinishedAt = DateTimeOffset.UtcNow,
                                    ResultJson = finalResponse.Content,
                                    ResultHash = ComputeSha256Hash(finalResponse.Content),
                                    CacheKey = legacyKey,
                                    RequestedById = request.UserId.Value
                                };
                                await _aiJobRepo.AddAsync(job, cancellationToken);
                                await _unitOfWork.SaveChangesAsync(cancellationToken);
                            }

                            var draft = await _aiDraftRepo.GetQueryable()
                                .FirstOrDefaultAsync(item => item.AiJobId == job.Id, cancellationToken);
                            if (draft == null)
                            {
                                var payloadJson = System.Text.Json.JsonSerializer.Serialize(rawParams);
                                draft = new AiGeneratedDraft
                                {
                                    AiJobId = job.Id,
                                    ProjectId = request.ProjectId.Value,
                                    DraftType = toolName,
                                    PayloadJson = payloadJson,
                                    OriginalPayloadJson = payloadJson,
                                    WorkingPayloadJson = payloadJson,
                                    Status = AiDraftStatuses.PendingReview,
                                    SchemaId = job.SchemaId
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
                                        draft.Status
                                    }),
                                    cancellationToken
                                );
                            }

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
                            .FirstOrDefault(t => string.Equals(t.Name, toolName, System.StringComparison.OrdinalIgnoreCase));
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

                                var invokeResult = await tool.InvokeAsync(new AIFunctionArguments(boundArgs), cancellationToken);
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
                if (string.IsNullOrWhiteSpace(request.ExpectedSchemaId) ||
                    _outputValidator.Validate(
                        finalResponse.Content,
                        request.ExpectedSchemaId,
                        request.ValidationContextJson,
                        out validationError))
                {
                    // Validation success or schema validation not required
                    break;
                }

                _logger.LogWarning("AI output validation failed for schema {SchemaId} on attempt {Attempt}. Error: {ValidationError}", 
                    request.ExpectedSchemaId, attempt + 1, validationError);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _errorCallingAiProviderLogger(_logger, ex);
                    validationError = ex.Message;
                    await LogProviderRouteEventAsync(request, providerName, "AI_PROVIDER_FAILED", ex.Message, cancellationToken);
                    if (hasFallbackProvider)
                    {
                        break;
                    }
                }

                attempt++;
            }

            if (finalResponse != null &&
                (string.IsNullOrWhiteSpace(request.ExpectedSchemaId) || validationError == null))
            {
                break;
            }

            await LogProviderRouteEventAsync(
                request,
                providerName,
                "AI_PROVIDER_FALLBACK",
                validationError ?? "Provider did not produce a usable response.",
                cancellationToken);
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
            await _costService.RecordJobUsageAsync(
                request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.JobType,
                finalResponse.ProviderName, finalResponse.ModelName, finalResponse.InputTokens, finalResponse.OutputTokens, finalResponse.EstimatedCostUsd, 
                (int)sw.ElapsedMilliseconds, "success", false,
                request.JobId, request.ProviderAttemptId,
                cancellationToken: cancellationToken);

            return finalResponse;
        }

        // Log failed usage
        await _costService.RecordJobUsageAsync(
            request.TenantId ?? Guid.Empty, request.ProjectId ?? Guid.Empty, request.UserId ?? Guid.Empty, request.JobType,
            lastProviderName, "Unknown", 0, 0, 0m, (int)sw.ElapsedMilliseconds, "failed", false,
            request.JobId, request.ProviderAttemptId,
            anyProviderResponse ? AiErrorCodes.SchemaInvalid : AiErrorCodes.ProviderUnavailable,
            cancellationToken);

        if (request.AllowMockFallback && settings.AllowProviderDegradedMock)
        {
            return CreateMockResponse(GetFallbackResponse(request), "ProviderDegradedMock", "provider_degraded");
        }

        return CreateFailureResponse(
            anyProviderResponse ? AiErrorCodes.SchemaInvalid : AiErrorCodes.ProviderUnavailable,
            anyProviderResponse
                ? $"AI output failed schema validation after the permitted repair attempts. {validationError}"
                : request.StrictProvider
                    ? validationError ?? "The selected AI provider did not complete the request."
                    : "No configured AI provider completed the request. Choose a configured model or try again.",
            retryable: !anyProviderResponse);
    }

    private async Task LogProviderRouteEventAsync(
        AiRequest request,
        string providerName,
        string eventType,
        string detail,
        CancellationToken cancellationToken)
    {
        await _complianceService.LogJobAuditEventAsync(
            request.TenantId,
            request.ProjectId,
            request.UserId,
            eventType,
            nameof(AiRequest),
            null,
            null,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                provider = providerName,
                detail = detail.Length <= 500 ? detail : detail[..500]
            }),
            aiJobId: request.JobId,
            providerAttemptId: request.ProviderAttemptId,
            cancellationToken: cancellationToken);
    }

    private static List<string> ResolveProviderOrder(
        AiGatewaySettings settings,
        string? providerHint,
        bool strictProvider,
        bool canProcessInCloud,
        bool canProcessLocally)
    {
        var requestedProvider = NormalizeProviderName(providerHint);
        if (strictProvider && !string.IsNullOrWhiteSpace(requestedProvider))
        {
            var eligible = IsLocalProvider(requestedProvider)
                ? canProcessLocally
                : canProcessInCloud;
            return eligible ? [requestedProvider] : [];
        }

        var candidates = new[] { requestedProvider, settings.Provider }
            .Concat(settings.FallbackProviders ?? [])
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Select(provider => provider!.Trim())
            .Where(provider => IsLocalProvider(provider) ? canProcessLocally : canProcessInCloud)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates;
    }

    private static string? NormalizeProviderName(string? providerHint)
        => providerHint?.Trim().ToLowerInvariant() switch
        {
            null or "" or "auto" => null,
            "local" => "Ollama",
            "deepseek" or "deepseek-v4-pro" => "DeepSeek",
            "openai" => "OpenAI",
            "gemini" => "Gemini",
            "ollama" => "Ollama",
            _ => providerHint.Trim()
        };

    private static bool IsLocalProvider(string providerName)
        => string.Equals(providerName, "Ollama", StringComparison.OrdinalIgnoreCase);

    private static AiProviderSetting GetProviderSetting(AiGatewaySettings settings, string providerName)
        => providerName.Trim().ToLowerInvariant() switch
        {
            "deepseek" => settings.DeepSeek,
            "openai" => settings.OpenAI,
            "gemini" => settings.Gemini,
            _ => settings.Ollama
        };

    private static AiResponse CreateMockResponse(string content, string provider, string reason)
    {
        return new AiResponse
        {
            IsSuccess = true,
            Content = content,
            ProviderName = provider,
            ModelName = "Mock-1.0",
            InputTokens = 0,
            OutputTokens = 0,
            EstimatedCostUsd = 0m,
            IsMock = true,
            CacheHit = false,
            MockReason = reason
        };
    }

    private static AiResponse CreateFailureResponse(string errorCode, string message, bool retryable)
        => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            Retryable = retryable
        };

    private string GetFallbackResponse(AiRequest request)
    {
        string schemaId = request.ExpectedSchemaId;
        if (schemaId.Contains("project_delay_resolution", StringComparison.OrdinalIgnoreCase))
        {
            var projectId = request.ProjectId ?? Guid.Empty;
            var project = _context.Projects.FirstOrDefault(p => p.Id == projectId);
            var overdueTask = _context.TaskItems.FirstOrDefault(t => t.ProjectId == projectId && t.Status != "Done");
            var taskGuid = overdueTask?.Id ?? Guid.Empty;
            var taskTitle = overdueTask?.Title ?? "Thiết lập AI Gateway";
            
            return $$"""
                {
                  "projectId": "{{projectId}}",
                  "actions": [
                    {
                      "type": "SendNotification",
                      "recipientEmail": "trungnguyendoan9@gmail.com",
                      "recipientName": "Trung Nguyen",
                      "subject": "[Qaly Cảnh Báo Trễ Hạn] Đề xuất xử lý tiến độ dự án '{{project?.Name ?? "Dự án Qaly"}}'",
                      "message": "Xin chào Trung Nguyen, công việc '{{taskTitle}}' của dự án đang bị chậm tiến độ so với deadline. Vui lòng tập trung hoàn thành trước để mọi người tranh thủ hoàn thành trước."
                    },
                    {
                      "type": "UpdateTask",
                      "taskId": "{{taskGuid}}",
                      "title": "{{taskTitle}}",
                      "status": "In Progress",
                      "priority": "Critical",
                      "dueDate": "{{DateTimeOffset.UtcNow.AddDays(3):yyyy-MM-ddTHH:mm:sszzz}}"
                    }
                  ]
                }
                """;
        }

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
                sb.AppendLine($"- Tên: {function.Name}");
                sb.AppendLine($"  Mô tả: {function.Description}");
                sb.AppendLine("  Tham số:");
                foreach (var param in function.UnderlyingMethod?.GetParameters() ?? [])
                {
                    var req = !param.IsOptional ? "(Bắt buộc)" : "(Tùy chọn)";
                    sb.AppendLine($"    + {param.Name} ({param.ParameterType.Name}) {req}");
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
        foreach (var param in function.UnderlyingMethod?.GetParameters() ?? [])
        {
            if (param.Name != null && rawParams.TryGetValue(param.Name, out var rawVal) && rawVal != null)
            {
                bound[param.Name] = ConvertType(rawVal, param.ParameterType);
            }
            else if (!param.IsOptional && param.Name != null)
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
