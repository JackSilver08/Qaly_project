namespace Qaly.Application.DTOs.Ai;

public sealed record ErumiChatRequestDto(
    string Message,
    Guid? ProjectId,
    string Mode = "erumi",
    IList<AiChatMessageDto>? History = null);

public sealed record ErumiChatResponseDto(
    string Reply,
    IReadOnlyList<ErumiMetricDto> Metrics,
    IReadOnlyList<ErumiChartDto> Charts,
    IReadOnlyList<ErumiActionDto> Actions,
    IReadOnlyList<string> Sources,
    bool UsedAi,
    string Intent,
    int LatencyMs);

public sealed record ErumiMetricDto(
    string Label,
    string Value,
    string? Tone = null,
    string? Hint = null);

public sealed record ErumiChartDto(
    string Type,
    string Title,
    IReadOnlyList<string> Labels,
    IReadOnlyList<double> Values,
    string? Unit = null);

public sealed record ErumiActionDto(
    string Type,
    string Label,
    object? Payload = null,
    bool RequiresConfirmation = false);
