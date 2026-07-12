using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Qaly.Application.Services;

public class AiRequest
{
    public Guid? JobId { get; set; }
    public Guid? ProviderAttemptId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string ProviderHint { get; set; } = "auto";
    public string Prompt { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string ExpectedSchemaId { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ConsentId { get; set; }
    public Guid? RetentionPolicyId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string DataClassification { get; set; } = string.Empty;
    public string ProviderClass { get; set; } = string.Empty;
    public int? RetentionDays { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceEntityId { get; set; }
    public bool UseCache { get; set; } = true;
    public bool AllowMockFallback { get; set; }
    public System.Collections.Generic.IList<Qaly.Application.DTOs.Ai.AiChatMessageDto>? History { get; set; }
    public System.Collections.Generic.IList<AITool>? Tools { get; set; }
}

public class AiResponse
{
    public bool IsSuccess { get; set; } = true;
    public string Content { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool IsMock { get; set; }
    public bool CacheHit { get; set; }
    public string? MockReason { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Retryable { get; set; }
}

public interface IAiGateway
{
    /// <summary>
    /// Routes the request to the best AI provider based on budget, priority, and sensitivity.
    /// Handles fallback, caching, and logs usage to AiUsageLedger.
    /// </summary>
    Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken = default);
}
