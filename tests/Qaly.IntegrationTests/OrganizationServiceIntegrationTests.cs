using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Web.Controllers;

namespace Qaly.IntegrationTests;

public class OrganizationServiceIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public OrganizationServiceIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AddMember_DuplicateMember_ReturnsConflict409()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
                new User { Id = memberId, FullName = "Member", Email = $"member-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Dup Member Org",
                Code = $"dup-code-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = memberId,
                Role = "Member"
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", ownerId.ToString());

        var response = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/organizations/{organizationId}/members", new AddOrganizationMemberRequest(memberId, "Member"));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveMember_WhenTargetIsOwner_ReturnsConflict409()
    {
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.Add(new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Remove Owner Org",
                Code = $"rem-code-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = ownerId,
                Role = "Owner"
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", ownerId.ToString());

        var response = await SendWithCsrfAsync(client, HttpMethod.Delete,
            $"/api/organizations/{organizationId}/members/{ownerId}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateUserRole_WhenValid_ReturnsSuccess()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
                new User { Id = memberId, FullName = "Member", Email = $"member-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Update Role Org",
                Code = $"upd-code-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = memberId,
                Role = "Member"
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", ownerId.ToString());

        var response = await SendWithCsrfAsync(client, HttpMethod.Patch,
            $"/api/organizations/{organizationId}/users/{memberId}", new UpdateOrganizationUserRoleRequest("BillingAdmin"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivateOrganization_BlocksAccessToProjects()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
                new User { Id = memberId, FullName = "Member", Email = $"member-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Deactivate Org",
                Code = $"deact-code-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Org Project",
                Code = $"proj-code-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = memberId,
                Role = "Member"
            });
            await db.SaveChangesAsync();
        }

        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("X-Test-UserId", ownerId.ToString());

        // Deactivate Org
        var deactResponse = await SendWithCsrfAsync(ownerClient, HttpMethod.Delete,
            $"/api/organizations/{organizationId}");
        deactResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify member cannot access project in deactivated org
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add("X-Test-UserId", memberId.ToString());

        var getProjResponse = await memberClient.GetAsync($"/api/projects/{projectId}");
        getProjResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body = null)
    {
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf"))!.Token;
        using var request = new HttpRequestMessage(method, url)
        {
            Content = body == null ? null : JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string Token);
}
