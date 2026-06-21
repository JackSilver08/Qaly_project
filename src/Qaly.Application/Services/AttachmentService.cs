using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Attachment;
using Qaly.Application.Services.Notifications;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IRepository<TaskAttachment> _attachmentRepo;
    private readonly IRepository<PhysicalFile> _physicalFileRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
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
        IRepository<OrganizationMember> organizationMemberRepo,
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
        _organizationMemberRepo = organizationMemberRepo;
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

        if (string.IsNullOrWhiteSpace(fileName) || fileSize <= 0)
        {
            return Result.Failure<TaskAttachmentDto>("A valid file is required.");
        }

        var task = await LoadTaskAsync(taskItemId, ct);
        if (task == null)
        {
            return Result.NotFound<TaskAttachmentDto>();
        }

        if (!await CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskAttachmentDto>();
        }

        // Copy content stream to MemoryStream to calculate hash and seek
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Calculate SHA-256 hash
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(ms);
        var contentHash = Convert.ToHexStringLower(hashBytes);
        ms.Position = 0;

        // Check if there is an existing physical file with the same hash
        var physicalFile = await _physicalFileRepo.GetQueryable()
            .FirstOrDefaultAsync(f => f.ContentHash == contentHash, ct);

        if (physicalFile != null)
        {
            physicalFile.ReferenceCount++;
            await _physicalFileRepo.UpdateAsync(physicalFile, ct);
        }
        else
        {
            var storedPath = await _fileStorageService.UploadAsync(ms, fileName, contentType, ct);
            physicalFile = new PhysicalFile
            {
                ContentHash = contentHash,
                FilePath = storedPath,
                FileSize = fileSize,
                ReferenceCount = 1
            };
            await _physicalFileRepo.AddAsync(physicalFile, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var attachment = new TaskAttachment
        {
            TaskItemId = taskItemId,
            Scope = "Task",
            UploadedById = currentUserId.Value,
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType,
            PhysicalFileId = physicalFile.Id,
            PhysicalFile = physicalFile
        };

        await _attachmentRepo.AddAsync(attachment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(TaskAttachment), attachment.Id.ToString(), new { taskItemId, attachment.FileName, contentHash }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
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

        attachment.EvidenceApprovalStatus = approve ? "Approved" : "Rejected";
        attachment.EvidenceReviewedById = currentUserId.Value;
        attachment.EvidenceReviewedAt = DateTimeOffset.UtcNow;
        attachment.EvidenceReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();

        await _attachmentRepo.UpdateAsync(attachment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
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
            .Include(item => item.TaskItem)
                .ThenInclude(task => task!.Project)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (attachment == null)
        {
            return Result.Failure("Attachment was not found.", 404);
        }

        if (attachment.TaskItem == null || !await CanAccessTaskAsync(attachment.TaskItem, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        await _attachmentRepo.DeleteAsync(attachment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(TaskAttachment), id.ToString(), new { attachment.TaskItemId, attachment.FileName }, ct);

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

        var recipientIds = new[] { attachment.UploadedById, task.ReporterId }
            .Concat(task.AssigneeId.HasValue ? [task.AssigneeId.Value] : [])
            .Concat(task.Assignees.Select(assignment => assignment.UserId))
            .Where(userId => userId != reviewerId)
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
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (task.Project == null)
        {
            return false;
        }

        if (IsAdmin() ||
            task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Project.OwnerId == currentUserId)
        {
            return true;
        }

        if (task.IsPrivate)
        {
            return false;
        }

        return await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == task.ProjectId && member.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (task.Project == null)
        {
            return false;
        }

        if (IsAdmin() ||
            task.Project.OwnerId == currentUserId ||
            task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId))
        {
            return true;
        }

        var canManageProject = await _memberRepo.GetQueryable()
            .AnyAsync(member =>
                member.ProjectId == task.ProjectId &&
                member.UserId == currentUserId &&
                ProjectRoleRules.CanManageProject(member.Role), ct);
        if (canManageProject)
        {
            return true;
        }

        if (!task.Project.OrganizationId.HasValue)
        {
            return false;
        }

        var organizationRole = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == task.Project.OrganizationId.Value && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return ProjectRoleRules.CanManageProject(organizationRole);
    }

    private async Task<bool> CanReviewEvidenceAsync(TaskItem task, Guid currentUserId, CancellationToken ct)
    {
        var project = task.Project;
        if (project == null)
        {
            return false;
        }

        if (IsAdmin() || project.OwnerId == currentUserId)
        {
            return true;
        }

        var projectRole = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == task.ProjectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        if (ProjectRoleRules.CanManageProject(projectRole))
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        var organizationRole = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return ProjectRoleRules.CanManageProject(organizationRole);
    }

    private bool IsAdmin()
        => string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<IReadOnlyList<DuplicateFileDto>>> GetDuplicatesAsync(CancellationToken ct = default)
    {
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
                continue;
            }
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
        var physicalFiles = await _physicalFileRepo.GetQueryable().ToListAsync(ct);
        var filesByActualHash = new Dictionary<string, List<PhysicalFile>>(StringComparer.OrdinalIgnoreCase);

        int totalProcessed = 0;
        int totalMerged = 0;
        int totalHashUpdates = 0;
        long bytesSaved = 0;

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

                original.ReferenceCount += attachmentsToUpdate.Count;
                await _physicalFileRepo.UpdateAsync(original, ct);

                try
                {
                    await _fileStorageService.DeleteAsync(duplicate.FilePath, ct);
                }
                catch
                {
                    // Ignore storage deletion errors
                }

                bytesSaved += duplicate.FileSize;
                await _physicalFileRepo.HardDeleteAsync(duplicate, ct);
                totalMerged++;
            }
        }

        if (totalMerged > 0 || totalHashUpdates > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return Result.Success(new DeduplicateResultDto(totalProcessed, totalMerged, bytesSaved));
    }

    private static async Task<string> ComputeSha256Async(Stream content, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content, ct);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static string FormatFileSize(long size)
        => size < 1024 * 1024
            ? $"{(size / 1024.0):F1} KB"
            : $"{(size / (1024.0 * 1024.0)):F1} MB";
}
