namespace Qaly.Application.DTOs.Organization;

public sealed record ProfessionalProfileDefinitionDto(
    Guid Id,
    Guid OrganizationId,
    string Key,
    string Name,
    string? Description,
    string Category,
    bool IsSystemSeed,
    bool IsActive,
    string RowVersion);

public sealed record CreateProfessionalProfileDefinitionDto(
    string Name,
    string? Key,
    string Category,
    string? Description);

public sealed record UpdateProfessionalProfileDefinitionDto(
    string Name,
    string Category,
    string? Description,
    bool IsActive,
    string RowVersion);

public sealed record MemberProfessionalProfileDto(
    Guid AssignmentId,
    Guid DefinitionId,
    string Key,
    string Name,
    string Category,
    string? Description,
    string Proficiency,
    string VerificationStatus,
    string Source,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid? VerifiedByUserId,
    string? VerifiedByName,
    DateTimeOffset? VerifiedAt,
    string? Note,
    string RowVersion,
    bool CanEdit);

public sealed record MemberProfessionalProfileSetDto(
    Guid OrganizationId,
    Guid UserId,
    string MemberName,
    string AccessRole,
    bool IsSelf,
    bool CanManage,
    string AuthorizationNotice,
    IReadOnlyList<MemberProfessionalProfileDto> Profiles);

public sealed record MemberProfessionalProfileSelectionDto(
    Guid DefinitionId,
    string Proficiency,
    string VerificationStatus,
    string? Source,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Note);

public sealed record ProfessionalProfileKnownRowDto(Guid AssignmentId, string RowVersion);

public sealed record ReplaceMemberProfessionalProfilesDto(
    bool Confirmed,
    IReadOnlyList<MemberProfessionalProfileSelectionDto> Profiles,
    IReadOnlyList<ProfessionalProfileKnownRowDto> KnownRows);
