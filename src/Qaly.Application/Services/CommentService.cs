using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Comment;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class CommentService : ICommentService
{
    private readonly IRepository<TaskComment> _commentRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public CommentService(
        IRepository<TaskComment> commentRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _commentRepo = commentRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
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

        var task = await LoadTaskAsync(dto.TaskItemId, ct);
        if (task == null)
        {
            return Result.NotFound<CommentDto>();
        }

        if (!await CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden<CommentDto>();
        }

        var comment = new TaskComment
        {
            TaskItemId = dto.TaskItemId,
            AuthorId = currentUserId.Value,
            Content = dto.Content.Trim()
        };

        await _commentRepo.AddAsync(comment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(TaskComment), comment.Id.ToString(), new { dto.TaskItemId }, ct);

        await NotifyParticipantsAsync(task, currentUserId.Value, ct);

        var saved = await _commentRepo.GetQueryable()
            .Include(item => item.Author)
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

        if (comment.AuthorId != currentUserId && !IsAdmin())
        {
            return Result.Failure("Access denied.", 403);
        }

        await _commentRepo.DeleteAsync(comment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(TaskComment), id.ToString(), new { comment.TaskItemId }, ct);

        return Result.Success();
    }

    private async Task<TaskItem?> LoadTaskAsync(Guid taskItemId, CancellationToken ct)
        => await _taskRepo.GetQueryable()
            .Include(task => task.Project)
            .FirstOrDefaultAsync(task => task.Id == taskItemId, ct);

    private async Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
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

    private async Task NotifyParticipantsAsync(TaskItem task, Guid currentUserId, CancellationToken ct)
    {
        var recipients = new[] { task.ReporterId, task.AssigneeId }
            .Where(userId => userId.HasValue && userId.Value != currentUserId)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToList();

        foreach (var recipientId in recipients)
        {
            await _notificationService.CreateAsync(
                recipientId,
                $"New comment on task \"{task.Title}\".",
                "CommentAdded",
                task.Id,
                nameof(TaskItem),
                ct);
        }
    }

    private bool IsAdmin()
        => string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
}
