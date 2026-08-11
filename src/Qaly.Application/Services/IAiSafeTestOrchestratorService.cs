using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiSafeTestOrchestratorService
{
    Task<Result<AiSafeTestRunPreviewDto>> PrepareAsync(
        Guid sessionId,
        Guid originTurnId,
        CancellationToken ct = default);

    Task<Result<AiSafeTestRunReportDto>> ConfirmAsync(
        Guid runId,
        long expectedRevision,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<Result<AiSafeTestRunReportDto>> GetAsync(Guid runId, CancellationToken ct = default);
}
