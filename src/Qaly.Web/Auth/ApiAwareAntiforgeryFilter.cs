using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Qaly.Web.Auth;

/// <summary>
/// Cookie sessions require CSRF validation for every unsafe MVC request. API keys are
/// bearer credentials and are protected by scope enforcement instead, so they must not
/// require a browser antiforgery cookie/header pair.
/// </summary>
public sealed class ApiAwareAntiforgeryFilter : IAsyncAuthorizationFilter, IAntiforgeryPolicy, IOrderedFilter
{
    private readonly IAntiforgery _antiforgery;

    public ApiAwareAntiforgeryFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    // Run after built-in Validate/Ignore attributes and become the effective policy.
    public int Order => 2000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.IsEffectivePolicy<IAntiforgeryPolicy>(this) ||
            context.Filters.OfType<IgnoreAntiforgeryTokenAttribute>().Any() ||
            IsSafeMethod(context.HttpContext.Request.Method) ||
            ApiKeyScopeMiddleware.IsApiKeyPrincipal(context.HttpContext.User))
        {
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "CSRF token không hợp lệ hoặc đã hết hạn.",
                errorCode = "csrf_validation_failed"
            });
        }
    }

    private static bool IsSafeMethod(string method)
        => HttpMethods.IsGet(method) ||
           HttpMethods.IsHead(method) ||
           HttpMethods.IsOptions(method) ||
           HttpMethods.IsTrace(method);
}
