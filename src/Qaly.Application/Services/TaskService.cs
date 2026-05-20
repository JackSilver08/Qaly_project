using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text.Json;

namespace Qaly.Application.Services;

public class TaskService : ITaskService
{
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskDependency> _dependencyRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<TaskAttachment> _attachmentRepo;
    private readonly IRepository<TaskAssignment> _assignmentRepo;
    private readonly IRepository<TaskViewEvent> _viewEventRepo;
    private readonly IRepository<TaskLabel> _taskLabelRepo;
    private readonly IRepository<ProjectLabel> _projectLabelRepo;
    private readonly IRepository<VectorSyncOutbox> _outboxRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ITaskPrioritySuggestionService _taskPrioritySuggestionService;
    private readonly IWebhookPublisher _webhookPublisher;

    public TaskService(
        IRepository<TaskItem> taskRepo,
        IRepository<TaskDependency> dependencyRepo,
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<User> userRepo,
        IRepository<TaskAttachment> attachmentRepo,
        IRepository<TaskAssignment> assignmentRepo,
        IRepository<TaskViewEvent> viewEventRepo,
        IRepository<TaskLabel> taskLabelRepo,
        IRepository<ProjectLabel> projectLabelRepo,
        IRepository<VectorSyncOutbox> outboxRepo,
        IUnitOfWork unitOfWork,
        ITaskAccessPolicy taskAccessPolicy,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        ITaskPrioritySuggestionService taskPrioritySuggestionService,
        IWebhookPublisher webhookPublisher)
    {
        _taskRepo = taskRepo;
        _dependencyRepo = dependencyRepo;
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
        _attachmentRepo = attachmentRepo;
        _assignmentRepo = assignmentRepo;
        _viewEventRepo = viewEventRepo;
        _taskLabelRepo = taskLabelRepo;
        _projectLabelRepo = projectLabelRepo;
        _outboxRepo = outboxRepo;
        _unitOfWork = unitOfWork;
        _taskAccessPolicy = taskAccessPolicy;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _taskPrioritySuggestionService = taskPrioritySuggestionService;
        _webhookPublisher = webhookPublisher;
    }

