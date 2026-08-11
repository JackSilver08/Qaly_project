namespace Qaly.Application.DTOs.Project;

public record ProjectRoleDefinitionDto(
    Guid Id,
    Guid OrganizationId,
    string Key,
    string DisplayName,
    string? Description,
    string BaseRole,
    string BaseRoleLabel,
    IReadOnlyList<string> SkillTags,
    bool IsActive,
    int MemberCount,
    DateTimeOffset CreatedAt);

public record CreateProjectRoleDefinitionDto(
    string DisplayName,
    string BaseRole,
    string? Description = null,
    string? SkillTags = null);

public record UpdateProjectRoleDefinitionDto(
    string DisplayName,
    string BaseRole,
    string? Description = null,
    string? SkillTags = null,
    bool IsActive = true);

/// <summary>One entry of the role picker: built-in roles and the organization's own roles together.</summary>
public record AssignableProjectRoleDto(
    string Key,
    string DisplayName,
    string BaseRole,
    bool IsCustom,
    string PermissionSummary,
    string AiTier,
    string AiTierDescription,
    IReadOnlyList<string> SkillTags);
