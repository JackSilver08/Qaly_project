using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class PrivacyApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public PrivacyApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PolicyConsentAndDecision_AreCsrfProtectedPurposeBoundAndRevocable()
    {
        var projectId = await SeedProjectAsync();
        var policyRequest = PolicyRequest(projectId);

        var noCsrf = await _client.PostAsJsonAsync("/api/privacy/policies", policyRequest);
        noCsrf.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var csrf = await GetCsrfTokenAsync(_client);
        var policyResponse = await SendAsync(_client, HttpMethod.Post, "/api/privacy/policies", policyRequest, csrf);
        policyResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var policy = (await policyResponse.Content.ReadFromJsonAsync<ApiResult<RetentionPolicyDto>>(JsonOptions))!.Data!;

        var consentResponse = await SendAsync(
            _client,
            HttpMethod.Post,
            "/api/privacy/consents",
            new PrivacyConsentGrantRequest(
                projectId,
                policy.Id,
                PrivacyPurposes.MeetingActionExtraction,
                PrivacyProviderClasses.Local,
                "meeting",
                null,
                "privacy-api-v1",
                null),
            csrf);
        consentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var consent = (await consentResponse.Content.ReadFromJsonAsync<ApiResult<PrivacyConsentDto>>(JsonOptions))!.Data!;

        var decisionResponse = await SendAsync(
            _client,
            HttpMethod.Post,
            "/api/privacy/decisions/evaluate",
            new PrivacyDecisionRequest(
                projectId,
                policy.Id,
                consent.Id,
                PrivacyPurposes.MeetingActionExtraction,
                PrivacyDataClasses.SensitiveCollaboration,
                PrivacyProviderClasses.Local,
                30,
                "meeting",
                null),
            csrf);
        var decision = (await decisionResponse.Content.ReadFromJsonAsync<ApiResult<PrivacyDecisionDto>>(JsonOptions))!.Data!;
        decision.Allowed.Should().BeTrue();
        decision.LocalEligible.Should().BeTrue();
        decision.CloudEligible.Should().BeFalse();

        var revokeResponse = await SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/privacy/consents/{consent.Id}/revoke",
            new PrivacyConsentRevokeRequest("User revoked", consent.RowVersion),
            csrf);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var blockedResponse = await SendAsync(
            _client,
            HttpMethod.Post,
            "/api/privacy/decisions/evaluate",
            new PrivacyDecisionRequest(
                projectId,
                policy.Id,
                consent.Id,
                PrivacyPurposes.MeetingActionExtraction,
                PrivacyDataClasses.SensitiveCollaboration,
                PrivacyProviderClasses.Local,
                30,
                "meeting",
                null),
            csrf);
        var blocked = (await blockedResponse.Content.ReadFromJsonAsync<ApiResult<PrivacyDecisionDto>>(JsonOptions))!.Data!;
        blocked.Allowed.Should().BeFalse();
        blocked.ErrorCode.Should().Be(PrivacyErrorCodes.ConsentRevoked);
    }

    [Fact]
    public async Task DataSubjectExport_RequiresAcceptanceRunsAsynchronouslyAndRechecksDownloadAuthorization()
    {
        var projectId = await SeedProjectAsync();
        var csrf = await GetCsrfTokenAsync(_client);
        var submitResponse = await SendAsync(
            _client,
            HttpMethod.Post,
            "/api/privacy/data-subject-requests",
            new DataSubjectRequestSubmitRequest(
                projectId,
                projectId,
                null,
                DataSubjectRequestTypes.Export,
                "project",
                "privacy-api-export-1"),
            csrf,
            "privacy-api-export-1");
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var dataRequest = (await submitResponse.Content.ReadFromJsonAsync<ApiResult<DataSubjectRequestDto>>(JsonOptions))!.Data!;

        var acceptResponse = await SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/privacy/data-subject-requests/{dataRequest.Id}/accept",
            new DataSubjectRequestDecisionRequest("Identity verified in test"),
            csrf);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", ProjectRoleRules.SystemAdmin);
        var adminCsrf = await GetCsrfTokenAsync(adminClient);
        var runResponse = await SendAsync(
            adminClient,
            HttpMethod.Post,
            "/api/privacy/worker/run-once",
            new { },
            adminCsrf);
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusResponse = await _client.GetAsync($"/api/privacy/data-subject-requests/{dataRequest.Id}");
        var status = (await statusResponse.Content.ReadFromJsonAsync<ApiResult<DataSubjectRequestDto>>(JsonOptions))!.Data!;
        status.Status.Should().Be(DataSubjectRequestStatuses.Completed);
        status.ResultExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        var download = await _client.GetAsync($"/api/privacy/data-subject-requests/{dataRequest.Id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var exportJson = await download.Content.ReadAsStringAsync();
        exportJson.Should().Contain("humanReadable");
        exportJson.Should().Contain("machineReadable");
        exportJson.Should().NotContain("test-password-hash");

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
        var forbidden = await otherClient.GetAsync($"/api/privacy/data-subject-requests/{dataRequest.Id}/download");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantPrivacyReads_DoNotExposeRecordsToUnrelatedUser()
    {
        var projectId = await SeedProjectAsync();
        var csrf = await GetCsrfTokenAsync(_client);
        await SendAsync(_client, HttpMethod.Post, "/api/privacy/policies", PolicyRequest(projectId), csrf);

        var other = _factory.CreateClient();
        other.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
        var policies = await other.GetAsync($"/api/privacy/policies?tenantId={projectId}&projectId={projectId}");
        var requests = await other.GetAsync($"/api/privacy/data-subject-requests?tenantId={projectId}");

        policies.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        requests.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Health_ExposesOverdueDataSubjectRequestAndExpiredLeaseSignals()
    {
        var projectId = await SeedProjectAsync();
        var now = DateTimeOffset.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.DataSubjectRequests.Add(new DataSubjectRequest
            {
                TenantId = projectId,
                ProjectId = projectId,
                RequesterUserId = _factory.TestUserId,
                SubjectUserId = _factory.TestUserId,
                RequestType = DataSubjectRequestTypes.Export,
                ScopeJson = "{\"scope\":\"project\"}",
                Status = DataSubjectRequestStatuses.Collecting,
                RequestedAt = now.AddDays(-31),
                AvailableAt = now.AddDays(-31),
                DeadlineAt = now.AddDays(-1),
                LeaseOwner = "expired-api-worker",
                LeaseExpiresAt = now.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", ProjectRoleRules.SystemAdmin);

        var response = await adminClient.GetAsync("/api/privacy/health");
        var health = (await response.Content.ReadFromJsonAsync<ApiResult<PrivacyHealthDto>>(JsonOptions))!.Data!;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        health.ExpiredLeases.Should().BeGreaterThan(0);
        health.OverdueDataSubjectRequests.Should().BeGreaterThan(0);
        health.Status.Should().NotBe("healthy");
    }

    private async Task<Guid> SeedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var existing = await db.Projects.FirstOrDefaultAsync(project => project.OwnerId == _factory.TestUserId);
        if (existing != null)
        {
            return existing.Id;
        }

        if (!await db.Users.AnyAsync(user => user.Id == _factory.TestUserId))
        {
            db.Users.Add(new User
            {
                Id = _factory.TestUserId,
                FullName = "Privacy API User",
                Email = "privacy-api@qaly.test",
                PasswordHash = "test-password-hash",
                Role = ProjectRoleRules.Owner
            });
        }

        var project = new Project
        {
            Name = "Privacy API Project",
            Code = $"P-{Guid.NewGuid():N}"[..12],
            OwnerId = _factory.TestUserId
        };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = _factory.TestUserId,
            Role = ProjectRoleRules.Owner
        });
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static RetentionPolicyUpsertRequest PolicyRequest(Guid projectId)
        => new(
            projectId,
            projectId,
            "API meeting policy",
            PrivacyDataClasses.SensitiveCollaboration,
            PrivacyPurposes.MeetingActionExtraction,
            [7, 30, 90],
            30,
            PrivacyExpiryActions.Redact,
            false,
            true,
            true,
            null,
            null,
            null);

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions);
        return payload!.Token;
    }

    private static Task<HttpResponseMessage> SendAsync<T>(
        HttpClient client,
        HttpMethod method,
        string url,
        T body,
        string csrf,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private sealed record CsrfResponse(string Token);
    private sealed record ApiResult<T>(
        bool IsSuccess,
        T? Data,
        string? Error,
        string? ErrorCode,
        int StatusCode);
}
