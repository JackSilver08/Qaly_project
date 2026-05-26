namespace Qaly.Domain.Entities;

public class Project : BaseEntity, ISoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Foreign keys
    public Guid OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? SourceGroupId { get; set; }

    // Navigation properties
    public User Owner { get; set; } = null!;
    public Organization? Organization { get; set; }
    public WorkGroup? SourceGroup { get; set; }
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
    public ICollection<ProjectLabel> Labels { get; set; } = new List<ProjectLabel>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<AiJob> AiJobs { get; set; } = new List<AiJob>();
    public ICollection<AiGeneratedDraft> AiDrafts { get; set; } = new List<AiGeneratedDraft>();
}
