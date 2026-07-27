namespace Qaly.Domain.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? AllowedEmailDomains { get; set; }
    public string? WorkspaceIcon { get; set; }
    public string? WorkspaceCover { get; set; }

    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<OrganizationSkill> Skills { get; set; } = new List<OrganizationSkill>();
    public ICollection<WorkGroup> WorkGroups { get; set; } = new List<WorkGroup>();
}
