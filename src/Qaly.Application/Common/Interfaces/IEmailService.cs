namespace Qaly.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendTaskAssignmentNotificationAsync(string to, string taskTitle, string projectName);
    Task SendDueDateReminderAsync(string to, string taskTitle, DateTimeOffset dueDate);
}
