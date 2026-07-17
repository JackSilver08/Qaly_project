using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAgentRunService
{
    Task<Result<AgentRunDto>> StartAsync(StartAgentRunDto request, CancellationToken ct = default);
    Task<Result<AgentRunDto>> GetAsync(Guid runId, CancellationToken ct = default);
    Task<Result<AiDraftConfirmResultDto>> ApproveAsync(Guid runId, ApproveAgentRunDto request, CancellationToken ct = default);
}
