using Qaly.Application.Services;

namespace Qaly.Web.Auth;

/// <summary>
/// Enforces the least-privilege contract carried by an authenticated personal API key.
/// The regular controller/service authorization still runs afterwards, so a scope can
/// only narrow the owning user's permissions; it can never elevate them.
/// </summary>
public sealed class ApiKeyScopeMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsApiKeyPrincipal(context.User))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.ContainsKey(Middlewares.HeaderSimulationMiddleware.HeaderName))
        {
            await DenyAsync(context, "API Key không được dùng chế độ xem dưới danh nghĩa người khác.");
            return;
        }

        var requiredScope = ResolveRequiredScope(context.Request.Path, context.Request.Method);
        if (requiredScope == null)
        {
            await DenyAsync(
                context,
                "Endpoint này chưa được phát hành cho API Key. Hãy dùng phiên đăng nhập hoặc endpoint có scope được hỗ trợ.");
            return;
        }

        var granted = context.User.FindAll("scope")
            .Select(claim => claim.Value)
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.Ordinal));
        if (!granted)
        {
            await DenyAsync(context, $"API Key thiếu phạm vi bắt buộc: {requiredScope}.", requiredScope);
            return;
        }

        context.Items["ApiKeyRequiredScope"] = requiredScope;
        await _next(context);
    }

    internal static bool IsApiKeyPrincipal(System.Security.Claims.ClaimsPrincipal principal)
        => principal.Identities.Any(identity =>
            identity.IsAuthenticated &&
            string.Equals(identity.AuthenticationType, ApiKeyDefaults.AuthenticationScheme, StringComparison.Ordinal));

    internal static string? ResolveRequiredScope(PathString path, string method)
    {
        var value = path.Value?.TrimEnd('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || !value.StartsWith("/api/", StringComparison.Ordinal))
            return null;

        var access = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            ? "read"
            : "write";

        if (value.StartsWith("/api/projects/", StringComparison.Ordinal) &&
            value.Contains("/webhooks", StringComparison.Ordinal))
        {
            return $"webhooks:{access}";
        }

        if (value.StartsWith("/api/tasks", StringComparison.Ordinal) ||
            value.StartsWith("/api/sprints", StringComparison.Ordinal) ||
            IsProjectTaskRoute(value))
        {
            return $"tasks:{access}";
        }

        if (value.Equals("/api/projects", StringComparison.Ordinal) ||
            value.StartsWith("/api/projects/", StringComparison.Ordinal))
        {
            return $"projects:{access}";
        }

        return null;
    }

    private static bool IsProjectTaskRoute(string path)
        => path.StartsWith("/api/projects/", StringComparison.Ordinal) &&
           (path.Contains("/sprints", StringComparison.Ordinal) ||
            path.EndsWith("/gantt", StringComparison.Ordinal) ||
            path.EndsWith("/timeline", StringComparison.Ordinal) ||
            path.EndsWith("/workload", StringComparison.Ordinal) ||
            path.EndsWith("/task-attention", StringComparison.Ordinal));

    private static async Task DenyAsync(HttpContext context, string message, string? requiredScope = null)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = message,
            errorCode = "api_key_scope_denied",
            requiredScope
        }, context.RequestAborted);
    }
}

public static class ApiKeyScopeMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyScopes(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyScopeMiddleware>();
}
