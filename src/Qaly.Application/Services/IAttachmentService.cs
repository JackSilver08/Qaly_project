using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Attachment;

namespace Qaly.Application.Services;

public interface IAttachmentService
{
    Task<Result<IReadOnlyList<TaskAttachmentDto>>> GetByTaskAsync(Guid taskItemId, CancellationToken ct = default);
    Task<Result<TaskAttachmentDto>> UploadAsync(Guid taskItemId, string fileName, string contentType, long fileSize, Stream content, CancellationToken ct = default);
    Task<Result<TaskAttachmentDto>> MarkAsEvidenceAsync(Guid id, bool isEvidence, CancellationToken ct = default);
    Task<Result<TaskAttachmentDto>> ReviewEvidenceAsync(Guid id, bool approve, string? reviewNote, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
