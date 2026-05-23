namespace Qaly.Application.Services.Notifications;

public sealed record NotificationTemplate(
    string Message,
    string Type,
    string Tone,
    string IdempotencyKey);

public static class NotificationTemplates
{
    private static readonly HashSet<string> ImportantStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "InReview",
        "Done",
        "Cancelled",
        "OnHold"
    };

    public static NotificationTemplate TaskAssigned(Guid taskId, string taskTitle, Guid assigneeId)
        => new(
            $"Bạn đã được giao nhiệm vụ \"{taskTitle}\".",
            "TaskAssigned",
            "info",
            $"task:{taskId}:assigned:{assigneeId}");

    public static NotificationTemplate Mentioned(Guid taskId, Guid commentId, string taskTitle, Guid mentionedUserId)
        => new(
            $"Bạn được nhắc đến trong nhiệm vụ \"{taskTitle}\".",
            "Mentioned",
            "info",
            $"task:{taskId}:comment:{commentId}:mention:{mentionedUserId}");

    public static NotificationTemplate CommentAdded(Guid taskId, Guid commentId, string taskTitle, Guid recipientId)
        => new(
            $"Có bình luận mới trong nhiệm vụ \"{taskTitle}\".",
            "CommentAdded",
            "info",
            $"task:{taskId}:comment:{commentId}:recipient:{recipientId}");

    public static NotificationTemplate ImportantStatusChanged(Guid taskId, string taskTitle, string oldStatus, string newStatus)
        => new(
            $"Nhiệm vụ \"{taskTitle}\" đã chuyển từ {oldStatus} sang {newStatus}.",
            "TaskStatusChanged",
            string.Equals(newStatus, "Done", StringComparison.OrdinalIgnoreCase) ? "success" : "warning",
            $"task:{taskId}:status:{oldStatus}->{newStatus}");

    public static NotificationTemplate EvidenceReviewed(Guid taskId, Guid attachmentId, string taskTitle, bool approved, Guid recipientId)
        => new(
            $"Minh chứng của nhiệm vụ \"{taskTitle}\" đã được {(approved ? "duyệt" : "từ chối")}.",
            "ReviewCompleted",
            approved ? "success" : "warning",
            $"task:{taskId}:attachment:{attachmentId}:review:{(approved ? "approved" : "rejected")}:{recipientId}");

    public static bool IsImportantStatusChange(string oldStatus, string newStatus)
        => !string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase) &&
           (ImportantStatuses.Contains(newStatus) || string.Equals(oldStatus, "Done", StringComparison.OrdinalIgnoreCase));
}
