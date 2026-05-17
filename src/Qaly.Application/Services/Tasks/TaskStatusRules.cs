using Qaly.Domain.Enums;

namespace Qaly.Application.Services.Tasks;

public static class TaskStatusRules
{
    public static bool CanTransition(string oldStatus, string newStatus)
        => IsValidStatus(newStatus);

    public static bool IsValidStatus(string status)
        => Enum.TryParse<TaskItemStatus>(status, ignoreCase: true, out _);

    public static bool IsValidPriority(string priority)
        => Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out _);

    public static string NormalizeStatus(string status)
        => Enum.Parse<TaskItemStatus>(status, ignoreCase: true).ToString();

    public static string NormalizePriority(string priority)
        => Enum.Parse<TaskPriority>(priority, ignoreCase: true).ToString();
}