    public async Task<Result<TaskItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await TaskDetailsQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task == null)
        {
            return Result.NotFound<TaskItemDto>();
        }

        if (!await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var isRestricted = !await CanViewTaskDetailsAsync(task, ct);
        return Result.Success(task.ToDto(isRestricted));
    }

    public async Task<Result<PagedResult<TaskItemDto>>> GetByProjectAsync(Guid projectId, string? status = null, string? priority = null, int page = 1, int pageSize = 20, string? search = null, Guid? assigneeId = null, Guid? labelId = null, string sort = "default", CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.NotFound<PagedResult<TaskItemDto>>();
        }

        if (!await _taskAccessPolicy.CanAccessProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<PagedResult<TaskItemDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = TaskDetailsQuery()
            .Where(t => t.ProjectId == projectId);

        query = _taskAccessPolicy.ApplyVisibilityFilter(query);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(t => t.Title.Contains(normalizedSearch) || (t.Description != null && t.Description.Contains(normalizedSearch)));
        }

        if (assigneeId.HasValue)
        {
            query = query.Where(t => t.AssigneeId == assigneeId || t.Assignees.Any(assignment => assignment.UserId == assigneeId.Value));
        }

        if (labelId.HasValue)
        {
            query = query.Where(t => t.Labels.Any(label => label.ProjectLabelId == labelId.Value));
        }

        var totalCount = await query.CountAsync(ct);
        query = ApplyTaskSort(query, sort);

        var items = await query
            .OrderByPlanningPriority()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var mappedItems = new List<TaskItemDto>(items.Count);
        foreach (var item in items)
        {
            mappedItems.Add(item.ToDto(!await CanViewTaskDetailsAsync(item, ct)));
        }

        return Result.Success(new PagedResult<TaskItemDto>
        {
            Items = mappedItems,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PagedResult<TaskItemDto>>> GetByAssigneeAsync(Guid assigneeId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!_taskAccessPolicy.IsAdmin && _taskAccessPolicy.CurrentUserId != assigneeId)
        {
            return Result.Forbidden<PagedResult<TaskItemDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _taskAccessPolicy.ApplyVisibilityFilter(TaskDetailsQuery())
            .Where(t => t.AssigneeId == assigneeId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<TaskItemDto>
        {
            Items = items.Select(item => item.ToDto(false)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PagedResult<TaskAttentionDto>>> GetAttentionByProjectAsync(
        Guid projectId,
        Guid? assigneeId = null,
        Guid? reporterId = null,
        string? status = null,
        string? priority = null,
        string? riskType = null,
        DateTimeOffset? from = null,
        DateTimeOffset? toDate = null,
        int page = 1,
        int pageSize = 25,
        string sort = "risk",
        CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.NotFound<PagedResult<TaskAttentionDto>>();
        }

        if (!await _taskAccessPolicy.CanAccessProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<PagedResult<TaskAttentionDto>>();
        }

        var currentUserId = _taskAccessPolicy.CurrentUserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<TaskAttentionDto>>();
        }

        var canViewProjectRisk =
            await _taskAccessPolicy.CanViewProjectTimelineAsync(projectId, project.OwnerId, ct) &&
            await _taskAccessPolicy.CanViewTaskRiskAsync(projectId, project.OwnerId, ct);
        var canViewUnseenSignal =
            await _taskAccessPolicy.CanViewProjectTimelineAsync(projectId, project.OwnerId, ct) &&
            await _taskAccessPolicy.CanViewUnseenTaskSignalAsync(projectId, project.OwnerId, ct);
        var canNudgeAssignee =
            await _taskAccessPolicy.CanNudgeAssigneeAsync(projectId, project.OwnerId, ct);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = DateTimeOffset.UtcNow;

        var query = _taskAccessPolicy.ApplyVisibilityFilter(_taskRepo.GetQueryable())
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .Include(task => task.Project)
            .Include(task => task.Reporter)
            .Include(task => task.Assignee)
            .Include(task => task.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(task => task.ViewEvents)
            .AsQueryable();

        if (!canViewProjectRisk)
        {
            query = query.Where(task =>
                task.ReporterId == currentUserId ||
                task.AssigneeId == currentUserId ||
                task.Assignees.Any(assignment => assignment.UserId == currentUserId));
        }

        if (assigneeId.HasValue)
        {
            query = query.Where(task => task.AssigneeId == assigneeId || task.Assignees.Any(assignment => assignment.UserId == assigneeId.Value));
        }

        if (reporterId.HasValue)
        {
            query = query.Where(task => task.ReporterId == reporterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = TaskStatusRules.NormalizeStatus(status);
            query = query.Where(task => task.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            var normalizedPriority = TaskStatusRules.NormalizePriority(priority);
            query = query.Where(task => task.Priority == normalizedPriority);
        }

        if (from.HasValue)
        {
            query = query.Where(task => task.DueDate >= from.Value || task.StartDate >= from.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(task => task.DueDate <= toDate.Value || task.StartDate <= toDate.Value);
        }

        var tasks = await query.ToListAsync(ct);
        var items = tasks
            .SelectMany(task => BuildAttentionItems(task, now, currentUserId.Value, canViewProjectRisk, canViewUnseenSignal, canNudgeAssignee))
            .Where(item => MatchesRiskType(item, riskType))
            .ToList();

        items = SortAttentionItems(items, sort);

        var totalCount = items.Count;
        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(new PagedResult<TaskAttentionDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<TaskItemDto>> CreateAsync(CreateTaskDto dto, CancellationToken ct = default)
    {
        var currentUserId = _taskAccessPolicy.CurrentUserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var assigneeIds = NormalizeAssigneeIds(dto.AssigneeId, dto.AssigneeIds);
        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, dto.ProjectId, assigneeIds, dto.LabelIds, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Nhiệm vụ không hợp lệ.", validation.StatusCode);
        }

        var project = validation.Project!;
        if (!await _taskAccessPolicy.CanAccessProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var task = dto.ToEntity();
        task.Title = dto.Title.Trim();
        task.Priority = TaskStatusRules.NormalizePriority(dto.Priority);
        task.ReporterId = currentUserId.Value;

        await _taskRepo.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await SyncAssignmentsAsync(task.Id, assigneeIds, ct);
        await SyncLabelsAsync(task.Id, task.ProjectId, dto.LabelIds, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(TaskItem), task.Id.ToString(), new { task.Title, task.ProjectId }, ct);

        // Realtime broadcast
        await _notificationService.BroadcastToProjectAsync(task.ProjectId, $"Nhiệm vụ \"{task.Title}\" đã được tạo.", "TaskCreated", new { task.Id }, ct);
        await _webhookPublisher.PublishAsync(task.ProjectId, "task.created", new { task.Id, task.Title, task.Status }, ct);

        foreach (var assigneeId in assigneeIds.Where(id => id != currentUserId).Distinct())
        {
            await _notificationService.CreateAsync(
                assigneeId,
                $"Bạn đã được giao nhiệm vụ \"{task.Title}\".",
                "TaskAssigned",
                task.Id,
                nameof(TaskItem),
                ct);
        }

        var result = await GetByIdAsync(task.Id, ct);
        if (!result.IsSuccess || result.Data == null)
        {
            return result;
        }

        var suggestion = await _taskPrioritySuggestionService.SuggestAsync(task.Title, task.Description ?? string.Empty, project.Name);
        return Result.Created(result.Data with { AiPrioritySuggestion = suggestion });
    }

    public async Task<Result<TaskItemDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken ct = default)
    {
        var task = await TaskDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (task == null)
        {
            return Result.NotFound<TaskItemDto>();
        }

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var assigneeIds = NormalizeAssigneeIds(dto.AssigneeId, dto.AssigneeIds);
        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, task.ProjectId, assigneeIds, dto.LabelIds, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Nhiệm vụ không hợp lệ.", validation.StatusCode);
        }

        if (!TaskStatusRules.IsValidStatus(dto.Status))
        {
            return Result.Failure<TaskItemDto>("Trạng thái nhiệm vụ không hợp lệ.");
        }

        var oldStatus = task.Status;
        var normalizedStatus = TaskStatusRules.NormalizeStatus(dto.Status);
        if (!TaskStatusRules.CanTransition(oldStatus, normalizedStatus))
        {
            return Result.Failure<TaskItemDto>($"Không cho phép chuyển trạng thái từ {oldStatus} sang {normalizedStatus}.", 400);
        }

        if (RequiresApprovedEvidence(oldStatus, normalizedStatus) && !await HasApprovedEvidenceAsync(task.Id, ct))
        {
            return Result.Failure<TaskItemDto>("Không thể đánh dấu hoàn thành vì chưa có minh chứng được duyệt.", 400);
        }

        var previousAssignees = task.Assignees.Select(assignment => assignment.UserId).ToHashSet();
        dto.ApplyTo(task);
        task.Title = dto.Title.Trim();
        task.Status = normalizedStatus;
        task.Priority = TaskStatusRules.NormalizePriority(dto.Priority);

        await _taskRepo.UpdateAsync(task, ct);
        await SyncAssignmentsAsync(task.Id, assigneeIds, ct);
        await SyncLabelsAsync(task.Id, task.ProjectId, dto.LabelIds, ct);
        await AddToOutboxAsync("TaskUpdated", new { Id = task.Id }, ct);
        await _webhookPublisher.PublishAsync(task.ProjectId, "task.updated", new { task.Id, task.Title, task.Status }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(TaskItem), task.Id.ToString(), dto, ct);

        foreach (var assigneeId in assigneeIds.Where(id => !previousAssignees.Contains(id)))
        {
            await _notificationService.CreateAsync(
                assigneeId,
                $"Bạn đã được giao nhiệm vụ \"{task.Title}\".",
                "TaskAssigned",
                task.Id,
                nameof(TaskItem),
                ct);
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> UpdateStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
    {
        var task = await TaskDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (task == null)
        {
            return Result.Failure("Không tìm thấy nhiệm vụ.", 404);
        }

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.Failure("Truy cập bị từ chối.", 403);
        }

        if (!TaskStatusRules.IsValidStatus(newStatus))
        {
            return Result.Failure("Trạng thái nhiệm vụ không hợp lệ.");
        }

        var normalizedStatus = TaskStatusRules.NormalizeStatus(newStatus);
        var oldStatus = task.Status;

        if (!TaskStatusRules.CanTransition(oldStatus, normalizedStatus))
        {
            return Result.Failure($"Không cho phép chuyển trạng thái từ {oldStatus} sang {normalizedStatus}.", 400);
        }

        if (RequiresApprovedEvidence(oldStatus, normalizedStatus) && !await HasApprovedEvidenceAsync(task.Id, ct))
        {
            return Result.Failure("Không thể đánh dấu hoàn thành vì chưa có minh chứng được duyệt.", 400);
        }

        task.Status = normalizedStatus;

        await _taskRepo.UpdateAsync(task, ct);
        await AddToOutboxAsync("TaskUpdated", new { Id = task.Id }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("StatusChange", nameof(TaskItem), task.Id.ToString(), new { oldStatus, newStatus = normalizedStatus }, ct);

        await NotifyStatusChangeAsync(task, oldStatus, normalizedStatus, ct);

        return Result.Success();
    }

    public async Task<Result> UpdateSortOrderAsync(Guid id, int sortOrder, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetQueryable()
            .WithProject()
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (task == null)
        {
            return Result.Failure("Không tìm thấy nhiệm vụ.", 404);
        }

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.Failure("Truy cập bị từ chối.", 403);
        }

        task.SortOrder = sortOrder;
        await _taskRepo.UpdateAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await TaskDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (task == null)
        {
            return Result.Failure("Không tìm thấy nhiệm vụ.", 404);
        }

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.Failure("Truy cập bị từ chối.", 403);
        }

        var projectId = task.ProjectId;
        var title = task.Title;

        await _taskRepo.DeleteAsync(task, ct);
        await AddToOutboxAsync("TaskDeleted", new { Id = task.Id }, ct);
        await _webhookPublisher.PublishAsync(projectId, "task.deleted", new { id, title }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(TaskItem), id.ToString(), new { task.Title }, ct);

        // Realtime broadcast
        await _notificationService.BroadcastToProjectAsync(projectId, $"Nhiệm vụ \"{title}\" đã bị xóa.", "TaskDeleted", new { id }, ct);

        return Result.Success();
    }

    public async Task<Result> BatchDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var tasks = await _taskRepo.GetQueryable()
            .WithProject()
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);

        if (tasks.Count == 0) return Result.Success();

        foreach (var task in tasks)
        {
            if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct)) continue;

            var projectId = task.ProjectId;
            var title = task.Title;

            await _taskRepo.DeleteAsync(task, ct);
            await AddToOutboxAsync("TaskDeleted", new { Id = task.Id }, ct);
            await _webhookPublisher.PublishAsync(projectId, "task.deleted", new { task.Id, title }, ct);
            await _auditLogService.LogAsync("Delete", nameof(TaskItem), task.Id.ToString(), new { task.Title }, ct);
            await _notificationService.BroadcastToProjectAsync(projectId, $"Nhiệm vụ \"{title}\" đã bị xóa.", "TaskDeleted", new { task.Id }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> BatchUpdateStatusAsync(IEnumerable<Guid> ids, string newStatus, CancellationToken ct = default)
    {
        if (!TaskStatusRules.IsValidStatus(newStatus))
        {
            return Result.Failure("Trạng thái nhiệm vụ không hợp lệ.");
        }

        var normalizedStatus = TaskStatusRules.NormalizeStatus(newStatus);
        var tasks = await _taskRepo.GetQueryable()
            .WithProject()
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);

        if (tasks.Count == 0) return Result.Success();

        foreach (var task in tasks)
        {
            if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct)) continue;

            var oldStatus = task.Status;
            if (!TaskStatusRules.CanTransition(oldStatus, normalizedStatus))
            {
                continue;
            }

            if (RequiresApprovedEvidence(oldStatus, normalizedStatus) && !await HasApprovedEvidenceAsync(task.Id, ct))
            {
                continue;
            }

            task.Status = normalizedStatus;

            await _taskRepo.UpdateAsync(task, ct);
            await AddToOutboxAsync("TaskUpdated", new { Id = task.Id }, ct);
            await _auditLogService.LogAsync("StatusChange", nameof(TaskItem), task.Id.ToString(), new { oldStatus, newStatus = normalizedStatus }, ct);
            await NotifyStatusChangeAsync(task, oldStatus, normalizedStatus, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IEnumerable<GanttTaskDto>>> GetGanttDataAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<IEnumerable<GanttTaskDto>>();

        if (!await _taskAccessPolicy.CanAccessProjectAsync(projectId, project.OwnerId, ct))
            return Result.Forbidden<IEnumerable<GanttTaskDto>>();

        var tasks = await _taskAccessPolicy.ApplyVisibilityFilter(_taskRepo.GetQueryable())
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.PredecessorDependencies)
            .ToListAsync(ct);

        var dtos = tasks.Select(t => new GanttTaskDto
        {
            Id = t.Id,
            Title = t.Title,
            Status = t.Status,
            StartDate = t.StartDate,
            EndDate = t.DueDate,
            Progress = t.Status == "Done" ? 100 : (t.Status == "InProgress" ? 50 : 0),
            Dependencies = t.PredecessorDependencies.Select(d => d.PredecessorId).ToList(),
            IsCriticalPath = false
        }).ToList();

        GanttCriticalPathCalculator.MarkCriticalPath(dtos);

        return Result.Success<IEnumerable<GanttTaskDto>>(dtos);
    }

    public async Task<Result> UpdateDatesAsync(Guid taskId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetQueryable()
            .WithProject()
            .FirstOrDefaultAsync(item => item.Id == taskId, ct);
        if (task == null) return Result.Failure("Task was not found.", 404);

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct)) return Result.Failure("Access denied.", 403);

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            return Result.Failure("Start date cannot be after end date.");

        task.StartDate = startDate;
        task.DueDate = endDate;

        await _taskRepo.UpdateAsync(task, ct);
        await AddToOutboxAsync("TaskUpdated", new { Id = task.Id }, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> MarkViewedAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        var currentUserId = _taskAccessPolicy.CurrentUserId;
        if (currentUserId == null)
        {
            return Result.Forbidden();
        }

        var task = await _taskRepo.GetQueryable()
            .WithDetails()
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId, ct);

        if (task == null)
        {
            return Result.NotFound();
        }

        if (!await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden();
        }

        var viewedAt = DateTimeOffset.UtcNow;
        var existing = await _viewEventRepo.GetQueryable()
            .FirstOrDefaultAsync(view => view.TaskItemId == taskId && view.UserId == currentUserId.Value, ct);

        if (existing == null)
        {
            await _viewEventRepo.AddAsync(new TaskViewEvent
            {
                TaskItemId = taskId,
                UserId = currentUserId.Value,
                ViewedAt = viewedAt,
                ViewCount = 1
            }, ct);
        }
        else
        {
            existing.ViewedAt = viewedAt;
            existing.ViewCount += 1;
            await _viewEventRepo.UpdateAsync(existing, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> NudgeAssigneeAsync(Guid projectId, Guid taskId, Guid? assigneeId = null, CancellationToken ct = default)
    {
        var currentUserId = _taskAccessPolicy.CurrentUserId;
        if (currentUserId == null)
        {
            return Result.Forbidden();
        }

        var task = await _taskRepo.GetQueryable()
            .WithDetails()
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId, ct);

        if (task == null)
        {
            return Result.NotFound();
        }

        if (!await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
        {
            return Result.Forbidden();
        }

        var canNudge = await _taskAccessPolicy.CanNudgeAssigneeAsync(projectId, task.Project.OwnerId, ct) ||
            task.ReporterId == currentUserId.Value;
        if (!canNudge)
        {
            return Result.Forbidden("Bạn không có quyền nhắc người phụ trách task này.");
        }

        var assignedUserIds = task.Assignees.Select(assignment => assignment.UserId).ToHashSet();
        if (task.AssigneeId.HasValue)
        {
            assignedUserIds.Add(task.AssigneeId.Value);
        }

        if (assigneeId.HasValue)
        {
            if (!assignedUserIds.Contains(assigneeId.Value))
            {
                return Result.Failure("Người dùng này không phải người phụ trách task.", 400);
            }

            assignedUserIds = [assigneeId.Value];
        }

        assignedUserIds.Remove(currentUserId.Value);
        if (assignedUserIds.Count == 0)
        {
            return Result.Failure("Task không có người phụ trách phù hợp để nhắc.", 400);
        }

        var message = $"Task \"{task.Title}\" cần được kiểm tra lại trên timeline dự án.";
        foreach (var userId in assignedUserIds)
        {
            await _notificationService.CreateAsync(
                userId,
                message,
                "TaskAttentionNudge",
                task.Id,
                nameof(TaskItem),
                ct);
        }

        await _notificationService.BroadcastToProjectAsync(
            task.ProjectId,
            message,
            "TaskAttentionNudge",
            new { task.Id, assigneeId },
            ct);

        return Result.Success();
    }

    public async Task<Result> AddDependencyAsync(Guid predecessorId, Guid successorId, string type = "FinishToStart", CancellationToken ct = default)
    {
        if (predecessorId == successorId) return Result.Failure("Cannot depend on itself.");

        var predecessor = await _taskRepo.GetQueryable()
            .WithProject()
            .FirstOrDefaultAsync(task => task.Id == predecessorId, ct);
        var successor = await _taskRepo.GetQueryable()
            .WithProject()
            .FirstOrDefaultAsync(task => task.Id == successorId, ct);

        if (predecessor == null || successor == null) return Result.Failure("Task not found.", 404);

        if (predecessor.ProjectId != successor.ProjectId)
            return Result.Failure("Tasks must be in the same project.");

        if (!await _taskAccessPolicy.CanManageTaskAsync(successor, ct)) return Result.Failure("Access denied.", 403);

        var exists = await _dependencyRepo.GetQueryable()
            .AnyAsync(d => d.PredecessorId == predecessorId && d.SuccessorId == successorId, ct);

        if (exists) return Result.Failure("Dependency already exists.");

        var dependency = new TaskDependency
        {
            PredecessorId = predecessorId,
            SuccessorId = successorId,
            DependencyType = type
        };

        await _dependencyRepo.AddAsync(dependency, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> RemoveDependencyAsync(Guid dependencyId, CancellationToken ct = default)
    {
        var dependency = await _dependencyRepo.GetQueryable()
            .Include(d => d.Successor)
            .ThenInclude(task => task.Project)
            .FirstOrDefaultAsync(d => d.Id == dependencyId, ct);

        if (dependency == null) return Result.Failure("Dependency not found.", 404);

        if (!await _taskAccessPolicy.CanManageTaskAsync(dependency.Successor, ct)) return Result.Failure("Access denied.", 403);

        await _dependencyRepo.DeleteAsync(dependency, ct);
        await _unitOfWork.SaveChangesAsync(ct);

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

    private IQueryable<TaskItem> TaskDetailsQuery()
        => _taskRepo.GetQueryable()
            .WithDetails();

    private static IQueryable<TaskItem> ApplyTaskSort(IQueryable<TaskItem> query, string? sort)
        => (sort ?? "default").Trim().ToLowerInvariant() switch
        {
            "deadline" => query
                .OrderByDescending(t => t.IsPinned)
                .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
                .ThenByDescending(t => t.CreatedAt),
            "newest" => query
                .OrderByDescending(t => t.IsPinned)
                .ThenByDescending(t => t.CreatedAt),
            "oldest" => query
                .OrderByDescending(t => t.IsPinned)
                .ThenBy(t => t.CreatedAt),
            "priority" => query
                .OrderByDescending(t => t.IsPinned)
                .ThenBy(t => t.Priority == "Critical" ? 0 : t.Priority == "High" ? 1 : t.Priority == "Medium" ? 2 : 3)
                .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue),
            _ => query
                .OrderByDescending(t => t.IsPinned)
                .OrderByPlanningPriority()
        };

    private async Task<bool> CanViewTaskDetailsAsync(TaskItem task, CancellationToken ct)
        => await _taskAccessPolicy.CanAccessTaskAsync(task, ct);

    private static bool RequiresApprovedEvidence(string oldStatus, string newStatus)
        => !string.Equals(oldStatus, "Done", StringComparison.OrdinalIgnoreCase) &&
           string.Equals(newStatus, "Done", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> HasApprovedEvidenceAsync(Guid taskId, CancellationToken ct)
        => await _attachmentRepo.GetQueryable()
            .AnyAsync(attachment =>
                attachment.TaskItemId == taskId &&
                attachment.IsEvidence &&
                attachment.EvidenceApprovalStatus == "Approved", ct);

    private async Task<TaskInputValidation> ValidateTaskInputAsync(string title, string priority, Guid projectId, IReadOnlyList<Guid> assigneeIds, IReadOnlyList<Guid>? labelIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return TaskInputValidation.Failure("Task title is required.");
        }

        if (!TaskStatusRules.IsValidPriority(priority))
        {
            return TaskInputValidation.Failure("Invalid task priority.");
        }

        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return TaskInputValidation.Failure("Project was not found.", 404);
        }

        foreach (var assigneeId in assigneeIds.Distinct())
        {
            var userExists = await _userRepo.GetQueryable()
                .AnyAsync(user => user.Id == assigneeId && user.IsActive, ct);

            if (!userExists)
            {
                return TaskInputValidation.Failure("Assignee was not found.", 404);
            }

            var isProjectUser = project.OwnerId == assigneeId ||
                await _memberRepo.GetQueryable()
                    .AnyAsync(member => member.ProjectId == projectId && member.UserId == assigneeId, ct);

            if (!isProjectUser)
            {
                return TaskInputValidation.Failure("Assignee must be a project member.", 400);
            }
        }

        if (labelIds is { Count: > 0 })
        {
            var distinctLabelIds = labelIds.Distinct().ToList();
            var validLabelCount = await _projectLabelRepo.GetQueryable()
                .CountAsync(label => label.ProjectId == projectId && distinctLabelIds.Contains(label.Id), ct);
            if (validLabelCount != distinctLabelIds.Count)
            {
                return TaskInputValidation.Failure("One or more labels do not belong to this project.", 400);
            }
        }

        return TaskInputValidation.Success(project);
    }

    private static List<Guid> NormalizeAssigneeIds(Guid? primaryAssigneeId, IReadOnlyList<Guid>? assigneeIds)
    {
        var normalized = new List<Guid>();
        if (primaryAssigneeId.HasValue && primaryAssigneeId.Value != Guid.Empty)
        {
            normalized.Add(primaryAssigneeId.Value);
        }

        if (assigneeIds != null)
        {
            normalized.AddRange(assigneeIds.Where(id => id != Guid.Empty));
        }

        return normalized.Distinct().ToList();
    }

    private async Task SyncAssignmentsAsync(Guid taskId, IReadOnlyList<Guid> assigneeIds, CancellationToken ct)
    {
        var existing = await _assignmentRepo.GetQueryable()
            .Where(assignment => assignment.TaskItemId == taskId)
            .ToListAsync(ct);
        var target = assigneeIds.ToHashSet();

        foreach (var assignment in existing.Where(assignment => !target.Contains(assignment.UserId)))
        {
            await _assignmentRepo.DeleteAsync(assignment, ct);
        }

        var existingUserIds = existing.Select(assignment => assignment.UserId).ToHashSet();
        foreach (var userId in target.Where(userId => !existingUserIds.Contains(userId)))
        {
            await _assignmentRepo.AddAsync(new TaskAssignment
            {
                TaskItemId = taskId,
                UserId = userId,
                AssignedAt = DateTimeOffset.UtcNow,
                AssignedByUserId = _taskAccessPolicy.CurrentUserId
            }, ct);
        }
    }

    private async Task SyncLabelsAsync(Guid taskId, Guid projectId, IReadOnlyList<Guid>? labelIds, CancellationToken ct)
    {
        var existing = await _taskLabelRepo.GetQueryable()
            .Where(label => label.TaskItemId == taskId)
            .ToListAsync(ct);
        var target = (labelIds ?? []).Where(id => id != Guid.Empty).Distinct().ToHashSet();

        foreach (var label in existing.Where(label => !target.Contains(label.ProjectLabelId)))
        {
            await _taskLabelRepo.DeleteAsync(label, ct);
        }

        var existingLabelIds = existing.Select(label => label.ProjectLabelId).ToHashSet();
        foreach (var labelId in target.Where(labelId => !existingLabelIds.Contains(labelId)))
        {
            var belongsToProject = await _projectLabelRepo.GetQueryable()
                .AnyAsync(label => label.Id == labelId && label.ProjectId == projectId, ct);
            if (belongsToProject)
            {
                await _taskLabelRepo.AddAsync(new TaskLabel { TaskItemId = taskId, ProjectLabelId = labelId }, ct);
            }
        }
    }

    private async Task NotifyStatusChangeAsync(TaskItem task, string oldStatus, string newStatus, CancellationToken ct)
    {
        var currentUserId = _taskAccessPolicy.CurrentUserId;
        
        // Personal notifications
        var recipients = new[] { task.ReporterId, task.AssigneeId }
            .Where(userId => userId.HasValue && userId.Value != currentUserId)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToList();

        foreach (var recipient in recipients)
        {
            await _notificationService.CreateAsync(
                recipient,
                $"Task \"{task.Title}\" moved from {oldStatus} to {newStatus}.",
                "TaskStatusChanged",
                task.Id,
                nameof(TaskItem),
                ct);
        }

        // Realtime broadcast to project
        await _notificationService.BroadcastToProjectAsync(
            task.ProjectId, 
            $"Task \"{task.Title}\" status changed to {newStatus}.", 
            "TaskStatusChanged", 
            new { task.Id, oldStatus, newStatus }, 
            ct);
    }

    private sealed record TaskInputValidation(bool IsSuccess, string? Error, int StatusCode, Project? Project)
    {
        public static TaskInputValidation Success(Project project)
            => new(true, null, 200, project);

        public static TaskInputValidation Failure(string error, int statusCode = 400)
            => new(false, error, statusCode, null);
    }

    private static IEnumerable<TaskAttentionDto> BuildAttentionItems(
        TaskItem task,
        DateTimeOffset now,
        Guid currentUserId,
        bool canViewProjectRisk,
        bool canViewUnseenSignal,
        bool canNudgeAssignee)
    {
        var assignments = task.Assignees.Count > 0
            ? task.Assignees.Select(assignment => new AttentionAssignee(
                assignment.UserId,
                assignment.User?.FullName,
                assignment.AssignedAt == default ? task.CreatedAt : assignment.AssignedAt))
            : new[]
            {
                new AttentionAssignee(task.AssigneeId, task.Assignee?.FullName, task.CreatedAt)
            };

        foreach (var assignment in assignments)
        {
            var userId = assignment.UserId;
            var lastViewedAt = userId.HasValue
                ? task.ViewEvents
                    .Where(view => view.UserId == userId.Value)
                    .Select(view => (DateTimeOffset?)view.ViewedAt)
                    .OrderByDescending(viewedAt => viewedAt)
                    .FirstOrDefault()
                : null;

            var isDueSoon = IsDueSoon(task, now);
            var isOverdue = IsAttentionOverdue(task, now);
            var isStaleTodo = task.StartDate.HasValue &&
                task.StartDate.Value < now &&
                IsAttentionStatus(task, "Todo");
            var isStaleInProgress = IsStaleInProgress(task, now);
            var canSeeThisUnseenSignal = canViewUnseenSignal ||
                task.ReporterId == currentUserId ||
                userId == currentUserId;
            var isUnseen = canSeeThisUnseenSignal &&
                userId.HasValue &&
                !IsAttentionDone(task) &&
                (!lastViewedAt.HasValue || lastViewedAt.Value < assignment.AssignedAt);

            var reasons = BuildAttentionReasons(isDueSoon, isOverdue, isStaleTodo, isStaleInProgress, isUnseen);
            if (reasons.Count == 0)
            {
                continue;
            }

            var canActOnTask = canViewProjectRisk ||
                task.ReporterId == currentUserId ||
                task.AssigneeId == currentUserId ||
                task.Assignees.Any(item => item.UserId == currentUserId);

            yield return new TaskAttentionDto(
                task.Id,
                task.Title,
                task.ProjectId,
                task.Project?.Name ?? string.Empty,
                task.Status,
                task.Priority,
                task.StartDate,
                task.DueDate,
                task.ReporterId,
                task.Reporter?.FullName ?? string.Empty,
                userId,
                assignment.FullName,
                assignment.AssignedAt,
                lastViewedAt,
                isDueSoon,
                isOverdue,
                isStaleTodo,
                isStaleInProgress,
                isUnseen,
                reasons,
                BuildAllowedActions(task, currentUserId, userId, canActOnTask, canNudgeAssignee));
        }
    }

    private static List<string> BuildAttentionReasons(
        bool isDueSoon,
        bool isOverdue,
        bool isStaleTodo,
        bool isStaleInProgress,
        bool isUnseen)
    {
        var reasons = new List<string>();
        if (isOverdue) reasons.Add("QuaHan");
        if (isDueSoon) reasons.Add("SapToiHan");
        if (isStaleTodo) reasons.Add("ChuaBatDau");
        if (isStaleInProgress) reasons.Add("DangLamQuaLau");
        if (isUnseen) reasons.Add("ChuaXem");
        return reasons;
    }

    private static IReadOnlyList<string> BuildAllowedActions(
        TaskItem task,
        Guid currentUserId,
        Guid? attentionAssigneeId,
        bool canActOnTask,
        bool canNudgeAssignee)
    {
        var actions = new List<string> { "MoChiTiet" };
        if (!canActOnTask)
        {
            return actions;
        }

        actions.Add("BinhLuan");
        if (!IsAttentionDone(task) &&
            attentionAssigneeId.HasValue &&
            attentionAssigneeId.Value != currentUserId &&
            (canNudgeAssignee || task.ReporterId == currentUserId))
        {
            actions.Add("NhacNguoiPhuTrach");
        }

        if (IsAttentionStatus(task, "Todo") && (task.AssigneeId == currentUserId || task.Assignees.Any(assignment => assignment.UserId == currentUserId)))
        {
            actions.Add("BatDauLam");
        }

        return actions;
    }

    private static bool MatchesRiskType(TaskAttentionDto item, string? riskType)
    {
        if (string.IsNullOrWhiteSpace(riskType) || string.Equals(riskType, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return riskType.Trim().ToLowerInvariant() switch
        {
            "overdue" or "quahan" => item.IsOverdue,
            "duesoon" or "saptoihan" => item.IsDueSoon,
            "staletodo" or "chuabatdau" => item.IsStaleTodo,
            "staleinprogress" or "danglamqualau" => item.IsStaleInProgress,
            "unseen" or "chuaxem" => item.IsUnseenByAssignee,
            _ => true
        };
    }

    private static List<TaskAttentionDto> SortAttentionItems(List<TaskAttentionDto> items, string? sort)
        => (sort ?? "risk").Trim().ToLowerInvariant() switch
        {
            "duedate" => items
                .OrderBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
                .ThenBy(item => item.StartDate ?? DateTimeOffset.MaxValue)
                .ToList(),
            "priority" => items
                .OrderBy(PriorityRank)
                .ThenBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
                .ToList(),
            "assignee" => items
                .OrderBy(item => item.AssigneeName ?? string.Empty)
                .ThenByDescending(RiskRank)
                .ThenBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
                .ToList(),
            "status" => items
                .OrderBy(item => item.Status)
                .ThenByDescending(RiskRank)
                .ThenBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
                .ToList(),
            _ => items
                .OrderByDescending(RiskRank)
                .ThenBy(PriorityRank)
                .ThenBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
                .ThenBy(item => item.StartDate ?? DateTimeOffset.MaxValue)
                .ToList()
        };

    private static int RiskRank(TaskAttentionDto item)
    {
        var rank = 0;
        if (item.IsOverdue) rank += 100;
        if (item.IsStaleTodo) rank += 40;
        if (item.IsUnseenByAssignee) rank += 30;
        if (item.IsStaleInProgress) rank += 20;
        if (item.IsDueSoon) rank += 10;

        if (item.IsOverdue && item.DueDate.HasValue)
        {
            rank += Math.Min(30, Math.Max(0, (int)(DateTimeOffset.UtcNow - item.DueDate.Value).TotalDays));
        }

        return rank;
    }

    private static int PriorityRank(TaskAttentionDto item)
        => item.Priority switch
        {
            "Critical" => 0,
            "High" => 1,
            "Medium" => 2,
            "Low" => 3,
            _ => 4
        };

    private static bool IsDueSoon(TaskItem task, DateTimeOffset now)
        => task.DueDate.HasValue &&
           task.DueDate.Value >= now &&
           task.DueDate.Value <= now.AddHours(24) &&
           !IsAttentionDone(task);

    private static bool IsStaleInProgress(TaskItem task, DateTimeOffset now)
    {
        if (!IsAttentionStatus(task, "InProgress") || !task.StartDate.HasValue || !task.DueDate.HasValue)
        {
            return false;
        }

        var startDate = task.StartDate.Value;
        var dueDate = task.DueDate.Value;
        var total = dueDate - startDate;
        if (total.TotalMinutes <= 0)
        {
            return false;
        }

        var elapsed = now - startDate;
        return elapsed.TotalMinutes / total.TotalMinutes >= 0.7;
    }

    private static bool IsAttentionOverdue(TaskItem task, DateTimeOffset now)
        => task.DueDate.HasValue &&
           task.DueDate.Value < now &&
           !IsAttentionDone(task);

    private static bool IsAttentionDone(TaskItem task)
        => IsAttentionStatus(task, "Done") ||
           IsAttentionStatus(task, "Completed") ||
           IsAttentionStatus(task, "Closed");

    private static bool IsAttentionStatus(TaskItem task, string status)
        => string.Equals(task.Status, status, StringComparison.OrdinalIgnoreCase);

    private sealed record AttentionAssignee(Guid? UserId, string? FullName, DateTimeOffset AssignedAt);
}
