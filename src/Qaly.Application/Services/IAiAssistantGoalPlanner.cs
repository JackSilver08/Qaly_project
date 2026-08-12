using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiAssistantGoalPlanner
{
    Task<Result<AiAssistantGoalPlanningResultDto>> PlanAsync(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto discoveryContext,
        CancellationToken ct = default);
}
