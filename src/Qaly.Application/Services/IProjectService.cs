using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;

namespace Qaly.Application.Services;

public interface IProjectService
{
    Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetByOrganizationAsync(Guid organizationId, int page = 1, int pageSize = 100, string? search = null, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> AddMemberAsync(Guid projectId, Guid userId, string role, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<Result> UpdateMemberPermissionsAsync(Guid projectId, Guid userId, UpdateProjectMemberPermissionsDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProjectLabelDto>>> GetLabelsAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ProjectLabelDto>> CreateLabelAsync(Guid projectId, CreateProjectLabelDto dto, CancellationToken ct = default);
    Task<Result<ProjectLabelDto>> UpdateLabelAsync(Guid projectId, Guid labelId, UpdateProjectLabelDto dto, CancellationToken ct = default);
    Task<Result> DeleteLabelAsync(Guid projectId, Guid labelId, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetTrashAsync(int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<Result<PagedResult<ProjectDto>>> GetArchivedAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default);
    Task<Result> RestoreAsync(Guid id, CancellationToken ct = default);
    Task<Result> HardDeleteAsync(Guid id, CancellationToken ct = default);
}
