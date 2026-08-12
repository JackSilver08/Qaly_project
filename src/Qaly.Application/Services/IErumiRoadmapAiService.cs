using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IErumiRoadmapAiService
{
    Task<Result<ErumiRoadmapChatResponseDto>> ChatAndProposeRoadmapAsync(ErumiRoadmapChatRequestDto dto, CancellationToken ct = default);
    Task<Result> ApproveRoadmapProposalAsync(ApproveErumiRoadmapProposalDto dto, CancellationToken ct = default);
    Task<Result> RollbackRoadmapSnapshotAsync(RollbackErumiRoadmapSnapshotDto dto, CancellationToken ct = default);
}
