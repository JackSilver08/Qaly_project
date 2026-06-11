using System;

namespace Qaly.Application.Services;

public record ToolResult
{
    public bool Success { get; init; }
    public string? Data { get; init; }        // JSON string of result when success
    public string? ErrorCode { get; init; }   // e.g., "VALIDATION_FAILED", "UNAUTHORIZED"
    public string? UserMessage { get; init; } // message to display directly in chatbot
    public string? RetryHint { get; init; }   // instruction to help LLM correct its parameters
}
