using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public sealed record ProjectLaunchAnalysisResultDto(
    ProjectLaunchBriefDto? Brief,
    AiAssistantConversationTurnDto Conversation);

public interface IProjectLaunchService
{
    Task<Result<ProjectLaunchAnalysisResultDto>> AnalyzeAsync(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto executionContext,
        CancellationToken ct = default);
}
