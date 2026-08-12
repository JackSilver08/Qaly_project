namespace Qaly.Domain.Entities;

/// <summary>
/// A role an organization owner defined for their own way of working — "Dev Backend", "PO",
/// "AI Engineer" — on top of the built-in project roles.
///
/// A custom role is a label plus an inherited permission level: <see cref="BaseRole"/> names the
/// built-in role it behaves as. Authorization never reads <see cref="Key"/> directly, so adding a
/// custom role can never widen permissions beyond an existing built-in role.
/// </summary>
public class ProjectRoleDefinition : BaseEntity
{
    /// <summary>Owning organization. Custom roles are reusable across that organization's projects.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Stable slug stored on <see cref="ProjectMember.Role"/>, e.g. "dev-backend".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Name shown in the UI, e.g. "Lập trình viên Backend".</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Built-in project role whose permissions this role inherits.</summary>
    public string BaseRole { get; set; } = "Member";

    /// <summary>
    /// Comma-separated skill tags used when suggesting who should take a task,
    /// e.g. "backend,dotnet,sql".
    /// </summary>
    public string? SkillTags { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }

    public Organization Organization { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
