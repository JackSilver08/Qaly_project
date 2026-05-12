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

        return Result.Success(task.ToDto());
    }

    public async Task<Result<PagedResult<TaskItemDto>>> GetByProjectAsync(Guid projectId, string? status = null, string? priority = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
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

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByPlanningPriority()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<TaskItemDto>
        {
            Items = items.Select(item => item.ToDto()).ToList(),
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
            Items = items.Select(item => item.ToDto()).ToList(),
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

        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, dto.ProjectId, dto.AssigneeId, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Invalid task.", validation.StatusCode);
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
        await _auditLogService.LogAsync("Create", nameof(TaskItem), task.Id.ToString(), new { task.Title, task.ProjectId }, ct);

        // Realtime broadcast
        await _notificationService.BroadcastToProjectAsync(task.ProjectId, $"Task \"{task.Title}\" was created.", "TaskCreated", new { task.Id }, ct);
        await _webhookPublisher.PublishAsync(task.ProjectId, "task.created", new { task.Id, task.Title, task.Status }, ct);

        if (task.AssigneeId.HasValue && task.AssigneeId != currentUserId)
        {
            await _notificationService.CreateAsync(
                task.AssigneeId.Value,
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

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, task.ProjectId, dto.AssigneeId, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Invalid task.", validation.StatusCode);
        }

        if (!TaskStatusRules.IsValidStatus(dto.Status))
        {
            return Result.Failure<TaskItemDto>("Invalid task status.");
        }

        var previousAssignee = task.AssigneeId;
        dto.ApplyTo(task);
        task.Title = dto.Title.Trim();
        task.Status = TaskStatusRules.NormalizeStatus(dto.Status);
        task.Priority = TaskStatusRules.NormalizePriority(dto.Priority);

        await _taskRepo.UpdateAsync(task, ct);
        await AddToOutboxAsync("TaskUpdated", new { Id = task.Id }, ct);
        await _webhookPublisher.PublishAsync(task.ProjectId, "task.updated", new { task.Id, task.Title, task.Status }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(TaskItem), task.Id.ToString(), dto, ct);

        if (task.AssigneeId.HasValue && task.AssigneeId != previousAssignee)
        {
            await _notificationService.CreateAsync(
                task.AssigneeId.Value,
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

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        if (!TaskStatusRules.IsValidStatus(newStatus))
        {
            return Result.Failure("Invalid task status.");
        }

        var normalizedStatus = TaskStatusRules.NormalizeStatus(newStatus);
        if (!TaskStatusRules.CanTransition(task.Status, normalizedStatus))
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
        var task = await _taskRepo.GetQueryable()
            .WithProject()
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (task == null)
        {
            return Result.Failure("Task was not found.", 404);
        }

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
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

        if (!await _taskAccessPolicy.CanManageTaskAsync(task, ct))
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
            await _notificationService.BroadcastToProjectAsync(projectId, $"Task \"{title}\" was deleted.", "TaskDeleted", new { task.Id }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> BatchUpdateStatusAsync(IEnumerable<Guid> ids, string newStatus, CancellationToken ct = default)
    {
        if (!TaskStatusRules.IsValidStatus(newStatus))
        {
            return Result.Failure("Invalid task status.");
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
            if (!TaskStatusRules.CanTransition(task.Status, normalizedStatus)) continue;

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

    private async Task<TaskInputValidation> ValidateTaskInputAsync(string title, string priority, Guid projectId, Guid? assigneeId, CancellationToken ct)
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

        if (assigneeId.HasValue)
        {
            var userExists = await _userRepo.GetQueryable()
                .AnyAsync(user => user.Id == assigneeId.Value && user.IsActive, ct);

            if (!userExists)
            {
                return TaskInputValidation.Failure("Assignee was not found.", 404);
            }

            var isProjectUser = project.OwnerId == assigneeId.Value ||
                await _memberRepo.GetQueryable()
                    .AnyAsync(member => member.ProjectId == projectId && member.UserId == assigneeId.Value, ct);

            if (!isProjectUser)
            {
                return TaskInputValidation.Failure("Assignee must be a project member.", 400);
            }
        }

        return TaskInputValidation.Success(project);
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
}
