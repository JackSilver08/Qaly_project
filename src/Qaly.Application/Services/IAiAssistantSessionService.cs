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

    Task<Result<AiAssistantTurnResponseDto>> AppendTurnAsync(
        AiAssistantTurnRequestDto request,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct = default);
}
