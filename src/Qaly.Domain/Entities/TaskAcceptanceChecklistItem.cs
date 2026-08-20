namespace Qaly.Domain.Entities;

/// <summary>
/// Canonical, persisted acceptance item owned by a Task. AI may propose a
/// replacement set, but only an explicitly confirmed native-action draft may
/// mutate these rows.
/// </summary>
public sealed class TaskAcceptanceChecklistItem : BaseEntity
{
    public Guid TaskId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Kind { get; set; } = Acceptance;
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? SourceDraftId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public TaskItem Task { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;

    public const string Acceptance = "acceptance";
    public const string DefinitionOfDone = "definition_of_done";
}
