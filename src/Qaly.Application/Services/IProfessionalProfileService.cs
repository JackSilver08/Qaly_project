using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Organization;

namespace Qaly.Application.Services;

public interface IProfessionalProfileService
{
    Task<Result<IReadOnlyList<ProfessionalProfileDefinitionDto>>> GetDefinitionsAsync(Guid organizationId, bool includeInactive = false, CancellationToken ct = default);
    Task<Result<ProfessionalProfileDefinitionDto>> CreateDefinitionAsync(Guid organizationId, CreateProfessionalProfileDefinitionDto dto, CancellationToken ct = default);
    Task<Result<ProfessionalProfileDefinitionDto>> UpdateDefinitionAsync(Guid organizationId, Guid definitionId, UpdateProfessionalProfileDefinitionDto dto, CancellationToken ct = default);
    Task<Result<MemberProfessionalProfileSetDto>> GetMemberProfilesAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<MemberProfessionalProfileSetDto>> ReplaceMemberProfilesAsync(Guid organizationId, Guid userId, ReplaceMemberProfessionalProfilesDto dto, CancellationToken ct = default);
}
