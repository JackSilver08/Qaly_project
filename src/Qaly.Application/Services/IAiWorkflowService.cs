using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiWorkflowService
{
    Task<Result<AiJobCreatedDto>> CreateJobAsync(CreateAiJobDto dto, CancellationToken ct = default);
    Task<Result<AiDraftConfirmResultDto>> ConfirmDraftAsync(Guid draftId, ConfirmAiDraftDto dto, CancellationToken ct = default);
}
