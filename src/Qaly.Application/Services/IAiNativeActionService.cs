using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiNativeActionService
{
    Task<Result<AiNativeActionDraftDto>> PrepareAsync(
        string capabilityId,
        AiAssistantTurnRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiNativeActionDraftDto>> GetAsync(Guid draftId, CancellationToken ct = default);

    Task<Result<AiNativeActionDraftDto>> UpdateAsync(
        Guid draftId,
        UpdateAiNativeActionRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiNativeActionDraftDto>> RejectAsync(
        Guid draftId,
        RejectAiNativeActionRequestDto request,
        CancellationToken ct = default);

    Task<Result<AiNativeActionReceiptDto>> ConfirmAsync(
        Guid draftId,
        ConfirmAiNativeActionRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<TaskAcceptanceChecklistItemDto>>> GetChecklistAsync(
        Guid taskId,
        CancellationToken ct = default);

    Task<Result<TaskAcceptanceChecklistItemDto>> UpdateChecklistItemAsync(
        Guid itemId,
        UpdateTaskAcceptanceChecklistItemRequestDto request,
        CancellationToken ct = default);

    Task<Result<ProjectDigestSubscriptionDto?>> GetDigestSubscriptionAsync(
        Guid projectId,
        CancellationToken ct = default);
}
