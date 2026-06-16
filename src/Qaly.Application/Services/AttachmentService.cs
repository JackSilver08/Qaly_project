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

        var storedPath = await _fileStorageService.UploadAsync(content, fileName, contentType, ct);
        var attachment = new TaskAttachment
        {
            TaskItemId = taskItemId,
            Scope = "Task",
            UploadedById = currentUserId.Value,
            FileName = Path.GetFileName(fileName),
            FilePath = storedPath,
            FileSize = fileSize,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType
        };

        await _attachmentRepo.AddAsync(attachment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(TaskAttachment), attachment.Id.ToString(), new { taskItemId, attachment.FileName }, ct);

        var saved = await _attachmentRepo.GetQueryable()
            .Include(item => item.UploadedBy)
            .Include(item => item.EvidenceReviewedBy)
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

        await _fileStorageService.DeleteAsync(attachment.FilePath, ct);
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
        if (task.Project != null && (IsAdmin() || task.Project.OwnerId == currentUserId))
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

    private bool IsAdmin()
        => string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
}
