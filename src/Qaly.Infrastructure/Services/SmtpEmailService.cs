using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:SmtpHost"] ?? "localhost";
        var port = int.TryParse(_configuration["Email:SmtpPort"], out var configuredPort) ? configuredPort : 1025;
        var from = _configuration["Email:From"] ?? "qaly@local.dev";
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];
        var enableSsl = bool.TryParse(_configuration["Email:EnableSsl"], out var configuredSsl) && configuredSsl;

        using var message = new MailMessage(from, recipientEmail, subject, body)
        {
            IsBodyHtml = false
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex)
        {
            _logger.LogWarning(ex, "Could not send email to {RecipientEmail}", recipientEmail);
        }
    }

    public Task SendTaskAssignmentNotificationAsync(string recipientEmail, string taskTitle, string projectName)
        => SendAsync(recipientEmail, $"New task assigned: {taskTitle}", $"You were assigned to '{taskTitle}' in project '{projectName}'.");

    public Task SendDueDateReminderAsync(string recipientEmail, string taskTitle, DateTimeOffset dueDate)
        => SendAsync(recipientEmail, $"Task due soon: {taskTitle}", $"'{taskTitle}' is due at {dueDate:yyyy-MM-dd HH:mm}.");
}
