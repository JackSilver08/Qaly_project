using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiWorkflowService
{
    Task<Result<AiJobCreatedDto>> CreateJobAsync(
        CreateAiJobDto dto,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateJobAsync(
        CreateAiJobDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateProjectProgressSummaryAsync(
        Guid projectId,
        ProjectProgressSummaryRequestDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateSprintProgressSummaryAsync(
        Guid projectId,
        Guid sprintId,
        ProjectProgressSummaryRequestDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateTaskSkillSuggestionAsync(
        Guid taskId,
        TaskSkillSuggestionRequestDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateSourceLinkedTaskDraftAsync(
        Guid groupId,
        AiFunctionJobRequest dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateGroupSelectedSummaryAsync(
        Guid groupId,
        GroupSummaryRequestDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<AiJobCreatedDto>> CreateDashboardStrategicBriefAsync(
        DashboardStrategicBriefRequestDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default);
    Task<Result<IReadOnlyList<AiJobSummaryDto>>> ListJobsAsync(Guid? projectId, string? status, CancellationToken ct = default);
    Task<Result<AiJobDetailDto>> GetJobAsync(Guid jobId, CancellationToken ct = default);
    Task<Result<AiJobResultDto>> GetJobResultAsync(Guid jobId, CancellationToken ct = default);
    Task<Result<AiJobDetailDto>> RetryJobAsync(Guid jobId, RetryAiJobDto dto, CancellationToken ct = default);
    Task<Result<AiJobDetailDto>> CancelJobAsync(Guid jobId, CancelAiJobDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AiDraftSummaryDto>>> ListDraftsAsync(Guid? projectId, string? type, string? status, CancellationToken ct = default);
    Task<Result<AiDraftDetailDto>> GetDraftAsync(Guid draftId, CancellationToken ct = default);
    Task<Result<AiDraftDetailDto>> PatchDraftAsync(Guid draftId, PatchAiDraftDto dto, CancellationToken ct = default);
    Task<Result<AiDraftConfirmResultDto>> ConfirmDraftAsync(Guid draftId, ConfirmAiDraftDto dto, CancellationToken ct = default);
    Task<Result<AiDraftDetailDto>> RejectDraftAsync(Guid draftId, RejectAiDraftDto dto, CancellationToken ct = default);
}
