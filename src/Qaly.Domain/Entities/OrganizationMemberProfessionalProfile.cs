namespace Qaly.Domain.Entities;

/// <summary>
/// Professional profile assigned to an Organization member. This entity is deliberately separate
/// from OrganizationMember.Role and must not be consulted for authorization.
/// </summary>
public sealed class OrganizationMemberProfessionalProfile : BaseEntity
{
    public const string Declared = "Declared";
    public const string Verified = "Verified";
    public const string Rejected = "Rejected";

    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfessionalProfileDefinitionId { get; set; }
    public string Proficiency { get; set; } = "Practitioner";
    public string VerificationStatus { get; set; } = Declared;
    public string Source { get; set; } = "MemberDeclared";
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? Note { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public ProfessionalProfileDefinition ProfessionalProfileDefinition { get; set; } = null!;
    public User? VerifiedByUser { get; set; }
}
