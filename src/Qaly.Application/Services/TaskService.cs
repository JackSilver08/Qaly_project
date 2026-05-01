using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class TaskService : ITaskService
{
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public TaskService(
        IRepository<TaskItem> taskRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _taskRepo = taskRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TaskItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Reporter)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task == null) return Result.NotFound<TaskItemDto>();

        return Result.Success(task.ToDto());
    }

    public async Task<Result<PagedResult<TaskItemDto>>> GetByProjectAsync(Guid projectId, string? status = null, string? priority = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _taskRepo.GetQueryable()
            .Include(t => t.Assignee)
            .Include(t => t.Reporter)
            .Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrEmpty(priority)) query = query.Where(t => t.Priority == priority);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(static item => item.ToDto()).ToList();

        return Result.Success(new PagedResult<TaskItemDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PagedResult<TaskItemDto>>> GetByAssigneeAsync(Guid assigneeId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .Include(t => t.Reporter)
            .Where(t => t.AssigneeId == assigneeId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(static item => item.ToDto()).ToList();

        return Result.Success(new PagedResult<TaskItemDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<TaskItemDto>> CreateAsync(CreateTaskDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<TaskItemDto>();

        var task = dto.ToEntity();
        task.ReporterId = currentUserId.Value;

        await _taskRepo.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(task.Id, ct);
    }

    public async Task<Result<TaskItemDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetByIdAsync(id, ct);
        if (task == null) return Result.NotFound<TaskItemDto>();

        dto.ApplyTo(task);
        await _taskRepo.UpdateAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> UpdateStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetByIdAsync(id, ct);
        if (task == null) return Result.Failure("Không tìm thấy công việc", 404);

        task.Status = newStatus;
        await _taskRepo.UpdateAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetByIdAsync(id, ct);
        if (task == null) return Result.Failure("Không tìm thấy công việc", 404);

        await _taskRepo.DeleteAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
