using System.Net;
using System.Net.Sockets;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Infrastructure.Services;

public interface IWebhookDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
}

public sealed class SystemWebhookDnsResolver : IWebhookDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        => Dns.GetHostAddressesAsync(host, ct);
}

public sealed class WebhookEndpointPolicy : IWebhookEndpointPolicy
{
    private readonly IWebhookDnsResolver _dnsResolver;

    public WebhookEndpointPolicy(IWebhookDnsResolver dnsResolver)
    {
        _dnsResolver = dnsResolver;
    }

    public async Task<WebhookEndpointValidation> ValidateAsync(string payloadUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payloadUrl) ||
            !Uri.TryCreate(payloadUrl.Trim(), UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            return WebhookEndpointValidation.Blocked("Payload URL must be a valid http or https URL.");
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
        {
            return WebhookEndpointValidation.Blocked("Payload URL must not contain embedded credentials.");
        }

        var host = endpoint.IdnHost.TrimEnd('.');
        if (IsLocalHostname(host))
        {
            return WebhookEndpointValidation.Blocked("Payload URL must not target a local or private host.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            try
            {
                addresses = await _dnsResolver.ResolveAsync(host, ct);
            }
            catch (SocketException)
            {
                return WebhookEndpointValidation.Blocked("Payload URL host could not be resolved.");
            }
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            return WebhookEndpointValidation.Blocked("Payload URL must resolve only to public IP addresses.");
        }

        return WebhookEndpointValidation.Allowed(endpoint);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var first = bytes[0];
            var second = bytes[1];
            if (first is 0 or 10 or 127 || first >= 224) return false;
            if (first == 100 && second is >= 64 and <= 127) return false;
            if (first == 169 && second == 254) return false;
            if (first == 172 && second is >= 16 and <= 31) return false;
            if (first == 192 && second is 0 or 168) return false;
            if (first == 198 && second is 18 or 19) return false;
            if (first == 192 && second == 0 && bytes[2] == 2) return false;
            if (first == 198 && second == 51 && bytes[2] == 100) return false;
            if (first == 203 && second == 0 && bytes[2] == 113) return false;
            return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast ||
            address.IsIPv6SiteLocal ||
            (bytes[0] & 0xfe) == 0xfc ||
            (bytes[0] & 0xe0) != 0x20)
        {
            return false;
        }

        // Documentation prefix 2001:db8::/32 is not globally routable.
        return !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
    }

    private static bool IsLocalHostname(string host)
    {
        var normalized = host.ToLowerInvariant();
        return normalized == "localhost" ||
               normalized.EndsWith(".localhost", StringComparison.Ordinal) ||
               normalized.EndsWith(".local", StringComparison.Ordinal) ||
               normalized.EndsWith(".internal", StringComparison.Ordinal) ||
               normalized.EndsWith(".lan", StringComparison.Ordinal) ||
               normalized.EndsWith(".home", StringComparison.Ordinal);
    }
}
