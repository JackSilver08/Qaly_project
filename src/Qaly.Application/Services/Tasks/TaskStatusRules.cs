using Qaly.Domain.Enums;

namespace Qaly.Application.Services.Tasks;

public static class TaskStatusRules
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InProgress", "OnHold", "Cancelled" },
        ["InProgress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InReview", "OnHold", "Cancelled", "Done" },
        ["InReview"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InProgress", "Done", "OnHold", "Cancelled" },
        ["OnHold"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Todo", "InProgress", "Cancelled" },
        ["Done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InReview" },
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
}
