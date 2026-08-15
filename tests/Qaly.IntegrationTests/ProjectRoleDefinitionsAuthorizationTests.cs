using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class ProjectRoleDefinitionsAuthorizationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public ProjectRoleDefinitionsAuthorizationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OrganizationMember_CanReadButCannotCreateCustomRoleViaApi()
    {
        var organizationId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.Add(new User { Id = ownerId, FullName = "Owner", Email = $"owner-{ownerId:N}@qaly.dev", IsActive = true });
            if (!db.Users.Any(user => user.Id == _factory.TestUserId))
            {
                db.Users.Add(new User { Id = _factory.TestUserId, FullName = "Member", Email = "test@qaly.dev", IsActive = true });
            }
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                OwnerId = ownerId,
                Name = "RBAC test",
                Code = $"rbac-{organizationId:N}"
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = _factory.TestUserId,
                Role = OrganizationRoleRules.Member
            });
            await db.SaveChangesAsync();
        }

        var access = await _client.GetAsync($"/api/organizations/{organizationId}/role-definitions/access");
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/role-definitions")
        {
            Content = JsonContent.Create(
                new CreateProjectRoleDefinitionDto("Backend Engineer", ProjectRoleRules.Developer))
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        var mutation = await _client.SendAsync(request);

        access.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessBody = await access.Content.ReadFromJsonAsync<ApiResult<bool>>();
        accessBody!.Data.Should().BeFalse();
        mutation.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);
    private sealed record CsrfResponse(string Token);
}
