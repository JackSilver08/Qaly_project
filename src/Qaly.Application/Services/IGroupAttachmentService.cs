using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;

namespace Qaly.Application.Services;

public interface IGroupAttachmentService
{
    Task<Result<GroupAttachmentDto>> UploadAsync(
        Guid groupId,
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken ct = default);

    Task<Result<GroupAttachmentDownloadDto>> DownloadAsync(
        Guid groupId,
        Guid attachmentId,
        CancellationToken ct = default);
}
