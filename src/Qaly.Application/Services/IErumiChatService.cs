using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IErumiChatService
{
    Task<Result<ErumiChatResponseDto>> ChatFastAsync(ErumiChatRequestDto request, CancellationToken ct = default);
    Task<Result<AiAssistantTurnResponseDto>> AssistantTurnAsync(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto executionContext,
        CancellationToken ct = default);
    Task<Result<AiAssistantTurnResponseDto>> AssistantPlannedTurnAsync(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto executionContext,
        AiAssistantGoalPlanningResultDto planning,
        CancellationToken ct = default);
}
