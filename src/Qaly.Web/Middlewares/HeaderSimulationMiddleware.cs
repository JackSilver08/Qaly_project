using System.Security.Claims;

namespace Qaly.Web.Middlewares;

/// <summary>
/// Middleware hỗ trợ "View-As Permission Simulation Mode" cho Admin/PM.
/// Nếu Header "X-Simulate-User-Id" có mặt và user hiện tại có quyền Admin,
/// gán HttpContext.Items["SimulatedUserId"] = targetUserId để CurrentUserService override UserId.
/// </summary>
public class HeaderSimulationMiddleware
{
    private readonly RequestDelegate _next;

    public HeaderSimulationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userRole = context.User.FindFirstValue(ClaimTypes.Role);
            var isSystemAdmin = string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (isSystemAdmin && context.Request.Headers.TryGetValue("X-Simulate-User-Id", out var simHeaderValue))
            {
                if (Guid.TryParse(simHeaderValue.ToString(), out var targetUserId))
                {
                    context.Items["SimulatedUserId"] = targetUserId;
                    context.Response.Headers["X-Simulation-Active"] = "true";
                }
            }
        }

        await _next(context);
    }
}

public static class HeaderSimulationMiddlewareExtensions
{
    public static IApplicationBuilder UseHeaderSimulation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<HeaderSimulationMiddleware>();
    }
}
