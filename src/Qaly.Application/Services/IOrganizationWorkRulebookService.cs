using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IOrganizationWorkRulebookService
{
    Task<Result<IReadOnlyList<OrganizationWorkRuleSetDto>>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<OrganizationWorkRuleSetDto?>> GetEffectiveAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<OrganizationWorkRuleSetDto>> CreateDraftAsync(Guid organizationId, CreateOrganizationWorkRuleSetRequestDto request, CancellationToken ct = default);
    Task<Result<OrganizationWorkRuleSetDto>> ActivateAsync(Guid organizationId, Guid ruleSetId, ActivateOrganizationWorkRuleSetRequestDto request, CancellationToken ct = default);
}
