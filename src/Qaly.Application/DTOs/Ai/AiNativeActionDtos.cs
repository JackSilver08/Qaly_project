using System.Text.Json;

namespace Qaly.Application.DTOs.Ai;

public static class AiNativeDomainActionContract
{
    public const string ChecklistCapability = "task.acceptance_checklist.v1";
    public const string BreakdownCapability = "task.breakdown.v1";
    public const string WikiCapability = "wiki.brief_task.v1";
    public const string GroupPollCapability = "group.poll.create.v1";
    public const string DigestCapability = "project.digest.configure.v1";
    public const string MeetingActionsCapability = "meeting.actions.review.v1";
    public const string RoadmapAdjustCapability = "project.roadmap.adjust.v1";
    public const string SkillEvidenceCapability = "task.skill_evidence.confirm.v1";

    public const string ChecklistSchemaId = "task_acceptance_checklist_draft.v1";
    public const string BreakdownSchemaId = "task_breakdown_draft.v1";
    public const string WikiSchemaId = "wiki_brief_task_draft.v1";
    public const string GroupPollSchemaId = "group_poll_draft.v1";
    public const string DigestSchemaId = "project_digest_subscription_draft.v1";
    public const string MeetingActionsSchemaId = "meeting_actions_review_draft.v1";
    public const string RoadmapAdjustSchemaId = "project_roadmap_adjustment_draft.v1";
    public const string SkillEvidenceSchemaId = "task_skill_evidence_draft.v1";
    public const string ReceiptSchemaId = "ai_native_action_receipt.v1";
    public const string RendererId = "native-action-review.v1";

    public static readonly IReadOnlySet<string> CapabilityIds = new HashSet<string>(StringComparer.Ordinal)
    {
        ChecklistCapability,
        BreakdownCapability,
        WikiCapability,
        GroupPollCapability,
        DigestCapability,
        MeetingActionsCapability,
        RoadmapAdjustCapability,
        SkillEvidenceCapability
    };
}

public sealed record AiNativeChecklistPayloadDto(
    Guid TaskId,
    string TaskTitle,
    IReadOnlyList<string> Items,
    string SourceRef);

public sealed record AiNativeBreakdownItemDto(
    string Title,
    string? Description,
    string Priority,
    int? EstimatedHours,
    bool DependsOnPrevious = true,
    Guid? RequiredSkillId = null,
    string? RequiredSkillName = null);

public sealed record AiNativeBreakdownSkillOptionDto(Guid SkillId, string Name);

public sealed record AiNativeBreakdownPayloadDto(
    Guid ParentTaskId,
    string ParentTaskTitle,
    IReadOnlyList<AiNativeBreakdownItemDto> Subtasks,
    string SourceRef,
    IReadOnlyList<AiNativeBreakdownSkillOptionDto>? SkillOptions = null);

public sealed record AiNativeWikiPayloadDto(
    Guid WikiPageId,
    Guid ProjectId,
    string WikiTitle,
    string Summary,
    string TaskTitle,
    string TaskDescription,
    bool CreateTask,
    string SourceRef,
    IReadOnlyList<string>? SectionRefs = null,
    IReadOnlyList<AiNativeWikiTaskCandidateDto>? TaskCandidates = null);

public sealed record AiNativeWikiTaskCandidateDto(
    string ClientId,
    string Title,
    string Description,
    bool Selected,
    string SourceRef);

public sealed record AiNativeGroupPollPayloadDto(
    Guid GroupId,
    string GroupName,
    string Question,
    IReadOnlyList<string> Options,
    bool AllowMultiple,
    DateTimeOffset? ExpiredAt,
    string SourceRef);

public sealed record AiNativeDigestPayloadDto(
    Guid ProjectId,
    string ProjectName,
    bool IsEnabled,
    int DayOfWeek,
    int LocalTimeMinutes,
    string TimeZoneId,
    string SourceRef,
    string DeliveryChannel = "email");

public sealed record AiNativeMeetingTaskOptionDto(Guid TaskId, string Title);

