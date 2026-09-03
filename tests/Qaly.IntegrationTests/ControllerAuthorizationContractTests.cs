using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Qaly.IntegrationTests;

public sealed class ControllerAuthorizationContractTests
{
    private static readonly HashSet<string> IntentionalAnonymousActions =
    [
        "AuthController.Login",
        "AuthController.Register",
        "GitHubWebhookController.Receive",
        "SecurityController.GetCsrfToken"
    ];

    [Fact]
    public void EveryHttpAction_HasAnExplicitAuthenticationBoundary()
    {
        var actions = GetHttpActions();

        var uncovered = actions
            .Where(action => !HasAttribute<AuthorizeAttribute>(action.Controller, action.Action)
                && !HasAttribute<AllowAnonymousAttribute>(action.Controller, action.Action))
            .Select(FormatAction)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        uncovered.Should().BeEmpty(
            "every HTTP action must explicitly require authorization or declare intentional anonymous access");
    }

    [Fact]
    public void AnonymousHttpActions_MatchTheReviewedAllowlist()
    {
        var anonymousActions = GetHttpActions()
            .Where(action => HasAttribute<AllowAnonymousAttribute>(action.Controller, action.Action))
            .Select(FormatAction)
            .ToHashSet(StringComparer.Ordinal);

        anonymousActions.Should().BeEquivalentTo(IntentionalAnonymousActions,
            "new anonymous endpoints require an explicit security review and allowlist update");
    }

    private static HttpAction[] GetHttpActions()
    {
        return typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(action => action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(action => new HttpAction(controller, action)))
            .ToArray();
    }

    private static bool HasAttribute<TAttribute>(Type controller, MethodInfo action)
        where TAttribute : Attribute
    {
        return controller.IsDefined(typeof(TAttribute), inherit: true)
            || action.IsDefined(typeof(TAttribute), inherit: true);
    }

    private static string FormatAction(HttpAction action)
        => $"{action.Controller.Name}.{action.Action.Name}";

    private sealed record HttpAction(Type Controller, MethodInfo Action);
}
