using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Comment;
using Qaly.Application.Services.Notifications;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text.Json;

namespace Qaly.Application.Services;

public class CommentService : ICommentService
{
    private readonly IRepository<TaskComment> _commentRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<VectorSyncOutbox> _outboxRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public CommentService(
        IRepository<TaskComment> commentRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<VectorSyncOutbox> outboxRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _commentRepo = commentRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _outboxRepo = outboxRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _taskAccessPolicy = taskAccessPolicy;
    }

    public async Task<Result<IReadOnlyList<CommentDto>>> GetByTaskAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskItemId, ct);
        if (task == null)
        {
            return Result.NotFound<IReadOnlyList<CommentDto>>();
        }

        if (!await CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden<IReadOnlyList<CommentDto>>();
        }

        var comments = await _commentRepo.GetQueryable()
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.Attachments)
            .Where(comment => comment.TaskItemId == taskItemId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<CommentDto>>(comments.Select(comment => comment.ToDto()).ToList());
    }

    public async Task<Result<CommentDto>> CreateAsync(CreateCommentDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<CommentDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            return Result.Failure<CommentDto>("Comment content is required.");
        }

        if (dto.Content.Trim().Length > 10_000)
        {
            return Result.Failure<CommentDto>("Bình luận không được vượt quá 10.000 ký tự.", 400);
        }

        if ((dto.MentionedUserIds?.Distinct().Count() ?? 0) > 50)
        {
            return Result.Failure<CommentDto>("Một bình luận không thể nhắc quá 50 người.", 400);
        }

        var task = await LoadTaskAsync(dto.TaskItemId, ct);
        if (task == null)
        {
            return Result.NotFound<CommentDto>();
        }

        if (!await _taskAccessPolicy.CanContributeToTaskAsync(task, ct))
        {
            return Result.Forbidden<CommentDto>();
        }

        if (dto.ParentCommentId.HasValue)
        {
            var parentExists = await _commentRepo.GetQueryable()
                .AnyAsync(comment => comment.Id == dto.ParentCommentId.Value && comment.TaskItemId == dto.TaskItemId, ct);
            if (!parentExists)
            {
                return Result.Failure<CommentDto>("Parent comment was not found.", 404);
            }
        }

        var comment = new TaskComment
        {
            TaskItemId = dto.TaskItemId,
            AuthorId = currentUserId.Value,
            ParentCommentId = dto.ParentCommentId,
            Content = dto.Content.Trim()
        };

        await _commentRepo.AddAsync(comment, ct);
        await AddToOutboxAsync("CommentAdded", new { Id = comment.Id }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Create",
            nameof(TaskComment),
            comment.Id.ToString(),
            new { dto.TaskItemId },
            ct);

        await NotifyParticipantsAsync(task, comment.Id, currentUserId.Value, dto.MentionedUserIds, ct);

        var saved = await _commentRepo.GetQueryable()
            .Include(item => item.Author)
            .Include(item => item.Attachments)
            .FirstAsync(item => item.Id == comment.Id, ct);

        return Result.Created(saved.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure("Access denied.", 403);
        }

        var comment = await _commentRepo.GetQueryable()
            .Include(item => item.TaskItem)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (comment == null)
        {
            return Result.Failure("Comment was not found.", 404);
        }

        var task = await LoadTaskAsync(comment.TaskItemId, ct);
        if (task == null || !await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var canDeleteOwn = comment.AuthorId == currentUserId &&
            await _taskAccessPolicy.CanContributeToTaskAsync(task, ct);
        var canModerateProject = await _taskAccessPolicy.CanManageProjectAsync(
            task.ProjectId,
            task.Project.OwnerId,
            ct);
        if (!canDeleteOwn && !canModerateProject)
        {
            return Result.Failure("Access denied.", 403);
        }

        await _commentRepo.DeleteAsync(comment, ct);
        await AddToOutboxAsync("CommentDeleted", new { Id = comment.Id }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Delete",
            nameof(TaskComment),
            id.ToString(),
            new { comment.TaskItemId },
            ct);

        return Result.Success();
    }

    private async Task AddToOutboxAsync(string eventType, object payload, CancellationToken ct)
    {
        var message = new VectorSyncOutbox
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload)
        };
        await _outboxRepo.AddAsync(message, ct);
    }

    private async Task<TaskItem?> LoadTaskAsync(Guid taskItemId, CancellationToken ct)
        => await _taskRepo.GetQueryable()
            .Include(task => task.Project)
                .ThenInclude(project => project.Organization)
            .Include(task => task.Assignees)
            .FirstOrDefaultAsync(task => task.Id == taskItemId, ct);

    private async Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct)
        => await _taskAccessPolicy.CanAccessTaskAsync(task, ct);

    private async Task NotifyParticipantsAsync(
        TaskItem task,
        Guid commentId,
        Guid currentUserId,
        IReadOnlyList<Guid>? mentionedUserIds,
        CancellationToken ct)
    {
        var mentionedIds = (mentionedUserIds ?? [])
            .Where(id => id != Guid.Empty && id != currentUserId)
            .Distinct()
            .ToHashSet();

        var activeProjectMemberIds = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == task.ProjectId)
            .Select(member => member.UserId)
            .ToHashSetAsync(ct);

        bool IsCurrentProjectParticipant(Guid userId)
            => task.Project.OwnerId == userId || activeProjectMemberIds.Contains(userId);

        bool CanReceivePrivateTaskNotification(Guid userId)
            => !task.IsPrivate ||
               task.Project.OwnerId == userId ||
               task.ReporterId == userId ||
               task.AssigneeId == userId ||
               task.Assignees.Any(assignment => assignment.UserId == userId);

        var recipients = new[] { task.ReporterId }
            .Concat(task.AssigneeId.HasValue ? [task.AssigneeId.Value] : [])
            .Concat(task.Assignees.Select(assignment => assignment.UserId))
            .Where(userId => userId != currentUserId && !mentionedIds.Contains(userId))
            .Where(IsCurrentProjectParticipant)
            .Where(CanReceivePrivateTaskNotification)
            .Distinct()
            .ToList();

        foreach (var mentionedUserId in mentionedIds)
        {
            if (IsCurrentProjectParticipant(mentionedUserId) && CanReceivePrivateTaskNotification(mentionedUserId))
            {
                var template = NotificationTemplates.Mentioned(task.Id, commentId, task.Title, mentionedUserId);
                await _notificationService.CreateAsync(
                    mentionedUserId,
                    template.Message,
                    template.Type,
                    template.Tone,
                    task.Id,
                    nameof(TaskItem),
                    template.IdempotencyKey,
                    ct);
            }
        }

        foreach (var recipientId in recipients)
        {
            var template = NotificationTemplates.CommentAdded(task.Id, commentId, task.Title, recipientId);
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

}
