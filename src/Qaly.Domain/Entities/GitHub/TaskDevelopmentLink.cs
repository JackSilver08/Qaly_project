namespace Qaly.Domain.Entities.GitHub;

/// <summary>
/// Liên kết một task Qaly với một thực thể phát triển (branch/commit/PR/issue/
/// release). Có thể tạo tự động từ task key hoặc thủ công.
/// </summary>
public class TaskDevelopmentLink : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>Task Qaly.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Branch, Commit, PullRequest, Issue, Release.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>ID entity đã chuẩn hóa (SHA, số PR, tên branch...).</summary>
    public string ExternalEntityId { get; set; } = string.Empty;

    /// <summary>Manual, TaskKey, BranchName, CommitMessage, PullRequest.</summary>
    public string LinkSource { get; set; } = "TaskKey";

    /// <summary>Độ tin cậy của liên kết tự động (0..1).</summary>
    public double Confidence { get; set; } = 1;

    /// <summary>Người xác nhận nếu liên kết thủ công.</summary>
    public Guid? LinkedByUserId { get; set; }

    // Navigation
    public TaskItem Task { get; set; } = null!;
}
