namespace Qaly.Application.DTOs.Ai;

public sealed record ErumiChatRequestDto(
    string Message,
    Guid? ProjectId,
    string Mode = "erumi",
    IList<AiChatMessageDto>? History = null,
    IReadOnlyList<ErumiUploadedFileDto>? Files = null,
    string ProviderHint = "auto",
    AiAssistantExecutionContextDto? AuthorizedContext = null,
    bool AdvisoryOnly = false);

public sealed record ErumiChatResponseDto(
    string Reply,
    IReadOnlyList<ErumiMetricDto> Metrics,
    IReadOnlyList<ErumiTableDto> Tables,
    IReadOnlyList<ErumiChartDto> Charts,
    IReadOnlyList<ErumiActionDto> Actions,
    IReadOnlyList<ErumiFileDto> Files,
    IReadOnlyList<string> Sources,
    double Confidence,
    bool UsedAi,
    string Intent,
    int LatencyMs,
    string? ConfidenceReason = null,
    AiModelMetadataDto? Model = null);

public sealed record AiModelMetadataDto(
    string Id,
    string Label,
    string Provider,
    string Status);

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

public sealed record ErumiTableDto(
    string Title,
    IReadOnlyList<ErumiTableColumnDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string? Description = null);

public sealed record ErumiTableColumnDto(
    string Key,
    string Label,
    string Type = "text",
    string Align = "left");

public sealed record ErumiActionDto(
    string Type,
    string Label,
    object? Payload = null,
    bool RequiresConfirmation = false);

public sealed record ErumiFileDto(
    string Label,
    string Format,
    string Url,
    string? Description = null);

public sealed record ErumiUploadedFileDto(
    string FileName,
    string? ContentType,
    long Size,
    IReadOnlyList<string>? Headers = null,
    IReadOnlyList<IReadOnlyList<string>>? PreviewRows = null,
    int? TotalRowCount = null,
    string? Error = null);
