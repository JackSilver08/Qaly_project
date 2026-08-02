using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiAssistantContextRegistry
{
    Task<Result<AiAssistantExecutionContextDto>> DiscoverAsync(
        AiAssistantTurnRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiAssistantExecutionContextDto>> ResolveAsync(
        AiAssistantTurnRequestDto request,
        CancellationToken ct = default);
}
