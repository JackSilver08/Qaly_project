namespace Qaly.Application.DTOs.Import;

/// <summary>
/// Kết quả parse file — trả về cho frontend hiển thị mapping UI.
/// </summary>
public record ParsedFileResult(
    string FileName,
    List<string> Headers,
    List<List<string>> PreviewRows,
    int TotalRowCount,
    List<ColumnMappingSuggestion> Suggestions,
    List<string>? SheetNames
);

public record ColumnMappingSuggestion(
    int ColumnIndex,
    string HeaderName,
    string? SuggestedField
);

/// <summary>
/// Frontend gửi lên khi user confirm mapping.
/// </summary>
public record ImportRequest(
    Guid? ProjectId,
    string? NewProjectName,
    List<ColumnMapping> Mappings,
    bool FirstRowIsHeader,
    bool SkipDuplicates,
    string? SheetName,
    Guid? DefaultAssigneeId = null,
    bool AssignToMeIfEmpty = false,
    string? DefaultPriority = null,
    bool EnableAiCategorization = false
);

public record ColumnMapping(
    int ColumnIndex,
    string TargetField
);

/// <summary>
/// Chi tiết dòng bị bỏ qua trong quá trình import
/// </summary>
public record SkippedRowDto(
    int RowIndex,
    string Reason,
    string Category = "Failed"
);

/// <summary>
/// Kết quả import trả về.
/// </summary>
public record ImportResult(
    Guid ImportSessionId,
    Guid ProjectId,
    int TotalRows,
    int ImportedCount,
    int SkippedCount,
    int FailedCount,
    int DuplicateSkippedCount,
    int NewLabelsCreated,
    List<string> UnmappedStatuses,
    Dictionary<string, int> StatusDistribution,
    List<SkippedRowDto> SkippedRows
);

public record ImportSessionDto(
    Guid Id,
    string FileName,
    int ImportedCount,
    int SkippedCount,
    bool IsUndone,
    bool CanUndo,
    DateTimeOffset CreatedAt
);

public record AiCategorizationRequest(
    int RowIndex,
    string Title,
    string? Description
);

public record AiCategorizationResult(
    int RowIndex,
    string Status,
    string Priority,
    string[] Labels
);

public record DocumentImportPreviewResult(
    string FileName,
    string FileType,
    string Title,
    string Description,
    int BlockCount,
    List<string> PreviewBlocks,
    List<string> Warnings
);

public record DocumentImportResult(
    Guid PageId,
    Guid ProjectId,
    string Title,
    int BlockCount,
    List<string> Warnings
);
