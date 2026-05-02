using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Domain.Enums;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class TaskService : ITaskService
{
    private static readonly Dictionary<string, string[]> StatusTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todo"] = ["InProgress", "Cancelled"],
        ["InProgress"] = ["Todo", "InReview", "Done", "Cancelled"],
        ["InReview"] = ["InProgress", "Done", "Cancelled"],
        ["Done"] = ["InReview"],
        ["Cancelled"] = ["Todo"]
    };

    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IAiService _aiService;

    public TaskService(
        IRepository<TaskItem> taskRepo,
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IAiService aiService)
    {
        _taskRepo = taskRepo;
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _aiService = aiService;
    }

    public async Task<Result<TaskItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await TaskDetailsQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task == null)
        {
            return Result.NotFound<TaskItemDto>();
        }

        if (!await CanAccessTaskAsync(task, ct))
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

        if (!await CanAccessProjectAsync(projectId, project.OwnerId, ct))
        {
            return Result.Forbidden<PagedResult<TaskItemDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = TaskDetailsQuery()
            .Where(t => t.ProjectId == projectId);

        query = ApplyPrivateTaskFilter(query);

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
            .OrderBy(t => t.Status == "InProgress" ? 0 :
                t.Status == "InReview" ? 1 :
                t.Status == "Todo" ? 2 :
                t.Status == "Done" ? 3 :
                t.Status == "Cancelled" ? 4 : 5)
            .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
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
        if (!IsAdmin() && _currentUserService.UserId != assigneeId)
        {
            return Result.Forbidden<PagedResult<TaskItemDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ApplyPrivateTaskFilter(TaskDetailsQuery())
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
        var currentUserId = _currentUserService.UserId;
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
        if (!await CanAccessProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var task = dto.ToEntity();
        task.Title = dto.Title.Trim();
        task.Priority = NormalizePriority(dto.Priority);
        task.ReporterId = currentUserId.Value;

        await _taskRepo.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(TaskItem), task.Id.ToString(), new { task.Title, task.ProjectId }, ct);

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

        var suggestion = await _aiService.SuggestTaskPriorityAsync(task.Title, task.Description ?? string.Empty, project.Name);
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

        if (!await CanManageTaskAsync(task, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var validation = await ValidateTaskInputAsync(dto.Title, dto.Priority, task.ProjectId, dto.AssigneeId, ct);
        if (!validation.IsSuccess)
        {
            return Result.Failure<TaskItemDto>(validation.Error ?? "Invalid task.", validation.StatusCode);
        }

        if (!IsValidStatus(dto.Status))
        {
            return Result.Failure<TaskItemDto>("Invalid task status.");
        }

        var previousAssignee = task.AssigneeId;
        dto.ApplyTo(task);
        task.Title = dto.Title.Trim();
        task.Status = NormalizeStatus(dto.Status);
        task.Priority = NormalizePriority(dto.Priority);

        await _taskRepo.UpdateAsync(task, ct);
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

        if (!await CanManageTaskAsync(task, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var normalizedStatus = NormalizeStatus(newStatus);
        if (!IsValidStatus(normalizedStatus))
        {
            return Result.Failure("Invalid task status.");
        }

        if (!CanTransition(task.Status, normalizedStatus))
        {
            return Result.Failure($"Cannot move task from {task.Status} to {normalizedStatus}.", 409);
        }

        var oldStatus = task.Status;
        task.Status = normalizedStatus;

        await _taskRepo.UpdateAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("StatusChange", nameof(TaskItem), task.Id.ToString(), new { oldStatus, newStatus = normalizedStatus }, ct);

        await NotifyStatusChangeAsync(task, oldStatus, normalizedStatus, ct);

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

        if (!await CanManageTaskAsync(task, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        await _taskRepo.DeleteAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(TaskItem), id.ToString(), new { task.Title }, ct);

        return Result.Success();
    }

    private IQueryable<TaskItem> TaskDetailsQuery()
        => _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Reporter)
            .Include(t => t.Comments)
            .Include(t => t.Attachments);

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
        return task.ReporterId == currentUserId || task.AssigneeId == currentUserId || task.Project.OwnerId == currentUserId;
    }

    private async Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || task.ReporterId == currentUserId || task.AssigneeId == currentUserId || task.Project.OwnerId == currentUserId)
        {
            return true;
        }

        return await _memberRepo.GetQueryable()
            .AnyAsync(member =>
                member.ProjectId == task.ProjectId &&
                member.UserId == currentUserId &&
                (member.Role == "Admin" || member.Role == "Owner"),
                ct);
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

    private async Task<TaskInputValidation> ValidateTaskInputAsync(string title, string priority, Guid projectId, Guid? assigneeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return TaskInputValidation.Failure("Task title is required.");
        }

        if (!IsValidPriority(priority))
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
        var currentUserId = _currentUserService.UserId;
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
    }

    private bool IsAdmin()
        => string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static bool CanTransition(string oldStatus, string newStatus)
        => string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase) ||
           (StatusTransitions.TryGetValue(oldStatus, out var allowed) && allowed.Contains(newStatus, StringComparer.OrdinalIgnoreCase));

    private static bool IsValidStatus(string status)
        => Enum.TryParse<TaskItemStatus>(status, ignoreCase: true, out _);

    private static bool IsValidPriority(string priority)
        => Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out _);

    private static string NormalizeStatus(string status)
        => Enum.Parse<TaskItemStatus>(status, ignoreCase: true).ToString();

    private static string NormalizePriority(string priority)
        => Enum.Parse<TaskPriority>(priority, ignoreCase: true).ToString();

    private sealed record TaskInputValidation(bool IsSuccess, string? Error, int StatusCode, Project? Project)
    {
        public static TaskInputValidation Success(Project project)
            => new(true, null, 200, project);

        public static TaskInputValidation Failure(string error, int statusCode = 400)
            => new(false, error, statusCode, null);
    }
}
