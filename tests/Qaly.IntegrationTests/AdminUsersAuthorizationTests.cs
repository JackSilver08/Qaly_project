using System.Net;
using FluentAssertions;

namespace Qaly.IntegrationTests;

public class AdminUsersAuthorizationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AdminUsersAuthorizationTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task UserManagementApi_AllowsRootAdmin()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/admin/users?page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserManagementApi_RejectsModeratorWithoutDelegatedScope()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Moderator");

        var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserManagementApi_RejectsMember()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminPage_RejectsMemberAtServerBoundary()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var response = await client.GetAsync("/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
