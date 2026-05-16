using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Import;

namespace Qaly.Application.Common.Interfaces;

/// <summary>
/// Service interface for CSV/XLSX import into Kanban board.
/// Supports parse-preview, execute-import, undo, and session history.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Bước 1: Parse file và trả về preview + gợi ý mapping.
    /// </summary>
    Task<Result<ParsedFileResult>> ParseFileAsync(Stream fileStream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Bước 2: Thực hiện import với mapping đã confirm.
    /// </summary>
    Task<Result<ImportResult>> ExecuteImportAsync(Stream fileStream, string fileName, ImportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Undo một import session (xóa tất cả task đã import).
    /// </summary>
    Task<Result<int>> UndoImportAsync(Guid importSessionId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách import sessions của 1 project.
    /// </summary>
    Task<Result<List<ImportSessionDto>>> GetSessionsAsync(Guid projectId, CancellationToken ct = default);
}
