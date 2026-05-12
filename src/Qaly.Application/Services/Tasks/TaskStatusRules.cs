using Qaly.Domain.Enums;

namespace Qaly.Application.Services.Tasks;

public static class TaskStatusRules
{
    private static readonly Dictionary<string, string[]> StatusTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Todo"] = ["InProgress", "Cancelled"],
        ["InProgress"] = ["Todo", "InReview", "Done", "Cancelled"],
        ["InReview"] = ["InProgress", "Done", "Cancelled"],
        ["Done"] = ["InReview"],
        ["Cancelled"] = ["Todo"]
    };

    public static bool CanTransition(string oldStatus, string newStatus)
        => string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase) ||
           (StatusTransitions.TryGetValue(oldStatus, out var allowed) &&
            allowed.Contains(newStatus, StringComparer.OrdinalIgnoreCase));

    public static bool IsValidStatus(string status)
        => Enum.TryParse<TaskItemStatus>(status, ignoreCase: true, out _);

    public static bool IsValidPriority(string priority)
        => Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out _);

    public static string NormalizeStatus(string status)
        => Enum.Parse<TaskItemStatus>(status, ignoreCase: true).ToString();

    public static string NormalizePriority(string priority)
        => Enum.Parse<TaskPriority>(priority, ignoreCase: true).ToString();
}
