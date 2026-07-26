using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Qaly.WebFeatureTests;

[TestFixture]
public sealed partial class WebSurfaceContractTests : IDisposable
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private FeatureTestFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new FeatureTestFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestCase("/dashboard")]
    [TestCase("/profile")]
    [TestCase("/projects")]
    [TestCase("/projects/archived")]
    [TestCase("/projects/11111111-1111-1111-1111-111111111111")]
    [TestCase("/projects/11111111-1111-1111-1111-111111111111/tasks/22222222-2222-2222-2222-222222222222")]
    [TestCase("/projects/11111111-1111-1111-1111-111111111111/wiki/22222222-2222-2222-2222-222222222222")]
    [TestCase("/tasks")]
    [TestCase("/teams")]
    [TestCase("/analytics")]
    [TestCase("/admin/users")]
    [TestCase("/organizations/users")]
    [TestCase("/admin/moderators")]
    [TestCase("/groups")]
    [TestCase("/groups/11111111-1111-1111-1111-111111111111")]
    [TestCase("/groups/11111111-1111-1111-1111-111111111111/meeting")]
    [TestCase("/groups/11111111-1111-1111-1111-111111111111/polls")]
    [TestCase("/groups/11111111-1111-1111-1111-111111111111/polls/22222222-2222-2222-2222-222222222222")]
    [TestCase("/settings")]
    [TestCase("/route-that-does-not-exist")]
    public async Task Spa_route_returns_the_application_shell(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (route.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("X-Test-Role", "Admin");
        }

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"{route} must be handled by the SPA fallback; response body: {body}");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        body.Should().Contain("<div id=\"qaly-dashboard-app\">");
        body.Should().Contain("/dist/assets/main.js");
    }

    [Test]
    public async Task Application_shell_references_assets_that_are_served()
    {
        var shell = await _client.GetStringAsync("/dashboard");
        var assetPaths = AssetReferenceRegex()
            .Matches(shell)
            .Select(match => match.Groups["path"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        assetPaths.Should().NotBeEmpty("the application shell must load its compiled frontend");

        foreach (var assetPath in assetPaths)
        {
            var response = await _client.GetAsync(assetPath);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{assetPath} is referenced by the application shell");
            (await response.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
        }
    }

    [TestCase("/api/route-that-does-not-exist")]
    [TestCase("/hubs/route-that-does-not-exist")]
    [TestCase("/Account/route-that-does-not-exist")]
    public async Task Backend_and_auth_routes_are_not_swallowed_by_the_spa_fallback(string route)
    {
        var response = await _client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/html");
    }

    [Test]
    public void Every_lazy_loaded_page_in_the_router_exists_in_source()
    {
        var routerPath = Path.Combine(RepositoryRoot, "src", "Qaly.Web", "ClientApp", "router", "index.ts");
        var routerSource = File.ReadAllText(routerPath);
        var referencedPages = PageImportRegex()
            .Matches(routerSource)
            .Select(match => match.Groups["page"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        referencedPages.Should().NotBeEmpty();

        foreach (var page in referencedPages)
        {
            var pagePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(routerPath)!, page));
            File.Exists(pagePath).Should().BeTrue($"{page} is imported by the router");
        }
    }

    [Test]
    public void Every_controller_action_has_an_explicit_authentication_boundary()
    {
        var controllerAssembly = typeof(Program).Assembly;
        var failures = new List<string>();

        foreach (var controller in ControllerTypes(controllerAssembly))
        {
            var controllerRequiresAuth = controller.IsDefined(typeof(AuthorizeAttribute), inherit: true);
            var controllerAllowsAnonymous = controller.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

            foreach (var action in ControllerActions(controller))
            {
                var actionRequiresAuth = action.IsDefined(typeof(AuthorizeAttribute), inherit: true);
                var actionAllowsAnonymous = action.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
                if (!controllerRequiresAuth && !controllerAllowsAnonymous && !actionRequiresAuth && !actionAllowsAnonymous)
                {
                    failures.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        failures.Should().BeEmpty(
            "every HTTP action must explicitly require authentication or explicitly opt into anonymous access");
    }

    [Test]
    public void Runtime_endpoints_do_not_have_duplicate_http_method_and_route_contracts()
    {
        var endpoints = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                    ?? ["ANY"];
                return methods.Select(method => new
                {
                    Method = method,
                    Route = endpoint.RoutePattern.RawText ?? string.Empty,
                    endpoint.DisplayName
                });
            })
            .Where(endpoint => endpoint.Route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        endpoints.Should().NotBeEmpty();

        var duplicates = endpoints
            .GroupBy(endpoint => $"{endpoint.Method} {endpoint.Route}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(" | ", group.Select(item => item.DisplayName))}")
            .ToArray();

        duplicates.Should().BeEmpty("ambiguous API routes can fail at runtime depending on request data");
    }

    private static IEnumerable<Type> ControllerTypes(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

    private static IEnumerable<MethodInfo> ControllerActions(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute is HttpMethodAttribute));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Qaly_project.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the Qaly repository root.");
    }

    [GeneratedRegex("(?:src|href)=\"(?<path>/dist/assets/[^\"]+)\"")]
    private static partial Regex AssetReferenceRegex();

    [GeneratedRegex("import\\(\"(?<page>\\.\\./pages/[^\"]+\\.vue)\"\\)")]
    private static partial Regex PageImportRegex();
}
