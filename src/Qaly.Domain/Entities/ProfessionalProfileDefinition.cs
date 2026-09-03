namespace Qaly.Domain.Entities;

/// <summary>
/// Organization-owned professional taxonomy used for staffing and discovery. A professional
/// profile is evidence about a person's work domain; it never grants application access.
/// </summary>
public sealed class ProfessionalProfileDefinition : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Delivery";
    public bool IsSystemSeed { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public Organization Organization { get; set; } = null!;
    public ICollection<OrganizationMemberProfessionalProfile> MemberProfiles { get; set; } =
        new List<OrganizationMemberProfessionalProfile>();
}
