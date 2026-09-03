namespace Qaly.Web.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
            headers.TryAdd(
                "Permissions-Policy",
                "camera=(self), microphone=(self), display-capture=(self), geolocation=(), payment=(), usb=()");
            // These directives protect framing, plugin and base-URL surfaces without
            // prematurely constraining the configured LiveKit/provider connect targets.
            headers.TryAdd(
                "Content-Security-Policy",
                "base-uri 'self'; frame-ancestors 'none'; object-src 'none'");
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
