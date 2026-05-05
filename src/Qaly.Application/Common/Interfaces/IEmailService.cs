namespace Qaly.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken = default);
    Task SendTaskAssignmentNotificationAsync(string recipientEmail, string taskTitle, string projectName);
    Task SendDueDateReminderAsync(string recipientEmail, string taskTitle, DateTimeOffset dueDate);
}
