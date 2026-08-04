using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IPortfolioScheduleService
{
    Task<Result<PortfolioCapacityDto>> GetCapacityAsync(Guid projectId, DateTimeOffset? from, DateTimeOffset? windowEnd, CancellationToken ct = default);
    Task<Result<PortfolioMemberCapacityDto>> UpdateCapacityProfileAsync(Guid organizationId, Guid userId, UpdateMemberCapacityProfileDto dto, CancellationToken ct = default);
    Task<Result<PortfolioScheduleProposalDto>> CreateProposalAsync(Guid projectId, CreatePortfolioScheduleProposalDto dto, string idempotencyKey, CancellationToken ct = default);
    Task<Result<PortfolioScheduleProposalDto>> GetProposalAsync(Guid projectId, Guid draftId, CancellationToken ct = default);
    Task<Result<PortfolioScheduleProposalDto>> UpdateProposalAsync(Guid projectId, Guid draftId, UpdatePortfolioScheduleProposalDto dto, CancellationToken ct = default);
    Task<Result<PortfolioScheduleProposalDto>> ConfirmProposalAsync(Guid projectId, Guid draftId, ConfirmPortfolioScheduleProposalDto dto, CancellationToken ct = default);
    Task<Result<PortfolioScheduleProposalDto>> RejectProposalAsync(Guid projectId, Guid draftId, RejectPortfolioScheduleProposalDto dto, CancellationToken ct = default);
}