public sealed record AiNativeMeetingActionItemDto(
    int ItemIndex,
    string Title,
    string? Description,
    string Priority,
    string? SourceEvidence,
    string MappingMode,
    Guid? ExistingTaskId = null);

public sealed record AiNativeMeetingActionsPayloadDto(
    Guid MeetingImportId,
    Guid ProjectId,
    string MeetingTitle,
    string Summary,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<AiNativeMeetingActionItemDto> ActionItems,
    IReadOnlyList<AiNativeMeetingTaskOptionDto> ExistingTaskOptions,
    string SourceRef);

public sealed record AiNativeRoadmapAdjustmentItemDto(
    Guid? SprintId,
    string SprintName,
    DateTimeOffset BeforeStart,
    DateTimeOffset BeforeEnd,
    DateTimeOffset AfterStart,
    DateTimeOffset AfterEnd,
    bool Selected,
    string Reason);

public sealed record AiNativeRoadmapAdjustmentPayloadDto(
    Guid ProjectId,
    string ProjectName,
    string Summary,
    IReadOnlyList<AiNativeRoadmapAdjustmentItemDto> Adjustments,
    string SourceRef);

public sealed record AiNativeSkillEvidenceCandidateDto(Guid UserId, string Name, bool Selected);
public sealed record AiNativeSkillEvidenceSkillDto(Guid SkillId, string Name, string RequiredLevel);

public sealed record AiNativeSkillEvidencePayloadDto(
    Guid TaskId,
    Guid ProjectId,
    string TaskTitle,
    string TaskRowVersion,
    IReadOnlyList<AiNativeSkillEvidenceCandidateDto> Contributors,
    IReadOnlyList<AiNativeSkillEvidenceSkillDto> Skills,
    IReadOnlyList<string> AcceptanceEvidence,
    string SourceRef);

public sealed record AiNativeActionDraftDto(
    Guid DraftId,
    string CapabilityId,
    string SchemaId,
    string RendererId,
    string TargetType,
    Guid TargetId,
    Guid? ProjectId,
    string Status,
    int Revision,
    string RowVersion,
    JsonElement Payload,
    string SourceVersion,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    AiNativeActionReceiptDto? Receipt = null);

public sealed record ConfirmAiNativeActionRequestDto(
    int ExpectedRevision,
    string RowVersion,
    JsonElement? Payload = null);

public sealed record UpdateAiNativeActionRequestDto(
    int ExpectedRevision,
    string RowVersion,
    JsonElement Payload);

public sealed record RejectAiNativeActionRequestDto(
    int ExpectedRevision,
    string RowVersion);

public sealed record AiNativeActionReceiptItemDto(
    string EntityType,
    Guid EntityId,
    string Label,
    string Url);

public sealed record AiNativeActionReceiptDto(
    string SchemaId,
    Guid ReceiptId,
    Guid DraftId,
    string CapabilityId,
    string Status,
    IReadOnlyList<AiNativeActionReceiptItemDto> Items,
    IReadOnlyList<string> ReadBackLinks,
    DateTimeOffset ConfirmedAt,
    bool Replayed = false);

public sealed record TaskAcceptanceChecklistItemDto(
    Guid Id,
    Guid TaskId,
    string Text,
    int SortOrder,
    bool IsCompleted,
    string RowVersion,
    string Kind = "acceptance");

public sealed record UpdateTaskAcceptanceChecklistItemRequestDto(
    bool IsCompleted,
    string RowVersion);

public sealed record ProjectDigestSubscriptionDto(
    Guid Id,
    Guid ProjectId,
    Guid UserId,
    bool IsEnabled,
    string Cadence,
    int DayOfWeek,
    int LocalTimeMinutes,
    string TimeZoneId,
    DateTimeOffset? NextDeliveryAt,
    DateTimeOffset? LastDeliveryAt,
    string LastDeliveryStatus,
    string? LastError,
    int Revision,
    string RowVersion,
    string? LastDeliveryKey = null,
    DateTimeOffset? LastAttemptAt = null,
    int ConsecutiveFailureCount = 0);
