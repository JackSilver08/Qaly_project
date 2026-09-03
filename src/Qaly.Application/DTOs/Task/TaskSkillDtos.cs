namespace Qaly.Application.DTOs.Task;

public sealed record OrganizationSkillDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string NormalizedName,
    string? Description,
    bool IsActive,
    string RowVersion,
    string Category = "Chuyên môn",
    IReadOnlyList<string>? Aliases = null,
    string DefaultRequiredLevel = "Intermediate",
    bool IsSystemSeed = false);

public sealed record CreateOrganizationSkillDto(
    string Name,
    string? Description = null,
    string Category = "Chuyên môn",
    IReadOnlyList<string>? Aliases = null,
    string DefaultRequiredLevel = "Intermediate");

public sealed record UpdateOrganizationSkillDto(
    string Name,
    string? Description,
    bool IsActive,
    string RowVersion,
    string Category = "Chuyên môn",
    IReadOnlyList<string>? Aliases = null,
    string DefaultRequiredLevel = "Intermediate");

public sealed record TaskSkillSelectionDto(
    Guid SkillId,
    string RequiredLevel);

public sealed record ReplaceTaskSkillsDto(
    string TaskRowVersion,
    IReadOnlyList<TaskSkillSelectionDto> Skills);

public sealed record TaskSkillRequirementDto(
    Guid Id,
    Guid SkillId,
    string Name,
    string? Description,
    string RequiredLevel,
    string Provenance,
    Guid ConfirmedByUserId,
    DateTimeOffset ConfirmedAt,
    string RowVersion);

public sealed record TaskSkillsDto(
    Guid TaskId,
    Guid ProjectId,
    Guid? OrganizationId,
    string Availability,
    bool CanManage,
    bool CanManageCatalog,
    string TaskRowVersion,
    IReadOnlyList<TaskSkillRequirementDto> Requirements);
