using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services;

public interface ITaskSkillService
{
    Task<Result<IReadOnlyList<OrganizationSkillDto>>> GetOrganizationSkillsAsync(
        Guid organizationId,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<Result<OrganizationSkillDto>> CreateOrganizationSkillAsync(
        Guid organizationId,
        CreateOrganizationSkillDto dto,
        CancellationToken ct = default);

    Task<Result<OrganizationSkillDto>> UpdateOrganizationSkillAsync(
        Guid organizationId,
        Guid skillId,
        UpdateOrganizationSkillDto dto,
        CancellationToken ct = default);

    Task<Result<TaskSkillsDto>> GetTaskSkillsAsync(Guid taskId, CancellationToken ct = default);

    Task<Result<TaskSkillsDto>> ReplaceTaskSkillsAsync(
        Guid taskId,
        ReplaceTaskSkillsDto dto,
        CancellationToken ct = default);
}
