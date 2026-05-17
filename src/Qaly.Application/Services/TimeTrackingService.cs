using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Application.Services;

public class TimeTrackingService : ITimeTrackingService
{
    private readonly IRepository<TimeEntry> _timeRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public TimeTrackingService(
        IRepository<TimeEntry> timeRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _timeRepo = timeRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TimeEntryDto>> StartTimerAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<TimeEntryDto>();

        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return Result.NotFound<TimeEntryDto>();

        if (!await IsProjectMember(task.ProjectId, userId.Value, ct)) return Result.Forbidden<TimeEntryDto>();

        // Stop existing timers for this user
        var activeTimers = await _timeRepo.GetQueryable()
            .Where(te => te.UserId == userId.Value && te.EndedAt == null)
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
            .Include(te => te.User)
            .FirstOrDefaultAsync(te => te.Id == entryId, ct);

        if (entry == null) return Result.NotFound<TimeEntryDto>();
        if (entry.UserId != _currentUserService.UserId) return Result.Forbidden<TimeEntryDto>();

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

        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == dto.TaskId, ct);
        if (task == null) return Result.NotFound<TimeEntryDto>();

        if (!await IsProjectMember(task.ProjectId, userId.Value, ct)) return Result.Forbidden<TimeEntryDto>();

        var entry = new TimeEntry
        {
            TaskId = dto.TaskId,
            UserId = userId.Value,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            ManualMinutes = dto.ManualMinutes,
            Note = dto.Note
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
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return Result.NotFound<List<TimeEntryDto>>();

        if (!await IsProjectMember(task.ProjectId, userId.Value, ct))
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

        if (!await IsProjectMember(projectId, userId.Value, ct))
            return Result.Forbidden<List<TimeEntryDto>>();

        var query = _timeRepo.GetQueryable()
            .Include(te => te.Task)
            .Include(te => te.User)
            .Where(te => te.Task.ProjectId == projectId);

        if (from.HasValue) query = query.Where(te => te.StartedAt >= from.Value);
        if (endAt.HasValue) query = query.Where(te => te.StartedAt <= endAt.Value);

        var entries = await query.OrderByDescending(te => te.StartedAt).ToListAsync(ct);
        return Result.Success(entries.Select(e => MapToDto(e, e.Task.Title, e.User.FullName)).ToList());
    }

    private async Task<bool> IsProjectMember(Guid projectId, Guid userId, CancellationToken ct)
    {
        if (string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if the user is the project owner
        var isOwner = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Select(t => t.Project)
            .AnyAsync(p => p.Id == projectId && p.OwnerId == userId, ct);
        if (isOwner) return true;

        return await _memberRepo.GetQueryable()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);
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
