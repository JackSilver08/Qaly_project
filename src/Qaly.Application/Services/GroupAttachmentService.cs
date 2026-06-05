using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class GroupAttachmentService : IGroupAttachmentService
{
    private const long MaxFileSize = 25 * 1024 * 1024;
    private readonly IRepository<GroupAttachment> _attachmentRepo;
    private readonly IGroupsService _groupsService;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public GroupAttachmentService(
        IRepository<GroupAttachment> attachmentRepo,
        IGroupsService groupsService,
        IFileStorageService fileStorage,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _attachmentRepo = attachmentRepo;
        _groupsService = groupsService;
        _fileStorage = fileStorage;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GroupAttachmentDto>> UploadAsync(
        Guid groupId,
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null || !await _groupsService.CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupAttachmentDto>();
        }

        if (fileSize <= 0 || fileSize > MaxFileSize)
        {
            return Result.Failure<GroupAttachmentDto>("File must be between 1 byte and 25 MB.", 400);
        }

        var safeName = Path.GetFileName(fileName);
        var safeContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
        var storedPath = await _fileStorage.UploadAsync(stream, safeName, safeContentType, ct);
        var attachment = new GroupAttachment
        {
            WorkGroupId = groupId,
            UploadedById = userId.Value,
            FileName = safeName,
            FilePath = storedPath,
            ContentType = safeContentType,
            FileSize = fileSize
        };

        await _attachmentRepo.AddAsync(attachment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Created(ToDto(attachment));
    }

    public async Task<Result<GroupAttachmentDownloadDto>> DownloadAsync(
        Guid groupId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupAttachmentDownloadDto>();
        }

        var attachment = await _attachmentRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == attachmentId && item.WorkGroupId == groupId, ct);
        if (attachment == null)
        {
            return Result.NotFound<GroupAttachmentDownloadDto>("Attachment not found.");
        }

        try
        {
            var stream = await _fileStorage.DownloadAsync(attachment.FilePath, ct);
            return Result.Success(new GroupAttachmentDownloadDto(
                stream,
                attachment.FileName,
                attachment.ContentType));
        }
        catch (FileNotFoundException)
        {
            return Result.NotFound<GroupAttachmentDownloadDto>("Attachment file not found.");
        }
    }

    private static GroupAttachmentDto ToDto(GroupAttachment attachment)
        => new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.FileSize,
            attachment.CreatedAt);
}
