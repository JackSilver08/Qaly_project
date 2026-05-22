namespace Qaly.Domain.Entities;

public class MeetingActionItemMapping : BaseEntity
{
    public Guid MeetingImportId { get; set; }
    public int ActionItemIndex { get; set; }
    public Guid? TaskId { get; set; }
    public string Status { get; set; } = "Draft";
    public string? SourceTitle { get; set; }
    public string? SourcePriority { get; set; }
    public DateTimeOffset? SourceDueDate { get; set; }
    public string? SourceQuote { get; set; }
    public Guid CreatedById { get; set; }

    public MeetingImport MeetingImport { get; set; } = null!;
    public TaskItem? Task { get; set; }
    public User? CreatedBy { get; set; }
}
