using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Domain.Enums;
using Qaly.Domain.Interfaces;
using System.Text.Json;

namespace Qaly.Application.Services;

public class TaskService : ITaskService
{
    private static readonly Dictionary<string, string[]> StatusTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todo"] = ["InProgress", "OnHold", "Cancelled"],
        ["InProgress"] = ["Todo", "OnHold", "InReview", "Done", "Cancelled"],
        ["OnHold"] = ["Todo", "InProgress", "Cancelled"],
        ["InReview"] = ["InProgress", "OnHold", "Done", "Cancelled"],
        ["Done"] = ["InReview"],
        ["Cancelled"] = ["Todo"]
    };

    private static readonly Dictionary<string, string[]> MemberStatusTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todo"] = ["InProgress"],
        ["InProgress"] = ["OnHold", "InReview"],
        ["OnHold"] = ["InProgress"],
        ["InReview"] = ["InProgress"]
    };

    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskDependency> _dependencyRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<TaskAssignment> _assignmentRepo;
    private readonly IRepository<TaskLabel> _taskLabelRepo;
    private readonly IRepository<ProjectLabel> _projectLabelRepo;
    private readonly IRepository<VectorSyncOutbox> _outboxRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
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
        IRepository<TaskAssignment> assignmentRepo,
        IRepository<TaskLabel> taskLabelRepo,
        IRepository<ProjectLabel> projectLabelRepo,
        IRepository<VectorSyncOutbox> outboxRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
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
        _assignmentRepo = assignmentRepo;
        _taskLabelRepo = taskLabelRepo;
        _projectLabelRepo = projectLabelRepo;
        _outboxRepo = outboxRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
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

        if (!await CanAccessProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
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

        if (!await CanAccessProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<PagedResult<TaskItemDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = TaskDetailsQuery()
            .Where(t => t.ProjectId == projectId);

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
        if (!IsAdmin() && _currentUserService.UserId != assigneeId)
        {
            return Result.Forbidden<PagedResult<TaskItemDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = TaskDetailsQuery()
            .Where(t => t.AssigneeId == assigneeId || t.Assignees.Any(assignment => assignment.UserId == assigneeId));

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

    public async Task<Result<TaskItemDto>> CreateAsync(CreateTaskDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var assigneeIds = NormalizeAssigneeIds(dto.AssigneeId, dto.AssigneeIds);
        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, dto.ProjectId, assigneeIds, dto.LabelIds, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Invalid task.", validation.StatusCode);
        }

        var project = validation.Project!;
        if (!await CanWriteProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var task = dto.ToEntity();
        task.Title = dto.Title.Trim();
        task.Priority = NormalizePriority(dto.Priority);
        task.AssigneeId = assigneeIds.Count > 0 ? assigneeIds[0] : null;
        if (task.AssigneeId == Guid.Empty)
        {
            task.AssigneeId = null;
        }
        task.ReporterId = currentUserId.Value;

        await _taskRepo.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await SyncAssignmentsAsync(task.Id, assigneeIds, ct);
        await SyncLabelsAsync(task.Id, task.ProjectId, dto.LabelIds, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(TaskItem), task.Id.ToString(), new { task.Title, task.ProjectId }, ct);

        // Realtime broadcast
        await _notificationService.BroadcastToProjectAsync(task.ProjectId, $"Task \"{task.Title}\" was created.", "TaskCreated", new { task.Id }, ct);
        await _webhookPublisher.PublishAsync(task.ProjectId, "task.created", new { task.Id, task.Title, task.Status }, ct);

        foreach (var assigneeId in assigneeIds.Where(id => id != currentUserId).Distinct())
        {
            await _notificationService.CreateAsync(
                assigneeId,
                $"You were assigned to task \"{task.Title}\".",
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

        if (!await CanEditTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var assigneeIds = NormalizeAssigneeIds(dto.AssigneeId, dto.AssigneeIds);
        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, task.ProjectId, assigneeIds, dto.LabelIds, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Invalid task.", validation.StatusCode);
        }

        if (!IsValidStatus(NormalizeStatus(dto.Status)))
        {
            return Result.Failure<TaskItemDto>("Invalid task status.");
        }

        var previousAssignees = task.Assignees.Select(assignment => assignment.UserId).ToHashSet();
        dto.ApplyTo(task);
        task.Title = dto.Title.Trim();
        task.Status = NormalizeStatus(dto.Status);
        task.Priority = NormalizePriority(dto.Priority);
        task.AssigneeId = assigneeIds.Count > 0 ? assigneeIds[0] : null;
        if (task.AssigneeId == Guid.Empty)
        {
            task.AssigneeId = null;
        }

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
                $"You were assigned to task \"{task.Title}\".",
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
            return Result.Failure("Task was not found.", 404);
        }

        if (!await CanChangeStatusAsync(task, newStatus, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var normalizedStatus = NormalizeStatus(newStatus);
        if (!IsValidStatus(normalizedStatus))
        {
            return Result.Failure("Invalid task status.");
        }

        if (!await CanTransitionAsync(task, normalizedStatus, ct))
        {
            return Result.Failure($"Cannot move task from {task.Status} to {normalizedStatus}.", 409);
        }

        var oldStatus = task.Status;
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
        var task = await _taskRepo.GetByIdAsync(id, ct);
        if (task == null)
        {
            return Result.Failure("Task was not found.", 404);
        }

        if (!await CanPinTaskAsync(task, ct))
        {
            return Result.Failure("Access denied.", 403);
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
            return Result.Failure("Task was not found.", 404);
        }

        if (!await CanDeleteTaskAsync(task, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var projectId = task.ProjectId;
        var title = task.Title;

        await _taskRepo.DeleteAsync(task, ct);
        await AddToOutboxAsync("TaskDeleted", new { Id = task.Id }, ct);
        await _webhookPublisher.PublishAsync(projectId, "task.deleted", new { id, title }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(TaskItem), id.ToString(), new { task.Title }, ct);

        // Realtime broadcast
        await _notificationService.BroadcastToProjectAsync(projectId, $"Task \"{title}\" was deleted.", "TaskDeleted", new { id }, ct);

        return Result.Success();
    }

    public async Task<Result> BatchDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var tasks = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);

        if (!tasks.Any()) return Result.Success();

        foreach (var task in tasks)
        {
            if (!await CanDeleteTaskAsync(task, ct)) continue;

            var projectId = task.ProjectId;
            var title = task.Title;

            await _taskRepo.DeleteAsync(task, ct);
            await AddToOutboxAsync("TaskDeleted", new { Id = task.Id }, ct);
            await _webhookPublisher.PublishAsync(projectId, "task.deleted", new { task.Id, title }, ct);
            await _auditLogService.LogAsync("Delete", nameof(TaskItem), task.Id.ToString(), new { task.Title }, ct);
            await _notificationService.BroadcastToProjectAsync(projectId, $"Task \"{title}\" was deleted.", "TaskDeleted", new { task.Id }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> BatchUpdateStatusAsync(IEnumerable<Guid> ids, string newStatus, CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeStatus(newStatus);
        if (!IsValidStatus(normalizedStatus))
        {
            return Result.Failure("Invalid task status.");
        }

        var tasks = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);

        if (!tasks.Any()) return Result.Success();

        foreach (var task in tasks)
        {
            if (!await CanChangeStatusAsync(task, normalizedStatus, ct)) continue;
            if (!await CanTransitionAsync(task, normalizedStatus, ct)) continue;

            var oldStatus = task.Status;
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

        if (!await CanAccessProjectAsync(projectId, project.OwnerId, ct))
            return Result.Forbidden<IEnumerable<GanttTaskDto>>();

        var tasks = await ApplyPrivateTaskFilter(_taskRepo.GetQueryable())
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

        CalculateCriticalPath(dtos);

        return Result.Success<IEnumerable<GanttTaskDto>>(dtos);
    }

    private void CalculateCriticalPath(List<GanttTaskDto> tasks)
    {
        if (tasks == null || !tasks.Any()) return;

        var tasksWithDates = tasks.Where(t => t.StartDate.HasValue && t.EndDate.HasValue).ToList();
        if (!tasksWithDates.Any()) return;

        // 1. Forward Pass: Earliest Start (ES) and Earliest Finish (EF)
        var earlyStart = new Dictionary<Guid, DateTimeOffset>();
        var earlyFinish = new Dictionary<Guid, DateTimeOffset>();

        foreach (var task in tasksWithDates.OrderBy(t => t.StartDate))
        {
            var predecessors = tasksWithDates.Where(t => task.Dependencies.Contains(t.Id)).ToList();
            var es = task.StartDate!.Value;

            if (predecessors.Any())
            {
                var maxEF = predecessors.Max(p => earlyFinish.TryGetValue(p.Id, out var ef) ? ef : p.EndDate!.Value);
                if (maxEF > es) es = maxEF;
            }

            earlyStart[task.Id] = es;
            var duration = task.EndDate!.Value - task.StartDate!.Value;
            earlyFinish[task.Id] = es.Add(duration);
        }

        // 2. Backward Pass: Latest Start (LS) and Latest Finish (LF)
        var lateStart = new Dictionary<Guid, DateTimeOffset>();
        var projectFinish = earlyFinish.Values.Any() ? earlyFinish.Values.Max() : DateTimeOffset.MinValue;

        foreach (var task in tasksWithDates.OrderByDescending(t => t.EndDate))
        {
            var successors = tasksWithDates.Where(t => t.Dependencies.Contains(task.Id)).ToList();
            var lf = projectFinish;

            if (successors.Any())
            {
                lf = successors.Min(s => lateStart.TryGetValue(s.Id, out var ls) ? ls : s.StartDate!.Value);
            }

            var duration = task.EndDate!.Value - task.StartDate!.Value;
            lateStart[task.Id] = lf.Subtract(duration);
        }

        // 3. Mark Critical Path: Slack (LS - ES) == 0
        foreach (var task in tasksWithDates)
        {
            if (earlyStart.TryGetValue(task.Id, out var es) && lateStart.TryGetValue(task.Id, out var ls))
            {
                if (Math.Abs((ls - es).TotalHours) < 0.01)
                {
                    task.IsCriticalPath = true;
                }
            }
        }
    }

    public async Task<Result> UpdateDatesAsync(Guid taskId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetByIdAsync(taskId, ct);
        if (task == null) return Result.Failure("Task was not found.", 404);

        if (!await CanManageTaskAsync(task, ct)) return Result.Failure("Access denied.", 403);

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            return Result.Failure("Start date cannot be after end date.");

        task.StartDate = startDate;
        task.DueDate = endDate;

        await _taskRepo.UpdateAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await AddToOutboxAsync("TaskUpdated", new { Id = task.Id }, ct);

        return Result.Success();
    }

    public async Task<Result> AddDependencyAsync(Guid predecessorId, Guid successorId, string type = "FinishToStart", CancellationToken ct = default)
    {
        if (predecessorId == successorId) return Result.Failure("Cannot depend on itself.");

        var predecessor = await _taskRepo.GetByIdAsync(predecessorId, ct);
        var successor = await _taskRepo.GetByIdAsync(successorId, ct);

        if (predecessor == null || successor == null) return Result.Failure("Task not found.", 404);

        if (predecessor.ProjectId != successor.ProjectId)
            return Result.Failure("Tasks must be in the same project.");

        if (!await CanManageTaskAsync(successor, ct)) return Result.Failure("Access denied.", 403);

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
            .FirstOrDefaultAsync(d => d.Id == dependencyId, ct);

        if (dependency == null) return Result.Failure("Dependency not found.", 404);

        if (!await CanManageTaskAsync(dependency.Successor, ct)) return Result.Failure("Access denied.", 403);

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
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Assignees)
                .ThenInclude(a => a.User)
            .Include(t => t.Labels)
                .ThenInclude(l => l.ProjectLabel)
            .Include(t => t.Reporter)
            .Include(t => t.Comments)
            .Include(t => t.Attachments);

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
                .ThenBy(t => t.Status == "InProgress" ? 0 :
                    t.Status == "InReview" ? 1 :
                    t.Status == "Todo" ? 2 :
                    t.Status == "OnHold" ? 3 :
                    t.Status == "Done" ? 4 :
                    t.Status == "Cancelled" ? 5 : 6)
                .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
                .ThenByDescending(t => t.CreatedAt)
        };

    private IQueryable<TaskItem> ApplyPrivateTaskFilter(IQueryable<TaskItem> query)
    {
        if (IsAdmin())
        {
            return query;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return query.Where(task => false);
        }

        return query.Where(task =>
            !task.IsPrivate ||
            task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Project.OwnerId == currentUserId);
    }

    private async Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct)
        => await CanAccessProjectAsync(task.ProjectId, task.Project.OwnerId, ct)
           && (!task.IsPrivate || await CanViewTaskDetailsAsync(task, ct));

    private async Task<bool> CanViewTaskDetailsAsync(TaskItem task, CancellationToken ct)
    {
        if (IsAdmin())
        {
            return true;
        }

        if (!await CanAccessProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
        {
            return false;
        }

        if (!task.IsPrivate)
        {
            return true;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
            task.Project.OwnerId == currentUserId)
        {
            return true;
        }

        return await IsProjectManagerAsync(task.ProjectId, currentUserId.Value, ct);
    }

    private async Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct)
        => await CanEditTaskAsync(task, ct);

    private async Task<bool> CanEditTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || task.Project.OwnerId == currentUserId || await IsProjectManagerAsync(task.ProjectId, currentUserId.Value, ct))
        {
            return true;
        }

        var role = await GetProjectRoleAsync(task.ProjectId, currentUserId.Value, ct);
        if (!ProjectRoleRules.CanWrite(role))
        {
            return false;
        }

        return task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId);
    }

    private async Task<bool> CanDeleteTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        return IsAdmin() || task.Project.OwnerId == currentUserId || await IsProjectManagerAsync(task.ProjectId, currentUserId.Value, ct);
    }

    private Task<bool> CanPinTaskAsync(TaskItem task, CancellationToken ct)
        => CanDeleteTaskAsync(task, ct);

    private async Task<bool> CanChangeStatusAsync(TaskItem task, string newStatus, CancellationToken ct)
    {
        var normalized = NormalizeStatus(newStatus);
        if (!IsValidStatus(normalized))
        {
            return false;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || task.Project.OwnerId == currentUserId || await IsProjectManagerAsync(task.ProjectId, currentUserId.Value, ct))
        {
            return true;
        }

        if (normalized is "Done" or "Cancelled")
        {
            return false;
        }

        return await CanEditTaskAsync(task, ct);
    }

    private async Task<bool> CanTransitionAsync(TaskItem task, string newStatus, CancellationToken ct)
    {
        if (string.Equals(task.Status, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var currentUserId = _currentUserService.UserId;
        var transitions = currentUserId.HasValue &&
            (IsAdmin() || task.Project.OwnerId == currentUserId || await IsProjectManagerAsync(task.ProjectId, currentUserId.Value, ct))
            ? StatusTransitions
            : MemberStatusTransitions;

        return transitions.TryGetValue(task.Status, out var allowed) &&
            allowed.Contains(newStatus, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || ownerId == currentUserId)
        {
            return true;
        }

        return await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUserId, ct);
    }

    private async Task<bool> CanWriteProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || ownerId == currentUserId)
        {
            return true;
        }

        var role = await GetProjectRoleAsync(projectId, currentUserId.Value, ct);
        return ProjectRoleRules.CanWrite(role);
    }

    private async Task<bool> IsProjectManagerAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var role = await GetProjectRoleAsync(projectId, userId, ct);
        return ProjectRoleRules.IsProjectManager(role);
    }

    private async Task<string?> GetProjectRoleAsync(Guid projectId, Guid userId, CancellationToken ct)
        => await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

    private async Task<TaskInputValidation> ValidateTaskInputAsync(string title, string priority, Guid projectId, IReadOnlyList<Guid> assigneeIds, IReadOnlyList<Guid>? labelIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return TaskInputValidation.Failure("Task title is required.");
        }

        if (!IsValidPriority(NormalizePriority(priority)))
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

    private static IReadOnlyList<Guid> NormalizeAssigneeIds(Guid? primaryAssigneeId, IReadOnlyList<Guid>? assigneeIds)
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
            await _assignmentRepo.AddAsync(new TaskAssignment { TaskItemId = taskId, UserId = userId }, ct);
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
        var currentUserId = _currentUserService.UserId;
        
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

    private bool IsAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

    private static bool CanTransition(string oldStatus, string newStatus)
        => string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase) ||
           (StatusTransitions.TryGetValue(oldStatus, out var allowed) && allowed.Contains(newStatus, StringComparer.OrdinalIgnoreCase));

    private static bool IsValidStatus(string status)
        => Enum.TryParse<TaskItemStatus>(status, ignoreCase: true, out _);

    private static bool IsValidPriority(string priority)
        => Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out _);

    private static string NormalizeStatus(string status)
    {
        var normalized = (status ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized switch
        {
            "New" => nameof(TaskItemStatus.Todo),
            "Doing" => nameof(TaskItemStatus.InProgress),
            "InProgress" => nameof(TaskItemStatus.InProgress),
            "OnHold" => nameof(TaskItemStatus.OnHold),
            "Review" or "InReview" => nameof(TaskItemStatus.InReview),
            "Completed" or "Complete" or "Hoànthành" => nameof(TaskItemStatus.Done),
            "Done" => nameof(TaskItemStatus.Done),
            "Cancelled" or "Canceled" => nameof(TaskItemStatus.Cancelled),
            _ => status.Trim()
        };
    }

    private static string NormalizePriority(string priority)
    {
        var normalized = (priority ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized switch
        {
            "Thấp" or "Thap" => nameof(TaskPriority.Low),
            "Trungbình" or "Trungbinh" => nameof(TaskPriority.Medium),
            "Cao" => nameof(TaskPriority.High),
            "Khẩncấp" or "KhanCap" => nameof(TaskPriority.Critical),
            _ => Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out var parsed) ? parsed.ToString() : priority.Trim()
        };
    }

    private sealed record TaskInputValidation(bool IsSuccess, string? Error, int StatusCode, Project? Project)
    {
        public static TaskInputValidation Success(Project project)
            => new(true, null, 200, project);

        public static TaskInputValidation Failure(string error, int statusCode = 400)
            => new(false, error, statusCode, null);
    }
}
