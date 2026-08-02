using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiActionPlanValidator : IAiActionPlanValidator
{
    public bool TryValidateReviewedPlan(
        string payloadJson,
        string snapshotJson,
        out AiActionPlanDto? plan,
        out AiActionContextSnapshotDto? snapshot,
        out string? error)
    {
        plan = null;
        if (!AiActionComposerOutputContract.TryReadSnapshot(snapshotJson, out snapshot, out error) || snapshot == null)
        {
            return false;
        }

        return AiActionComposerOutputContract.TryValidateReviewedPlan(
            payloadJson,
            snapshot,
            out plan,
            out error);
    }
}
