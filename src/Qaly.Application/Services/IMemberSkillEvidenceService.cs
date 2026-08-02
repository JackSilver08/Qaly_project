using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services;

public interface IMemberSkillEvidenceService
{
    Task<Result<TaskCompletionAttributionsDto>> GetTaskCompletionAttributionsAsync(Guid taskId, CancellationToken ct = default);
    Task<Result<TaskCompletionAttributionsDto>> ReplaceTaskCompletionAttributionsAsync(Guid taskId, ReplaceTaskCompletionAttributionsDto dto, CancellationToken ct = default);
    Task<Result<TaskCompletionAttributionDto>> RequestCorrectionAsync(Guid taskId, Guid attributionId, RequestCompletionAttributionCorrectionDto dto, CancellationToken ct = default);
    Task<Result<MemberSkillProfileDto>> GetMemberSkillProfileAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);
}
