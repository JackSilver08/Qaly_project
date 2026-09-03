using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiAssistantSessionService
{
    Task<Result<AiAssistantSessionDto>> CreateAsync(
        CreateAiAssistantSessionRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> GetAsync(Guid sessionId, CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto?>> GetRecentAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<AiAssistantSessionSummaryDto>>> ListAsync(
        bool includeArchived = false,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> RenameAsync(
        Guid sessionId,
        UpdateAiAssistantSessionRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> UpdateScopeAsync(
        Guid sessionId,
        UpdateAiAssistantSessionScopeRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> ArchiveAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct = default);

    Task<Result> DeleteAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> SaveClarificationDraftAsync(
        Guid sessionId,
        UpdateAiAssistantClarificationDraftRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> ClearClarificationDraftAsync(
        Guid sessionId,
        long expectedVersion,
        CancellationToken ct = default);

    Task<Result<AiAssistantTurnResponseDto>> AppendTurnAsync(
        AiAssistantTurnRequestDto request,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct = default);

    Task<Result<AiAssistantSessionDto>> CancelTurnAsync(
        Guid turnId,
        long expectedVersion,
        CancellationToken ct = default);

    Task<Result<AiAssistantTurnResponseDto>> ResumeTurnAsync(
        Guid turnId,
        AiAssistantTurnControlRequestDto request,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct = default);

    Task<Result<AiAssistantQualityMetricsDto>> GetQualityMetricsAsync(CancellationToken ct = default);
}
