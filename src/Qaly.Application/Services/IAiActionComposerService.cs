using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiActionComposerService
{
    Task<Result<AiJobCreatedDto>> ComposeAsync(
        AiActionComposeRequestDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
}

public interface IAiJobActivityService
{
    Task AppendAsync(
        Guid jobId,
        AppendAiActionActivityDto dto,
        CancellationToken ct = default);

    Task<Result<AiActionActivityFeedDto>> GetAsync(
        Guid jobId,
        int afterSequence = 0,
        CancellationToken ct = default);
}

public interface IAiActionPlanValidator
{
    bool TryValidateReviewedPlan(
        string payloadJson,
        string snapshotJson,
        out AiActionPlanDto? plan,
        out AiActionContextSnapshotDto? snapshot,
        out string? error);
}
