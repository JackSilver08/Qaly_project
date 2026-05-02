namespace Qaly.Application.DTOs.Attachment;

public record TaskAttachmentDto(
    Guid Id,
    string FileName,
    string FilePath,
    long FileSize,
    string? ContentType,
    Guid TaskItemId,
    Guid UploadedById,
    string UploadedByName,
    DateTimeOffset UploadedAt);
