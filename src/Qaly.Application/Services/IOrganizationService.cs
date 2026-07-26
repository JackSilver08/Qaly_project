using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;

namespace Qaly.Application.Services;

public interface IOrganizationService
{
    Task<Result<OrganizationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<OrganizationDto>>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default);
    Task<Result<OrganizationDto>> CreateAsync(CreateOrganizationDto dto, CancellationToken ct = default);
    Task<Result<OrganizationDto>> UpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken ct = default);
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OrganizationMemberDto>>> GetMembersAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result> AddMemberAsync(Guid organizationId, Guid userId, string role, CancellationToken ct = default);
    Task<Result> AddMemberByEmailAsync(Guid organizationId, string email, string role, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetCurrentModeratorCapabilitiesAsync(Guid organizationId, CancellationToken ct = default);
}
