using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public async Task SendAsync_WhenSmtpDeliveryFails_DoesNotReportFalseSuccess()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var unavailablePort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"] = IPAddress.Loopback.ToString(),
                ["Email:SmtpPort"] = unavailablePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Email:From"] = "qaly@local.test"
            })
            .Build();
        var service = new SmtpEmailService(
            configuration,
            NullLogger<SmtpEmailService>.Instance);

        var act = () => service.SendAsync("recipient@qaly.test", "subject", "body");

        await act.Should().ThrowAsync<SmtpException>(
            "callers need the provider failure so durable delivery can retry instead of recording false success");
    }
}
