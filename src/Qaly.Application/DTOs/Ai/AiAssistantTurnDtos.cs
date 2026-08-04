namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantTurnContract
{
    public const string SchemaId = "assistant_turn.v1";
    public const string GroundedReadIntent = "grounded.read.v1";
    public const string ResearchPlanIntent = AiAssistantResearchPlanContract.CapabilityId;
    public const string TaskCreateIntent = "task.create.v1";
    public const string ClarificationIntent = "clarification.v1";
    public const string UnsupportedIntent = "unsupported.v1";
    public const string PolicyBlockedIntent = "policy_blocked.v1";
}

public sealed record AiAssistantClientContextDto(
    string? Route = null,
    string? Module = null,
    Guid? ProjectId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    IReadOnlyList<Guid>? SelectionIds = null);

public sealed record AiAssistantTurnRequestDto(
    string Message,
    AiAssistantClientContextDto? Context = null,
    string Mode = "auto",
    string Language = "vi",
    string ProviderHint = "auto",
    IList<AiChatMessageDto>? History = null,
    IReadOnlyList<ErumiUploadedFileDto>? Files = null,
    Guid? SessionId = null,
    long? ExpectedVersion = null,
    Guid? ClientTurnId = null,
    string? RequestedCapabilityId = null,
    IReadOnlyList<string>? RequestedSourceIds = null);

public sealed record AiAssistantChoiceDto(
    string Id,
    string Label,
    string? Description = null);

public sealed record AiAssistantClarificationDto(
    string QuestionId,
    string Field,
    string Prompt,
    IReadOnlyList<AiAssistantChoiceDto> Choices,
    bool AllowFreeText,
    int Turn,
    int MaxTurns);

public sealed record AiAssistantArtifactDto(
    string Kind,
    string SchemaId,
    string Message,
    Guid ProjectId);

public sealed record AiAssistantTurnResponseDto(
    string SchemaId,
    string Disposition,
    string Intent,
    string ExecutionPolicy,
    string AssistantMessage,
    double Confidence,
    AiAssistantClarificationDto? Clarification,
    AiAssistantArtifactDto? Artifact,
    IReadOnlyList<string> SourceRefs,
    ErumiChatResponseDto? Answer = null,
    Guid? SessionId = null,
    Guid? TurnId = null,
    int? Sequence = null,
    long? SessionVersion = null,
    Guid? ClientTurnId = null,
    string TurnStatus = "completed",
    string? CorrelationId = null,
    bool Replayed = false,
    string ModelProfile = "auto",
    string ActualProvider = "not_reached",
    string ActualModel = "not_reached",
    IReadOnlyList<AiAssistantProcessEventDto>? ProcessEvents = null,
    IReadOnlyList<AiAssistantCapabilityDescriptorDto>? Capabilities = null,
    IReadOnlyList<AiAssistantSourceDisclosureDto>? SourceDisclosures = null,
    AiAssistantResearchPlanDto? ResearchPlan = null,
    AiAssistantGoalAnalysisDto? GoalAnalysis = null,
    AiAssistantWorkPlanDto? WorkPlan = null);

public sealed record CreateAiAssistantSessionRequestDto(
    AiAssistantClientContextDto? Context = null,
    string? Title = null);

public sealed record AiAssistantProcessEventDto(
    int Sequence,
    string Stage,
    string Status,
    string PublicLabel,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt = null,
    int? DurationMs = null,
    bool Retryable = false,
    string? SafeErrorCode = null);

public sealed record AiAssistantStoredTurnDto(
    Guid TurnId,
    int Sequence,
    Guid ClientTurnId,
    string UserMessage,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    AiAssistantTurnResponseDto? Response,
    IReadOnlyList<AiAssistantProcessEventDto> ProcessEvents);

public sealed record AiAssistantSessionDto(
    Guid SessionId,
    string Title,
    string Status,
    long Version,
    Guid? ProjectId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<AiAssistantStoredTurnDto> Turns);
