using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Qaly.Infrastructure.Data;

namespace Qaly.Web.Middlewares;

/// <summary>
/// Read-only "view as" boundary for system administrators.
/// The effective principal is replaced before authorization so every controller and service sees
/// the target user's id and role. The original administrator id is retained only for diagnostics.
/// </summary>
public class HeaderSimulationMiddleware
{
    public const string HeaderName = "X-Simulate-User-Id";
    public const string SimulatedUserIdItem = "SimulatedUserId";
    public const string RealUserIdItem = "SimulationRealUserId";

    private readonly RequestDelegate _next;

    public HeaderSimulationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, QalyDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.Request.Headers.TryGetValue(HeaderName, out var simHeaderValue))
        {
            await _next(context);
            return;
        }

        var realRole = context.User.FindFirstValue(ClaimTypes.Role);
        var isSystemAdmin = string.Equals(realRole, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isSystemAdmin)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Only a system administrator may use view-as simulation."
            });
            return;
        }

        if (!Guid.TryParse(simHeaderValue.ToString(), out var targetUserId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "The simulated user id is invalid." });
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "View-as simulation is read-only. Exit simulation before changing data."
            });
            return;
        }

        var target = await db.Users.AsNoTracking()
            .Where(user => user.Id == targetUserId && user.IsActive)
            .Select(user => new { user.Id, user.FullName, user.Email, user.Role })
            .SingleOrDefaultAsync(context.RequestAborted);
        if (target == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "The simulated user is missing or inactive." });
            return;
        }

        var realUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(realUserId, out var parsedRealUserId))
        {
            context.Items[RealUserIdItem] = parsedRealUserId;
        }

        context.Items[SimulatedUserIdItem] = target.Id;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, target.Id.ToString()),
            new Claim(ClaimTypes.Name, target.FullName),
            new Claim(ClaimTypes.Email, target.Email),
            new Claim(ClaimTypes.Role, target.Role)
        ], "QalyViewAs"));
        context.Response.Headers["X-Simulation-Active"] = "true";
        context.Response.Headers["X-Simulation-Read-Only"] = "true";

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
