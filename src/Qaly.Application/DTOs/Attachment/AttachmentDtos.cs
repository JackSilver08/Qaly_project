namespace Qaly.Application.DTOs.Attachment;

public record TaskAttachmentDto(
    Guid Id,
    string FileName,
    string FilePath,
    long FileSize,
    string? ContentType,
    string Scope,
    Guid? ProjectId,
    Guid? TaskItemId,
    Guid? CommentId,
    Guid UploadedById,
    string UploadedByName,
    DateTimeOffset UploadedAt,
    bool IsEvidence,
    string EvidenceApprovalStatus,
    Guid? EvidenceReviewedById,
    string? EvidenceReviewedByName,
    DateTimeOffset? EvidenceReviewedAt,
    string? EvidenceReviewNote,
    string? ContentHash = null);

public record DuplicateFileDto(
    Guid Id,
    string Name,
    string Size,
    string Path,
    string DuplicateOf);

public record DeduplicateResultDto(
    int TotalProcessed,
    int TotalMerged,
    long BytesSaved);
