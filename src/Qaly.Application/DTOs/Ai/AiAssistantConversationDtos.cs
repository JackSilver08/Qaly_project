namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantConversationContract
{
    public const string SchemaId = "assistant_conversation_turn.v2";
    public const string GuidanceSchemaId = "assistant_manual_guidance.v1";
    public const string PromptVersion = "assistant_answer_first.v1";
}

public sealed record AiAssistantQuickReplyDto(
    string Value,
    string Label,
    string? Description = null);

public sealed record AiAssistantConversationQuestionDto(
    string Id,
    string Text,
    bool Blocking,
    string Reason,
    IReadOnlyList<AiAssistantQuickReplyDto> QuickReplies,
    bool AllowFreeText = true,
    string InputType = "text",
    string? Placeholder = null);

public sealed record AiAssistantManualGuidanceStepDto(
    int Sequence,
    string Label,
    string Route,
    string RequiredPermission);

public sealed record AiAssistantManualGuidanceDto(
    string SchemaId,
    bool Temporary,
    string Summary,
    IReadOnlyList<AiAssistantManualGuidanceStepDto> Steps);

public sealed record AiAssistantCapabilityGapDto(
    string CapabilityId,
    bool ExecutionUnavailable,
    string UserMessage,
    string InternalReasonCode);

public sealed record AiAssistantConversationTurnDto(
    string SchemaId,
    string ConversationDisposition,
    string ActionDisposition,
    string Answer,
    IReadOnlyList<AiAssistantConversationQuestionDto> Questions,
    AiAssistantManualGuidanceDto? Guidance,
    AiAssistantCapabilityGapDto? CapabilityGap,
    IReadOnlyList<string> ProposedActions,
    IReadOnlyList<string> Sources,
    double Confidence,
    string ActualProvider,
    string ActualModel,
    string PromptVersion = AiAssistantConversationContract.PromptVersion);
