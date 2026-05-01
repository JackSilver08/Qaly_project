using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ProjectService(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> memberRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetQueryable()
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project == null) return Result.NotFound<ProjectDto>();

        return Result.Success(project.ToDto());
    }

    public async Task<Result<PagedResult<ProjectDto>>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default)
    {
        var query = _projectRepo.GetQueryable()
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Include(p => p.Tasks)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(static item => item.ToDto()).ToList();

        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PagedResult<ProjectDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var query = _projectRepo.GetQueryable()
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Include(p => p.Tasks)
            .Where(p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(static item => item.ToDto()).ToList();

        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<ProjectDto>();

        var project = dto.ToEntity();
        project.OwnerId = currentUserId.Value;

        await _projectRepo.AddAsync(project, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Tải lại để có thông tin người phụ trách trong DTO.
        return await GetByIdAsync(project.Id, ct);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(id, ct);
        if (project == null) return Result.NotFound<ProjectDto>();

        if (project.OwnerId != _currentUserService.UserId) return Result.Forbidden<ProjectDto>();

        dto.ApplyTo(project);
        await _projectRepo.UpdateAsync(project, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(id, ct);
        if (project == null) return Result.Failure("Không tìm thấy dự án", 404);

        if (project.OwnerId != _currentUserService.UserId) return Result.Failure("Không có quyền truy cập", 403);

        await _projectRepo.DeleteAsync(project, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> AddMemberAsync(Guid projectId, Guid userId, string role, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.Failure("Không tìm thấy dự án", 404);

        if (project.OwnerId != _currentUserService.UserId) return Result.Failure("Không có quyền truy cập", 403);

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role
        };

        await _memberRepo.AddAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

        if (member == null) return Result.Failure("Không tìm thấy thành viên", 404);

        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project?.OwnerId != _currentUserService.UserId) return Result.Failure("Không có quyền truy cập", 403);

        await _memberRepo.DeleteAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
