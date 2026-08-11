namespace Qaly.Application.Common.Interfaces;

public sealed record WebhookEndpointValidation(
    bool IsAllowed,
    Uri? Endpoint,
    string? Error)
{
    public static WebhookEndpointValidation Allowed(Uri endpoint) => new(true, endpoint, null);
    public static WebhookEndpointValidation Blocked(string error) => new(false, null, error);
}

public interface IWebhookEndpointPolicy
{
    Task<WebhookEndpointValidation> ValidateAsync(string payloadUrl, CancellationToken ct = default);
}
