using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;

namespace Qaly.Application.Services;

public interface IProjectService
{
    Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> AddMemberAsync(Guid projectId, Guid userId, string role, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);
}
