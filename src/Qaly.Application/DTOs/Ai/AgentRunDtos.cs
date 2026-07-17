namespace Qaly.Application.DTOs.Ai;

public sealed record StartAgentRunDto(Guid ProjectId, string Goal);

public sealed record ApproveAgentRunDto(
    string Action = "execute_action",
    string? EditedPayloadJson = null,
    string? Note = null);

public sealed record AgentRunEventDto(
    string Type,
    string Message,
    DateTimeOffset At,
    int Progress);

public sealed record AgentRunDto(
    Guid Id,
    Guid ProjectId,
    string Goal,
    string Status,
    int Progress,
    string CurrentStep,
    bool RequiresApproval,
    Guid? DraftId,
    IReadOnlyList<AgentRunEventDto> Events,
    string? Error = null);
