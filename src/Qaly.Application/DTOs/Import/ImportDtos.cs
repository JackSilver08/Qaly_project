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
    string? SheetName
);

public record ColumnMapping(
    int ColumnIndex,
    string TargetField
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
    int NewLabelsCreated,
    List<string> UnmappedStatuses,
    Dictionary<string, int> StatusDistribution
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
