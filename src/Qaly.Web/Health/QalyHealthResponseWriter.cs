using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Qaly.Web.Health;

public static class QalyHealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/health+json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";

        // Health endpoints are intentionally anonymous for orchestrators. Keep the
        // payload operationally useful without returning exception messages,
        // connection strings or provider responses to an unauthenticated caller.
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            deploymentId = context.RequestServices
                .GetRequiredService<IConfiguration>()["ProductionReadiness:DeploymentId"] ?? "local",
            version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString().ToLowerInvariant(),
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
                })
        };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            payload,
            JsonOptions,
            context.RequestAborted);
    }
}
