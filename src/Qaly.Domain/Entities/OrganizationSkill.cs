namespace Qaly.Domain.Entities;

public sealed class OrganizationSkill : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public Organization Organization { get; set; } = null!;
    public ICollection<TaskSkillRequirement> TaskRequirements { get; set; } = new List<TaskSkillRequirement>();
}
