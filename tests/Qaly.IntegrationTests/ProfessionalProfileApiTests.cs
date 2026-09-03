using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Organization;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class ProfessionalProfileApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;

    public ProfessionalProfileApiTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task OwnerCanVerifyProfile_ReadBackIsCanonical_AndAccessRoleDoesNotChange()
    {
        var fixture = await SeedAsync();
        using var client = _factory.CreateClient();
        var csrf = await CsrfAsync(client);
        var payload = new ReplaceMemberProfessionalProfilesDto(true,
            [new(fixture.DefinitionId, ProfessionalProfileCatalog.Proficient, OrganizationMemberProfessionalProfile.Verified, ProfessionalProfileCatalog.ManagerConfirmed, null, null, "API integration evidence")],
            []);
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/organizations/{fixture.OrganizationId}/members/{fixture.MemberId}/professional-profiles")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<ApiEnvelope<MemberProfessionalProfileSetDto>>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Profiles.Should().ContainSingle(item =>
            item.DefinitionId == fixture.DefinitionId && item.VerificationStatus == OrganizationMemberProfessionalProfile.Verified);
        result.Data.AuthorizationNotice.Should().Contain("không cấp quyền truy cập");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.OrganizationMembers.SingleAsync(item => item.OrganizationId == fixture.OrganizationId && item.UserId == fixture.MemberId))
            .Role.Should().Be(OrganizationRoleRules.Member);
    }

    [Fact]
    public async Task MemberCannotSelfVerifyProfessionalProfile()
    {
        var fixture = await SeedAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", fixture.MemberId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Member");
        var csrf = await CsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/organizations/{fixture.OrganizationId}/members/{fixture.MemberId}/professional-profiles")
        {
            Content = JsonContent.Create(new ReplaceMemberProfessionalProfilesDto(true,
                [new(fixture.DefinitionId, ProfessionalProfileCatalog.Expert, OrganizationMemberProfessionalProfile.Verified, null, null, null, null)],
                []))
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.OrganizationMemberProfessionalProfiles.CountAsync(item => item.OrganizationId == fixture.OrganizationId)).Should().Be(0);
    }

    [Fact]
    public async Task ModeratorViewScopeCanReadButCannotMutateProfessionalProfiles()
    {
        var fixture = await SeedAsync();
        var moderatorId = Guid.NewGuid();
        var grantorId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                new User { Id = moderatorId, FullName = "Scoped Moderator", Email = $"moderator-{Guid.NewGuid():N}@qaly.dev", Role = SystemRoleRules.Moderator, IsActive = true },
                new User { Id = grantorId, FullName = "Grantor", Email = $"grantor-{Guid.NewGuid():N}@qaly.dev", Role = SystemRoleRules.Admin, IsActive = true });
            db.ModeratorAssignments.Add(new ModeratorAssignment
            {
                ModeratorUserId = moderatorId,
                OrganizationId = fixture.OrganizationId,
                GrantedByUserId = grantorId,
                Capability = ModeratorCapabilities.ProfessionalProfilesView,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", moderatorId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", SystemRoleRules.Moderator);

        using var read = await client.GetAsync($"/api/organizations/{fixture.OrganizationId}/professional-profiles");
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var csrf = await CsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/organizations/{fixture.OrganizationId}/members/{fixture.MemberId}/professional-profiles")
        {
            Content = JsonContent.Create(new ReplaceMemberProfessionalProfilesDto(true,
                [new(fixture.DefinitionId, ProfessionalProfileCatalog.Proficient, OrganizationMemberProfessionalProfile.Verified, null, null, null, null)],
                []))
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        using var write = await client.SendAsync(request);

        write.StatusCode.Should().Be(HttpStatusCode.NotFound, "view delegation must not imply mutation");
    }

    private async Task<Fixture> SeedAsync()
    {
        var organizationId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(item => item.Id == _factory.TestUserId))
        {
            db.Users.Add(new User { Id = _factory.TestUserId, FullName = "Test Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", Role = "Member", IsActive = true });
        }
        db.Users.Add(new User { Id = memberId, FullName = "Profile Member", Email = $"member-{Guid.NewGuid():N}@qaly.dev", Role = "Member", IsActive = true });
        db.Organizations.Add(new Organization { Id = organizationId, OwnerId = _factory.TestUserId, Name = "Professional Profiles", Code = $"profiles-{Guid.NewGuid():N}", IsActive = true });
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organizationId, UserId = _factory.TestUserId, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organizationId, UserId = memberId, Role = OrganizationRoleRules.Member });
        db.ProfessionalProfileDefinitions.Add(new ProfessionalProfileDefinition { Id = definitionId, OrganizationId = organizationId, Key = "backend-engineer", Name = "Backend Engineer", Category = "Engineering", IsActive = true });
        await db.SaveChangesAsync();
        return new(organizationId, memberId, definitionId);
    }

    private static async Task<string> CsrfAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", Json))!.Token;

    private sealed record Fixture(Guid OrganizationId, Guid MemberId, Guid DefinitionId);
    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error);
}
