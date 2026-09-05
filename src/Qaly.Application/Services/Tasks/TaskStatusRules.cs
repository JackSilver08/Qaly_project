using Qaly.Domain.Enums;

namespace Qaly.Application.Services.Tasks;

public static class TaskStatusRules
{
    public static readonly string[] OpenStatuses = ["Todo", "InProgress", "InReview", "OnHold"];

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InProgress", "OnHold", "Cancelled" },
        ["InProgress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InReview", "OnHold", "Cancelled", "Done" },
        ["InReview"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InProgress", "Done", "OnHold", "Cancelled" },
        ["OnHold"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Todo", "InProgress", "Cancelled" },
        // Reopening completed work is not a normal status/drag operation.
        ["Done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ["Cancelled"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Todo" }
    };

    public static bool CanTransition(string oldStatus, string newStatus)
    {
        if (!IsValidStatus(oldStatus) || !IsValidStatus(newStatus))
        {
            return false;
        }

        if (string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedOldStatus = NormalizeStatus(oldStatus);
        var normalizedNewStatus = NormalizeStatus(newStatus);

        return AllowedTransitions.TryGetValue(normalizedOldStatus, out var allowedTargets) &&
               allowedTargets.Contains(normalizedNewStatus);
    }

    public static bool IsValidStatus(string status)
        => Enum.TryParse<TaskItemStatus>(status, ignoreCase: true, out _);

    public static bool IsValidPriority(string priority)
        => Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out _);

    public static string NormalizeStatus(string status)
        => Enum.Parse<TaskItemStatus>(status, ignoreCase: true).ToString();

    public static string NormalizePriority(string priority)
        => Enum.Parse<TaskPriority>(priority, ignoreCase: true).ToString();

    public static bool IsDone(string? status)
        => string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase);

    public static bool IsCancelled(string? status)
        => string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string? status)
        => IsDone(status) ||
           IsCancelled(status) ||
           string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);

    public static bool IsOpen(string? status)
        => status != null && OpenStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool IsOverdue(string? status, DateTimeOffset? dueDate, DateTimeOffset now)
        => dueDate.HasValue && dueDate.Value < now && IsOpen(status);

    public static bool IsDueSoon(string? status, DateTimeOffset? dueDate, DateTimeOffset now, DateTimeOffset boundary)
        => dueDate.HasValue &&
           dueDate.Value >= now &&
           dueDate.Value < boundary &&
           IsOpen(status);
}
