using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Import;

namespace Qaly.Application.Common.Interfaces;

public interface IFileImportService
{
    Task<Result<DocumentImportPreviewResult>> PreviewDocumentAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);

    Task<Result<DocumentImportResult>> ImportDocumentAsync(
        Guid projectId,
        Stream fileStream,
        string fileName,
        string? title = null,
        CancellationToken ct = default);

    Task<Result<ZipBundlePreviewResult>> PreviewZipBundleAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);

    Task<Result<ZipBundleImportResult>> ImportZipBundleAsync(
        Guid projectId,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);
}
