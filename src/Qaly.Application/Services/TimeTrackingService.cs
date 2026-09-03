using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services.Tasks;

namespace Qaly.Application.Services;

public class TimeTrackingService : ITimeTrackingService
{
    private readonly IRepository<TimeEntry> _timeRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _accessPolicy;

    public TimeTrackingService(
        IRepository<TimeEntry> timeRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<Project> projectRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy accessPolicy)
    {
        _timeRepo = timeRepo;
        _taskRepo = taskRepo;
        _projectRepo = projectRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _accessPolicy = accessPolicy;
    }

    public async Task<Result<TimeEntryDto>> StartTimerAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<TimeEntryDto>();

        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return Result.NotFound<TimeEntryDto>();
        if (task.Project == null) return Result.NotFound<TimeEntryDto>();

        if (!await _accessPolicy.CanContributeToTaskAsync(task, ct)) return Result.Forbidden<TimeEntryDto>();

        // Stop existing timers for this user
        var activeTimers = await _timeRepo.GetQueryable()
            .Where(te => te.UserId == userId.Value && te.EndedAt == null && te.ManualMinutes == null)
            .ToListAsync(ct);

        foreach (var timer in activeTimers)
        {
            timer.EndedAt = DateTimeOffset.UtcNow;
            await _timeRepo.UpdateAsync(timer, ct);
        }

        var entry = new TimeEntry
        {
            TaskId = taskId,
            UserId = userId.Value,
            StartedAt = DateTimeOffset.UtcNow
        };

        await _timeRepo.AddAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(MapToDto(entry, task.Title, _currentUserService.UserName ?? "Unknown"));
    }

    public async Task<Result<TimeEntryDto>> StopTimerAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _timeRepo.GetQueryable()
            .Include(te => te.Task)
                .ThenInclude(task => task.Project)
            .Include(te => te.Task)
                .ThenInclude(task => task.Assignees)
            .Include(te => te.User)
            .FirstOrDefaultAsync(te => te.Id == entryId, ct);

        if (entry == null) return Result.NotFound<TimeEntryDto>();
        if (entry.UserId != _currentUserService.UserId) return Result.Forbidden<TimeEntryDto>();
        if (!await _accessPolicy.CanAccessTaskAsync(entry.Task, ct)) return Result.Forbidden<TimeEntryDto>();

        if (entry.EndedAt != null) return Result.Failure<TimeEntryDto>("Timer already stopped.");

        entry.EndedAt = DateTimeOffset.UtcNow;
        await _timeRepo.UpdateAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(MapToDto(entry, entry.Task.Title, entry.User.FullName));
    }

    public async Task<Result<TimeEntryDto>> AddManualEntryAsync(CreateTimeEntryDto dto, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<TimeEntryDto>();

        var now = DateTimeOffset.UtcNow;
        if (dto.StartedAt > now.AddMinutes(5))
            return Result.Failure<TimeEntryDto>("Manual time cannot start in the future.", 400);
        if (dto.ManualMinutes is null && dto.EndedAt is null)
            return Result.Failure<TimeEntryDto>("Manual minutes or an end time is required.", 400);
        if (dto.ManualMinutes is <= 0 or > 1440)
            return Result.Failure<TimeEntryDto>("Manual minutes must be between 1 and 1,440.", 400);
        if (dto.ManualMinutes.HasValue && dto.EndedAt.HasValue)
            return Result.Failure<TimeEntryDto>("Use manual minutes or an end time, not both.", 400);
        if (dto.EndedAt.HasValue && dto.EndedAt.Value < dto.StartedAt)
            return Result.Failure<TimeEntryDto>("End time cannot be before start time.", 400);
        if (dto.EndedAt > now.AddMinutes(5))
            return Result.Failure<TimeEntryDto>("Manual time cannot end in the future.", 400);
        if (dto.Note?.Trim().Length > 500)
            return Result.Failure<TimeEntryDto>("Note cannot exceed 500 characters.", 400);

        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == dto.TaskId, ct);
        if (task == null) return Result.NotFound<TimeEntryDto>();
        if (task.Project == null) return Result.NotFound<TimeEntryDto>();

        if (!await _accessPolicy.CanContributeToTaskAsync(task, ct)) return Result.Forbidden<TimeEntryDto>();

        var entry = new TimeEntry
        {
            TaskId = dto.TaskId,
            UserId = userId.Value,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            ManualMinutes = dto.ManualMinutes,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
        };

        await _timeRepo.AddAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(MapToDto(entry, task.Title, _currentUserService.UserName ?? "Unknown"));
    }

    public async Task<Result<List<TimeEntryDto>>> GetByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<List<TimeEntryDto>>();

        var task = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return Result.NotFound<List<TimeEntryDto>>();
        if (task.Project == null) return Result.NotFound<List<TimeEntryDto>>();

        if (!await _accessPolicy.CanAccessTaskAsync(task, ct) ||
            !await _accessPolicy.CanViewProjectWorkloadAsync(task.ProjectId, task.Project.OwnerId, ct))
            return Result.Forbidden<List<TimeEntryDto>>();

        var entries = await _timeRepo.GetQueryable()
            .Include(te => te.Task)
            .Include(te => te.User)
            .Where(te => te.TaskId == taskId)
            .OrderByDescending(te => te.StartedAt)
            .ToListAsync(ct);

        return Result.Success(entries.Select(e => MapToDto(e, e.Task.Title, e.User.FullName)).ToList());
    }

    public async Task<Result<List<TimeEntryDto>>> GetByProjectAsync(Guid projectId, DateTimeOffset? from = null, DateTimeOffset? endAt = null, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<List<TimeEntryDto>>();
        if (from.HasValue && endAt.HasValue && from.Value > endAt.Value)
            return Result.Failure<List<TimeEntryDto>>("The start of the range cannot be after its end.", 400);

        var project = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);
        if (project == null) return Result.NotFound<List<TimeEntryDto>>();

        if (!await _accessPolicy.CanViewProjectWorkloadAsync(projectId, project.OwnerId, ct))
            return Result.Forbidden<List<TimeEntryDto>>();

        var visibleTaskIds = _accessPolicy.ApplyVisibilityFilter(_taskRepo.GetQueryable())
            .Where(task => task.ProjectId == projectId)
            .Select(task => task.Id);

        var query = _timeRepo.GetQueryable()
            .Include(te => te.Task)
            .Include(te => te.User)
            .Where(te => visibleTaskIds.Contains(te.TaskId));

        if (from.HasValue) query = query.Where(te => te.StartedAt >= from.Value);
        if (endAt.HasValue) query = query.Where(te => te.StartedAt <= endAt.Value);

        var entries = await query.OrderByDescending(te => te.StartedAt).ToListAsync(ct);
        return Result.Success(entries.Select(e => MapToDto(e, e.Task.Title, e.User.FullName)).ToList());
    }

    private static TimeEntryDto MapToDto(TimeEntry e, string taskTitle, string userName)
    {
        return new TimeEntryDto(
            e.Id,
            e.TaskId,
            taskTitle,
            e.UserId,
            userName,
            e.StartedAt,
            e.EndedAt,
            e.ManualMinutes,
            e.TotalMinutes,
            e.Note,
            e.CreatedAt);
    }
}
