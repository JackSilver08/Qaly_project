using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Attachment;
using Qaly.Application.Services.Notifications;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class AttachmentService : IAttachmentService
{
    private const long MaxUploadBytes = 25_000_000;
    private readonly IRepository<TaskAttachment> _attachmentRepo;
    private readonly IRepository<PhysicalFile> _physicalFileRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly IProjectRoleCatalog _roleCatalog;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;

    public AttachmentService(
        IRepository<TaskAttachment> attachmentRepo,
        IRepository<PhysicalFile> physicalFileRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        ITaskAccessPolicy taskAccessPolicy,
        IProjectRoleCatalog roleCatalog,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService)
    {
        _attachmentRepo = attachmentRepo;
        _physicalFileRepo = physicalFileRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _taskAccessPolicy = taskAccessPolicy;
        _roleCatalog = roleCatalog;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<Result<IReadOnlyList<TaskAttachmentDto>>> GetByTaskAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskItemId, ct);
        if (task == null)
        {
            return Result.NotFound<IReadOnlyList<TaskAttachmentDto>>();
        }

        if (!await CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden<IReadOnlyList<TaskAttachmentDto>>();
        }

        var attachments = await _attachmentRepo.GetQueryable()
            .AsNoTracking()
            .Include(attachment => attachment.UploadedBy)
            .Include(attachment => attachment.EvidenceReviewedBy)
            .Include(attachment => attachment.PhysicalFile)
            .Where(attachment => attachment.TaskItemId == taskItemId)
            .OrderByDescending(attachment => attachment.UploadedAt)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<TaskAttachmentDto>>(attachments.Select(attachment => attachment.ToDto()).ToList());
    }

    public async Task<Result<TaskAttachmentDto>> UploadAsync(Guid taskItemId, string fileName, string contentType, long fileSize, Stream content, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<TaskAttachmentDto>();
        }

        var safeFileName = Path.GetFileName(fileName?.Trim());
        var safeContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
        if (string.IsNullOrWhiteSpace(safeFileName) || fileSize <= 0)
        {
            return Result.Failure<TaskAttachmentDto>("A valid file is required.");
        }
        if (safeFileName.Length > 500)
            return Result.Failure<TaskAttachmentDto>("File name cannot exceed 500 characters.", 400);
        if (safeContentType?.Length > 100)
            return Result.Failure<TaskAttachmentDto>("Content type cannot exceed 100 characters.", 400);
        if (fileSize > MaxUploadBytes)
            return Result.Failure<TaskAttachmentDto>("File cannot exceed 25 MB.", 413);

        var task = await LoadTaskAsync(taskItemId, ct);
        if (task == null)
        {
            return Result.NotFound<TaskAttachmentDto>();
        }

        if (!await _taskAccessPolicy.CanContributeToTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskAttachmentDto>();
        }

        // Copy through a bounded buffer. The caller-provided metadata is never authoritative for
        // quota, deduplication or storage accounting.
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long actualSize = 0;
        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) break;
            actualSize += read;
            if (actualSize > MaxUploadBytes)
                return Result.Failure<TaskAttachmentDto>("File cannot exceed 25 MB.", 413);
            await ms.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (actualSize <= 0)
            return Result.Failure<TaskAttachmentDto>("A valid file is required.", 400);
        if (actualSize != fileSize)
            return Result.Failure<TaskAttachmentDto>("File size metadata does not match the uploaded content.", 400);
        ms.Position = 0;

        // Calculate SHA-256 hash
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(ms);
        var contentHash = Convert.ToHexStringLower(hashBytes);
        ms.Position = 0;

        // Check if there is an existing physical file with the same hash
        var physicalFile = await _physicalFileRepo.GetQueryable()
            .FirstOrDefaultAsync(f => f.ContentHash == contentHash, ct);

        string? newlyStoredPath = null;
        if (physicalFile != null)
        {
            physicalFile.ReferenceCount++;
            await _physicalFileRepo.UpdateAsync(physicalFile, ct);
        }
        else
        {
            var storedPath = await _fileStorageService.UploadAsync(ms, safeFileName, safeContentType ?? "application/octet-stream", ct);
            newlyStoredPath = storedPath;
            physicalFile = new PhysicalFile
            {
                ContentHash = contentHash,
                FilePath = storedPath,
                FileSize = actualSize,
                ReferenceCount = 1
            };
            await _physicalFileRepo.AddAsync(physicalFile, ct);
        }

        var attachment = new TaskAttachment
        {
            TaskItemId = taskItemId,
            Scope = "Task",
            UploadedById = currentUserId.Value,
            FileName = safeFileName,
            ContentType = safeContentType,
            PhysicalFileId = physicalFile.Id,
            PhysicalFile = physicalFile
        };

        try
        {
            await _attachmentRepo.AddAsync(attachment, ct);
            // PhysicalFile/reference count, logical attachment and audit evidence are one database commit.
            await _unitOfWork.SaveChangesWithAuditAsync(
                _auditLogService,
                "Create",
                nameof(TaskAttachment),
                attachment.Id.ToString(),
                new { taskItemId, attachment.FileName, contentHash },
                ct);
        }
        catch
        {
            if (newlyStoredPath != null)
            {
                try
                {
                    await _fileStorageService.DeleteAsync(newlyStoredPath, CancellationToken.None);
                }
                catch
                {
                    // Preserve the authoritative database error. Storage reconciliation can remove
                    // an unreferenced blob; never turn the failed database write into a success.
                }
            }
            throw;
        }
        var saved = await _attachmentRepo.GetQueryable()
            .Include(item => item.UploadedBy)
            .Include(item => item.EvidenceReviewedBy)
            .Include(item => item.PhysicalFile)
            .FirstAsync(item => item.Id == attachment.Id, ct);

        return Result.Created(saved.ToDto());
    }

    public async Task<Result<TaskAttachmentDto>> MarkAsEvidenceAsync(Guid id, bool isEvidence, CancellationToken ct = default)
    {
        var attachment = await LoadAttachmentAsync(id, ct);
        if (attachment == null)
        {
            return Result.NotFound<TaskAttachmentDto>();
        }

        if (attachment.TaskItem == null || !await CanManageTaskAsync(attachment.TaskItem, ct))
        {
            return Result.Forbidden<TaskAttachmentDto>();
        }

        attachment.IsEvidence = isEvidence;
        if (isEvidence)
        {
            attachment.EvidenceApprovalStatus = "Pending";
        }
        else
        {
            attachment.EvidenceApprovalStatus = "None";
            attachment.EvidenceReviewedById = null;
            attachment.EvidenceReviewedAt = null;
            attachment.EvidenceReviewNote = null;
        }

        await _attachmentRepo.UpdateAsync(attachment, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "UpdateEvidenceFlag",
            nameof(TaskAttachment),
            attachment.Id.ToString(),
            new { attachment.TaskItemId, attachment.IsEvidence, attachment.EvidenceApprovalStatus },
            ct);

        return Result.Success(attachment.ToDto());
    }

    public async Task<Result<TaskAttachmentDto>> ReviewEvidenceAsync(Guid id, bool approve, string? reviewNote, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<TaskAttachmentDto>();
        }

        var attachment = await LoadAttachmentAsync(id, ct);
        if (attachment == null)
        {
            return Result.NotFound<TaskAttachmentDto>();
        }

        if (attachment.TaskItem == null || !await CanReviewEvidenceAsync(attachment.TaskItem, currentUserId.Value, ct))
        {
            return Result.Forbidden<TaskAttachmentDto>();
        }

        if (!attachment.IsEvidence)
        {
            return Result.Failure<TaskAttachmentDto>("Attachment is not marked as evidence.", 400);
        }

        if (reviewNote?.Trim().Length > 1000)
        {
            return Result.Failure<TaskAttachmentDto>("Review note cannot exceed 1,000 characters.", 400);
        }

        attachment.EvidenceApprovalStatus = approve ? "Approved" : "Rejected";
        attachment.EvidenceReviewedById = currentUserId.Value;
        attachment.EvidenceReviewedAt = DateTimeOffset.UtcNow;
        attachment.EvidenceReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();

        await _attachmentRepo.UpdateAsync(attachment, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "ReviewEvidence",
            nameof(TaskAttachment),
            attachment.Id.ToString(),
            new
            {
                attachment.TaskItemId,
                attachment.EvidenceApprovalStatus,
                attachment.EvidenceReviewedById,
                attachment.EvidenceReviewNote
            },
            ct);

        await NotifyEvidenceReviewedAsync(attachment, approve, currentUserId.Value, ct);

        return Result.Success(attachment.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await _attachmentRepo.GetQueryable()
            .Include(item => item.PhysicalFile)
            .Include(item => item.TaskItem)
                .ThenInclude(task => task!.Project)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (attachment == null)
        {
            return Result.Failure("Attachment was not found.", 404);
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null || attachment.TaskItem == null ||
            !await CanAccessTaskAsync(attachment.TaskItem, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var canDeleteOwn = attachment.UploadedById == currentUserId &&
            await _taskAccessPolicy.CanContributeToTaskAsync(attachment.TaskItem, ct);
        var canManageProject = await _taskAccessPolicy.CanManageProjectAsync(
            attachment.TaskItem.ProjectId,
            attachment.TaskItem.Project.OwnerId,
            ct);
        if (!canDeleteOwn && !canManageProject)
        {
            return Result.Failure("Access denied.", 403);
        }

        await _attachmentRepo.DeleteAsync(attachment, ct);
        if (attachment.PhysicalFile != null)
        {
            attachment.PhysicalFile.ReferenceCount = Math.Max(0, attachment.PhysicalFile.ReferenceCount - 1);
            await _physicalFileRepo.UpdateAsync(attachment.PhysicalFile, ct);
        }
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Delete",
            nameof(TaskAttachment),
            id.ToString(),
            new { attachment.TaskItemId, attachment.FileName },
            ct);

        return Result.Success();
    }

    private async Task<TaskItem?> LoadTaskAsync(Guid taskItemId, CancellationToken ct)
        => await _taskRepo.GetQueryable()
            .Include(task => task.Project)
                .ThenInclude(project => project.Organization)
            .Include(task => task.Assignees)
            .FirstOrDefaultAsync(task => task.Id == taskItemId, ct);

    private async Task<TaskAttachment?> LoadAttachmentAsync(Guid attachmentId, CancellationToken ct)
        => await _attachmentRepo.GetQueryable()
            .Include(item => item.PhysicalFile)
            .Include(item => item.TaskItem)
                .ThenInclude(task => task!.Project)
                    .ThenInclude(project => project.Organization)
            .Include(item => item.TaskItem)
                .ThenInclude(task => task!.Assignees)
            .Include(item => item.TaskItem)
                .ThenInclude(task => task!.Reporter)
            .Include(item => item.TaskItem)
                .ThenInclude(task => task!.Assignee)
            .Include(item => item.UploadedBy)
            .Include(item => item.EvidenceReviewedBy)
            .FirstOrDefaultAsync(item => item.Id == attachmentId, ct);

    private async Task NotifyEvidenceReviewedAsync(TaskAttachment attachment, bool approved, Guid reviewerId, CancellationToken ct)
    {
        var task = attachment.TaskItem;
        if (task == null)
        {
            return;
        }

        var currentProjectMemberIds = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == task.ProjectId)
            .Select(member => member.UserId)
            .ToHashSetAsync(ct);

        bool IsCurrentProjectParticipant(Guid userId)
            => task.Project.OwnerId == userId || currentProjectMemberIds.Contains(userId);

        bool CanReceivePrivateTaskNotification(Guid userId)
            => !task.IsPrivate ||
               task.Project.OwnerId == userId ||
               task.ReporterId == userId ||
               task.AssigneeId == userId ||
               task.Assignees.Any(assignment => assignment.UserId == userId);

        var recipientIds = new[] { attachment.UploadedById, task.ReporterId }
            .Concat(task.AssigneeId.HasValue ? [task.AssigneeId.Value] : [])
            .Concat(task.Assignees.Select(assignment => assignment.UserId))
            .Where(userId => userId != reviewerId)
            .Where(IsCurrentProjectParticipant)
            .Where(CanReceivePrivateTaskNotification)
            .Distinct()
            .ToList();

        foreach (var recipientId in recipientIds)
        {
            var template = NotificationTemplates.EvidenceReviewed(task.Id, attachment.Id, task.Title, approved, recipientId);
            await _notificationService.CreateAsync(
                recipientId,
                template.Message,
                template.Type,
                template.Tone,
                task.Id,
                nameof(TaskItem),
                template.IdempotencyKey,
                ct);
        }
    }

    private async Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct)
        => await _taskAccessPolicy.CanAccessTaskAsync(task, ct);

    private async Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct)
        => await _taskAccessPolicy.CanManageTaskAsync(task, ct);

    private async Task<bool> CanReviewEvidenceAsync(TaskItem task, Guid currentUserId, CancellationToken ct)
    {
        var project = task.Project;
        if (project == null)
        {
            return false;
        }

        if (!await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
        {
            return false;
        }

        if (await _taskAccessPolicy.CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return true;
        }

        var projectRole = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == task.ProjectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var resolvedRole = await _roleCatalog.ResolveAsync(projectRole, project.OrganizationId, ct);
        return resolvedRole != null && ProjectPermissionRules.Resolve(
            resolvedRole.BaseRole,
            isOwner: false,
            isSystemAdmin: false).CanReviewEvidence;
    }

    public async Task<Result<IReadOnlyList<DuplicateFileDto>>> GetDuplicatesAsync(CancellationToken ct = default)
    {
        if (!SystemRoleRules.IsAdmin(_currentUserService.Role))
            return Result.Forbidden<IReadOnlyList<DuplicateFileDto>>();

        var attachments = await _attachmentRepo.GetQueryable()
            .Include(a => a.PhysicalFile)
            .ToListAsync(ct);

        var list = new List<DuplicateFileDto>();
        var reportedAttachmentIds = new HashSet<Guid>();

        var groups = attachments
            .GroupBy(a => a.PhysicalFileId)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(a => a.UploadedAt).ToList();
            var original = sorted.First();
            var duplicates = sorted.Skip(1);

            foreach (var dup in duplicates)
            {
                var size = original.PhysicalFile?.FileSize ?? 0;
                var sizeLabel = size < 1024 * 1024
                    ? $"{(size / 1024.0):F1} KB"
                    : $"{(size / (1024.0 * 1024.0)):F1} MB";

                list.Add(new DuplicateFileDto(
                    dup.Id,
                    dup.FileName,
                    sizeLabel,
                    original.PhysicalFile?.FilePath ?? string.Empty,
                    original.FileName
                ));
                reportedAttachmentIds.Add(dup.Id);
            }
        }

        var physicalFiles = attachments
            .Where(a => a.PhysicalFile != null)
            .Select(a => a.PhysicalFile!)
            .DistinctBy(file => file.Id)
            .ToList();
        var physicalFilesByActualHash = new Dictionary<string, List<PhysicalFile>>(StringComparer.OrdinalIgnoreCase);
        var scanFailures = 0;

        foreach (var file in physicalFiles)
        {
            try
            {
                await using var content = await _fileStorageService.DownloadAsync(file.FilePath, ct);
                var actualHash = await ComputeSha256Async(content, ct);

                if (!physicalFilesByActualHash.TryGetValue(actualHash, out var hashGroup))
                {
                    hashGroup = [];
                    physicalFilesByActualHash[actualHash] = hashGroup;
                }

                hashGroup.Add(file);
            }
            catch
            {
                scanFailures++;
            }
        }

        if (scanFailures > 0)
        {
            return Result.Failure<IReadOnlyList<DuplicateFileDto>>(
                $"Không thể kiểm tra đầy đủ file trùng lặp vì {scanFailures} file vật lý không đọc được.",
                503);
        }

        foreach (var hashGroup in physicalFilesByActualHash.Values.Where(group => group.Count > 1))
        {
            var originalFile = hashGroup
                .OrderBy(file => file.CreatedAt)
                .First();
            var originalAttachment = attachments
                .Where(attachment => attachment.PhysicalFileId == originalFile.Id)
                .OrderBy(attachment => attachment.UploadedAt)
                .FirstOrDefault();

            if (originalAttachment == null)
            {
                continue;
            }

            foreach (var duplicateFile in hashGroup.Where(file => file.Id != originalFile.Id))
            {
                var duplicateAttachments = attachments
                    .Where(attachment => attachment.PhysicalFileId == duplicateFile.Id)
                    .OrderBy(attachment => attachment.UploadedAt);

                foreach (var duplicateAttachment in duplicateAttachments)
                {
                    if (!reportedAttachmentIds.Add(duplicateAttachment.Id))
                    {
                        continue;
                    }

                    list.Add(new DuplicateFileDto(
                        duplicateAttachment.Id,
                        duplicateAttachment.FileName,
                        FormatFileSize(duplicateFile.FileSize),
                        duplicateFile.FilePath,
                        originalAttachment.FileName));
                }
            }
        }

        return Result.Success<IReadOnlyList<DuplicateFileDto>>(list);
    }

    public async Task<Result<DeduplicateResultDto>> DeduplicateAsync(CancellationToken ct = default)
    {
        if (!SystemRoleRules.IsAdmin(_currentUserService.Role))
            return Result.Forbidden<DeduplicateResultDto>();

        var physicalFiles = await _physicalFileRepo.GetQueryable().ToListAsync(ct);
        var filesByActualHash = new Dictionary<string, List<PhysicalFile>>(StringComparer.OrdinalIgnoreCase);

        int totalProcessed = 0;
        int totalMerged = 0;
        int totalHashUpdates = 0;
        int scanFailures = 0;
        int cleanupFailures = 0;
        long bytesSaved = 0;
        var warnings = new List<string>();
        var obsoleteFiles = new List<PhysicalFile>();

        foreach (var file in physicalFiles)
        {
            string actualHash;
            try
            {
                await using var content = await _fileStorageService.DownloadAsync(file.FilePath, ct);
                actualHash = await ComputeSha256Async(content, ct);
            }
            catch
            {
                scanFailures++;
                continue;
            }

            totalProcessed++;
            if (!filesByActualHash.TryGetValue(actualHash, out var group))
            {
                group = [];
                filesByActualHash[actualHash] = group;
            }

            group.Add(file);
        }

        foreach (var hashGroup in filesByActualHash)
        {
            var sorted = hashGroup.Value
                .OrderBy(f => string.Equals(f.ContentHash, hashGroup.Key, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(f => f.CreatedAt)
                .ToList();

            var original = sorted.First();
            var canonicalReferenceCount = await _attachmentRepo.GetQueryable()
                .IgnoreQueryFilters()
                .CountAsync(attachment => attachment.PhysicalFileId == original.Id, ct);
            if (!string.Equals(original.ContentHash, hashGroup.Key, StringComparison.OrdinalIgnoreCase))
            {
                original.ContentHash = hashGroup.Key;
                await _physicalFileRepo.UpdateAsync(original, ct);
                totalHashUpdates++;
            }

            foreach (var duplicate in sorted.Skip(1))
            {
                var attachmentsToUpdate = await _attachmentRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(a => a.PhysicalFileId == duplicate.Id)
                    .ToListAsync(ct);

                foreach (var att in attachmentsToUpdate)
                {
                    att.PhysicalFileId = original.Id;
                    att.PhysicalFile = original;
                    await _attachmentRepo.UpdateAsync(att, ct);
                }

                canonicalReferenceCount += attachmentsToUpdate.Count;
                await _physicalFileRepo.HardDeleteAsync(duplicate, ct);
                obsoleteFiles.Add(duplicate);
                totalMerged++;
            }

            original.ReferenceCount = canonicalReferenceCount;
            await _physicalFileRepo.UpdateAsync(original, ct);
        }

        if (totalMerged > 0 || totalHashUpdates > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // Delete blobs only after the canonical database graph commits. A storage failure now leaves
        // an orphan eligible for reconciliation, never a live attachment pointing at a missing file.
        foreach (var obsoleteFile in obsoleteFiles)
        {
            try
            {
                await _fileStorageService.DeleteAsync(obsoleteFile.FilePath, CancellationToken.None);
                bytesSaved += obsoleteFile.FileSize;
            }
            catch
            {
                cleanupFailures++;
                warnings.Add($"File vật lý {obsoleteFile.Id} đã hợp nhất trong dữ liệu nhưng chưa xóa được khỏi storage.");
            }
        }

        if (scanFailures > 0)
        {
            warnings.Add($"Bỏ qua {scanFailures} file vật lý không đọc được; các file này chưa được kết luận là trùng lặp.");
        }

        return Result.Success(new DeduplicateResultDto(
            totalProcessed,
            totalMerged,
            bytesSaved,
            scanFailures,
            cleanupFailures,
            warnings));
    }

    private static async Task<string> ComputeSha256Async(Stream content, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content, ct);
        return Convert.ToHexStringLower(hashBytes);
    }

    public async Task<Result<StorageStatsDto>> GetStorageStatsAsync(CancellationToken ct = default)
    {
        if (!SystemRoleRules.IsAdmin(_currentUserService.Role))
            return Result.Forbidden<StorageStatsDto>();

        var files = await _physicalFileRepo.GetQueryable()
            .AsNoTracking()
            .Where(file => file.ReferenceCount > 0)
            .ToListAsync(ct);
        long totalBytes = files.Sum(f => f.FileSize);
        int totalFileCount = files.Count;

        // Find how many files belong to archived projects
        // We'll join TaskAttachments with their Projects to filter by status
        var archivedProjectFilesQuery = _attachmentRepo.GetQueryable()
            .AsNoTracking()
            .Include(a => a.Project)
            .Include(a => a.TaskItem)
            .ThenInclude(t => t!.Project)
            .Where(a => (a.Project != null && a.Project.Status == "Archived") || 
                        (a.TaskItem != null && a.TaskItem.Project != null && a.TaskItem.Project.Status == "Archived"));

        var archivedProjectAttachments = await archivedProjectFilesQuery.ToListAsync(ct);
        var archivedPhysicalFileIds = archivedProjectAttachments.Select(a => a.PhysicalFileId).Distinct().ToList();
        
        long archivedBytes = files.Where(f => archivedPhysicalFileIds.Contains(f.Id)).Sum(f => f.FileSize);

        return Result.Success(new StorageStatsDto(
            totalBytes,
            totalBytes, // For demo simplicity, we set AttachmentBytes to TotalBytes since attachments are the primary storage usage
            totalFileCount,
            archivedPhysicalFileIds.Count
        ));
    }

    private static string FormatFileSize(long size)
        => size < 1024 * 1024
            ? $"{(size / 1024.0):F1} KB"
            : $"{(size / (1024.0 * 1024.0)):F1} MB";
}
