using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IProjectLaunchOrchestratorService
{
    Task<Result<ProjectLaunchPlanDto>> GeneratePlanAsync(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto executionContext,
        CancellationToken ct = default);

    Task<Result<ProjectLaunchPlanDto>> GetPlanAsync(Guid planId, CancellationToken ct = default);

    Task<Result<ProjectLaunchPlanDto>> ConfirmAsync(
        Guid planId,
        ConfirmProjectLaunchPlanRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<Result<ProjectLaunchPlanDto>> RollbackAsync(
        Guid executionId,
        RollbackProjectLaunchExecutionRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<Result<ProjectLaunchPlanDto>> MonitorAsync(
        Guid executionId,
        MonitorProjectLaunchExecutionRequestDto request,
        CancellationToken ct = default);

    Task<Result<ProjectLaunchPlanDto>> MonitorProjectAsync(
        Guid projectId,
        CancellationToken ct = default);

    Task EvaluateDueAsync(CancellationToken ct = default);
}
