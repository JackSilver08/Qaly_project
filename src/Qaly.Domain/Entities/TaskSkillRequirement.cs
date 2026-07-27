namespace Qaly.Domain.Entities;

public sealed class TaskSkillRequirement : BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid OrganizationSkillId { get; set; }
    public string RequiredLevel { get; set; } = "Familiar";
    public string Provenance { get; set; } = "MANUAL";
    public Guid ConfirmedByUserId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    public TaskItem TaskItem { get; set; } = null!;
    public OrganizationSkill OrganizationSkill { get; set; } = null!;
    public User ConfirmedByUser { get; set; } = null!;
}
