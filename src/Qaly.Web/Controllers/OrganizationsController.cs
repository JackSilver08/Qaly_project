using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Project;
using Qaly.Application.DTOs.Task;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Organization;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrganizationsController : BaseApiController
{
    private readonly IOrganizationService _organizationService;
    private readonly ITaskSkillService _taskSkillService;
    private readonly IMemberSkillEvidenceService _memberSkillEvidenceService;
    private readonly IPortfolioScheduleService _portfolioScheduleService;
    private readonly IProfessionalProfileService _professionalProfileService;
    private readonly IAuditLogService _auditLogService;

    public OrganizationsController(
        IOrganizationService organizationService,
        ITaskSkillService taskSkillService,
        IMemberSkillEvidenceService memberSkillEvidenceService,
        IPortfolioScheduleService portfolioScheduleService,
        IProfessionalProfileService professionalProfileService,
        IAuditLogService auditLogService)
    {
        _organizationService = organizationService;
        _taskSkillService = taskSkillService;
        _memberSkillEvidenceService = memberSkillEvidenceService;
        _portfolioScheduleService = portfolioScheduleService;
        _professionalProfileService = professionalProfileService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _organizationService.GetAllAsync(page, pageSize, search, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _organizationService.GetByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateOrganizationDto dto, CancellationToken ct = default)
    {
        var result = await _organizationService.CreateAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, UpdateOrganizationDto dto, CancellationToken ct = default)
    {
        var result = await _organizationService.UpdateAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
    {
        var result = await _organizationService.DeactivateAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct = default)
    {
        var result = await _organizationService.GetMembersAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> GetActivity(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _auditLogService.GetByOrganizationAsync(id, page, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/members")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(Guid id, AddOrganizationMemberRequest request, CancellationToken ct = default)
    {
        var result = await _organizationService.AddMemberAsync(id, request.UserId, request.Role, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/users")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(Guid id, AddOrganizationUserRequest request, CancellationToken ct = default)
    {
        var result = await _organizationService.AddMemberByEmailAsync(id, request.Email, request.Role, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id:guid}/users/{userId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(Guid id, Guid userId, UpdateOrganizationUserRoleRequest request, CancellationToken ct = default)
    {
        var result = await _organizationService.UpdateMemberRoleAsync(id, userId, request.Role, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/users")]
    public Task<IActionResult> GetUsers(Guid id, CancellationToken ct = default)
        => GetMembers(id, ct);

    [HttpGet("{id:guid}/moderator-capabilities")]
    public async Task<IActionResult> GetModeratorCapabilities(Guid id, CancellationToken ct = default)
    {
        var result = await _organizationService.GetCurrentModeratorCapabilitiesAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct = default)
    {
        var result = await _organizationService.RemoveMemberAsync(id, userId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}/users/{userId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveUser(Guid id, Guid userId, CancellationToken ct = default)
        => RemoveMember(id, userId, ct);

    [HttpGet("{id:guid}/skills")]
    public async Task<IActionResult> GetSkills(
        Guid id,
        [FromQuery] string? search = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _taskSkillService.GetOrganizationSkillsAsync(id, search, includeInactive, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/skills")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSkill(
        Guid id,
        CreateOrganizationSkillDto dto,
        CancellationToken ct = default)
    {
        var result = await _taskSkillService.CreateOrganizationSkillAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/skills/{skillId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSkill(
        Guid id,
        Guid skillId,
        UpdateOrganizationSkillDto dto,
        CancellationToken ct = default)
    {
        var result = await _taskSkillService.UpdateOrganizationSkillAsync(id, skillId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/members/{userId:guid}/capacity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMemberCapacity(
        Guid id,
        Guid userId,
        UpdateMemberCapacityProfileDto dto,
        CancellationToken ct = default)
    {
        var result = await _portfolioScheduleService.UpdateCapacityProfileAsync(id, userId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/members/{memberId:guid}/skill-evidence")]
    public async Task<IActionResult> GetMemberSkillEvidence(
        Guid id,
        Guid memberId,
        CancellationToken ct = default)
    {
        var result = await _memberSkillEvidenceService.GetMemberSkillProfileAsync(id, memberId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/professional-profiles")]
    public async Task<IActionResult> GetProfessionalProfiles(
        Guid id,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _professionalProfileService.GetDefinitionsAsync(id, includeInactive, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/professional-profiles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProfessionalProfile(
        Guid id,
        CreateProfessionalProfileDefinitionDto dto,
        CancellationToken ct = default)
    {
        var result = await _professionalProfileService.CreateDefinitionAsync(id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/professional-profiles/{profileId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfessionalProfile(
        Guid id,
        Guid profileId,
        UpdateProfessionalProfileDefinitionDto dto,
        CancellationToken ct = default)
    {
        var result = await _professionalProfileService.UpdateDefinitionAsync(id, profileId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}/members/{userId:guid}/professional-profiles")]
    public async Task<IActionResult> GetMemberProfessionalProfiles(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var result = await _professionalProfileService.GetMemberProfilesAsync(id, userId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}/members/{userId:guid}/professional-profiles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceMemberProfessionalProfiles(
        Guid id,
        Guid userId,
        ReplaceMemberProfessionalProfilesDto dto,
        CancellationToken ct = default)
    {
        var result = await _professionalProfileService.ReplaceMemberProfilesAsync(id, userId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record AddOrganizationMemberRequest(Guid UserId, string Role);
public sealed record AddOrganizationUserRequest(string Email, string Role);
public sealed record UpdateOrganizationUserRoleRequest(string Role);
