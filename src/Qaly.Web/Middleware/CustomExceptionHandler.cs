using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Qaly.Domain.Interfaces;

namespace Qaly.Web.Middleware;

public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CustomExceptionHandler> _logger;
    private readonly ICurrentUserService _currentUserService;

    public CustomExceptionHandler(ILogger<CustomExceptionHandler> logger, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"] as string ?? "unknown";
        var userId = _currentUserService.UserId?.ToString() ?? "anonymous";
        var requestPath = httpContext.Request.Path;

        _logger.LogError(exception,
            "An unhandled exception occurred. UserId: {UserId}, RequestPath: {RequestPath}, CorrelationId: {CorrelationId}",
            userId, requestPath, correlationId);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            Type = "https://tools.ietf.org/html/rfc7807#section-6.6.1",
            Detail = "An unexpected error occurred on the server.",
            Instance = requestPath
        };

        problemDetails.Extensions.Add("correlationId", correlationId);

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
