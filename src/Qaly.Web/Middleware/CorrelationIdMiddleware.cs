using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Qaly.Web.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    internal const string CorrelationIdHeader = "X-Correlation-Id";
    internal const int MaxCorrelationIdLength = 64;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = ResolveCorrelationId(context.Request.Headers);

        context.Items["CorrelationId"] = correlationId;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            // The server owns the canonical response value. Overwrite rather than append so
            // downstream code cannot accidentally emit duplicate or conflicting identifiers.
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    internal static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(CorrelationIdHeader, out var values) || values.Count != 1)
        {
            return CreateCorrelationId();
        }

        var value = values[0]?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > MaxCorrelationIdLength ||
            !value.All(IsSafeCharacter))
        {
            return CreateCorrelationId();
        }

        return value;
    }

    private static bool IsSafeCharacter(char value)
        => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.';

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");
}
