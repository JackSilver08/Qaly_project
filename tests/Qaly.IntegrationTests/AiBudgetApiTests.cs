using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiBudgetApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiBudgetApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BillingAdmin_ReadsTenantScopedUsageBudgetAndInheritedProjectPolicy()
    {
        var tenant = await SeedTenantAsync(OrganizationRoleRules.BillingAdmin);

        var scopesResponse = await _client.GetAsync("/api/ai/budget/scopes");
        var usageResponse = await _client.GetAsync($"/api/ai/usage?organizationId={tenant.OrganizationId}");
        var budgetResponse = await _client.GetAsync($"/api/ai/budget?organizationId={tenant.OrganizationId}");
        var inheritedResponse = await _client.GetAsync(
            $"/api/ai/budget?organizationId={tenant.OrganizationId}&projectId={tenant.FirstProjectId}");

        scopesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var scopes = await ReadResultAsync<IReadOnlyList<AiBudgetScopeDto>>(scopesResponse);
        scopes.Data.Should().Contain(item =>
            item.ScopeType == "organization" &&
            item.ScopeId == tenant.OrganizationId);
        scopes.Data.Should().Contain(item =>
            item.ScopeType == "project" &&
            item.ScopeId == tenant.SecondProjectId);

        usageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var usage = (await ReadResultAsync<AiUsageSnapshotDto>(usageResponse)).Data!;
        usage.AttemptCount.Should().Be(3);
        usage.SucceededCount.Should().Be(2);
        usage.FailedCount.Should().Be(1);
        usage.EffectiveCostUsd.Should().Be(7.5m);
        usage.ByProvider.Should().Contain(item => item.Key == "provider-a" && item.AttemptCount == 2);
        usage.ByProvider.Should().Contain(item => item.Key == "provider-b" && item.AttemptCount == 1);
        usage.ByFunction.Should().Contain(item => item.Key == "project_progress_summary");
        usage.ByCache.Should().Contain(item => item.Key == "hit" && item.AttemptCount == 1);

        budgetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var budget = (await ReadResultAsync<AiBudgetSnapshotDto>(budgetResponse)).Data!;
        budget.PolicySource.Should().Be("organization");
        budget.IsInherited.Should().BeFalse();
        budget.DailyUsageUsd.Should().Be(7.5m);
        budget.WarningActive.Should().BeTrue();
        budget.HardStopActive.Should().BeFalse();
        budget.CanEdit.Should().BeTrue();
        budget.EditingEnabled.Should().BeTrue();

        inheritedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inherited = (await ReadResultAsync<AiBudgetSnapshotDto>(inheritedResponse)).Data!;
        inherited.IsInherited.Should().BeTrue();
        inherited.PolicySource.Should().Be("organization");
        inherited.PolicyId.Should().BeNull();
        inherited.Version.Should().BeNull();
        inherited.EffectivePolicyId.Should().Be(budget.EffectivePolicyId);
        inherited.DailyUsageUsd.Should().Be(7.5m);

        var csrf = await GetCsrfTokenAsync(_client);
        var projectOverride = await _client.SendAsync(CreateUpdateRequest(
            tenant.OrganizationId,
            csrf,
            new UpdateAiBudgetPolicyDto(5m, 50m, 60, true, false, Confirmed: true),
            tenant.FirstProjectId));
        projectOverride.StatusCode.Should().Be(HttpStatusCode.OK);
        var projectBudget = (await ReadResultAsync<AiBudgetSnapshotDto>(projectOverride)).Data!;
        projectBudget.IsInherited.Should().BeFalse();
        projectBudget.PolicySource.Should().Be("project");
        projectBudget.PolicyId.Should().NotBeNull();
        projectBudget.DailyUsageUsd.Should().Be(3.5m);
    }

    [Fact]
    public async Task UpdateBudget_RequiresCsrfConfirmationAndCurrentVersionThenAuditsReadBack()
    {
        var tenant = await SeedTenantAsync(OrganizationRoleRules.OrganizationAdmin);
        var current = await ReadResultAsync<AiBudgetSnapshotDto>(
            await _client.GetAsync($"/api/ai/budget?organizationId={tenant.OrganizationId}"));
        var version = current.Data!.Version;
        var update = new UpdateAiBudgetPolicyDto(
            12m,
            120m,
            75,
            true,
            false,
            version,
            Confirmed: true);

        var missingCsrf = await _client.PutAsJsonAsync(
            $"/api/ai/budget?organizationId={tenant.OrganizationId}",
            update);
        missingCsrf.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var csrf = await GetCsrfTokenAsync(_client);
        var unconfirmed = await _client.SendAsync(CreateUpdateRequest(
            tenant.OrganizationId,
            csrf,
            update with { Confirmed = false }));
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadResultAsync<AiBudgetSnapshotDto>(unconfirmed)).ErrorCode
            .Should().Be(AiErrorCodes.BudgetConfirmationRequired);

        var saved = await _client.SendAsync(CreateUpdateRequest(tenant.OrganizationId, csrf, update));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var savedBudget = (await ReadResultAsync<AiBudgetSnapshotDto>(saved)).Data!;
        savedBudget.DailyBudgetUsd.Should().Be(12m);
        savedBudget.MonthlyBudgetUsd.Should().Be(120m);
        savedBudget.WarningAtPercent.Should().Be(75);
        savedBudget.Version.Should().NotBe(version);

        var stale = await _client.SendAsync(CreateUpdateRequest(
            tenant.OrganizationId,
            csrf,
            update with { DailyBudgetUsd = 13m }));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadResultAsync<AiBudgetSnapshotDto>(stale)).ErrorCode
            .Should().Be(AiErrorCodes.BudgetPolicyConflict);

        var readBack = (await ReadResultAsync<AiBudgetSnapshotDto>(
            await _client.GetAsync($"/api/ai/budget?organizationId={tenant.OrganizationId}"))).Data!;
        readBack.DailyBudgetUsd.Should().Be(12m);
        readBack.Version.Should().Be(savedBudget.Version);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var policy = await db.AiBudgetPolicies.SingleAsync(item =>
            item.TenantId == tenant.OrganizationId &&
            item.ProjectId == null);
        policy.DailyBudgetUsd.Should().Be(12m);
        (await db.AuditLogs.CountAsync(item =>
            item.Action == "UpdateAiBudgetPolicy" &&
            item.EntityId == policy.Id.ToString())).Should().Be(1);
    }

    [Fact]
    public async Task CrossTenantAndMismatchedScope_ReturnNotFoundWithoutLedgerDisclosure()
    {
        var tenant = await SeedTenantAsync(OrganizationRoleRules.OrganizationAdmin);
        var outsiderId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.Add(new User
            {
                Id = outsiderId,
                FullName = "Outside User",
                Email = $"outside-{outsiderId:N}@qaly.test",
                PasswordHash = "test"
            });
            await db.SaveChangesAsync();
        }

        var outsider = _factory.CreateClient();
        outsider.DefaultRequestHeaders.Add("X-Test-UserId", outsiderId.ToString());
        var deniedUsage = await outsider.GetAsync($"/api/ai/usage?organizationId={tenant.OrganizationId}");
        var deniedBudget = await outsider.GetAsync($"/api/ai/budget?organizationId={tenant.OrganizationId}");
        deniedUsage.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deniedBudget.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadResultAsync<AiUsageSnapshotDto>(deniedUsage)).Data.Should().BeNull();
        (await ReadResultAsync<AiBudgetSnapshotDto>(deniedBudget)).Data.Should().BeNull();

        var mismatch = await _client.GetAsync(
            $"/api/ai/budget?organizationId={Guid.NewGuid()}&projectId={tenant.FirstProjectId}");
        mismatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadResultAsync<AiBudgetSnapshotDto>(mismatch)).Data.Should().BeNull();
    }

    private async Task<SeededTenant> SeedTenantAsync(string currentUserRole)
    {
        var organizationId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(item => item.Id == _factory.TestUserId))
        {
            db.Users.Add(new User
            {
                Id = _factory.TestUserId,
                FullName = "AI Budget Admin",
                Email = $"budget-{_factory.TestUserId:N}@qaly.test",
                PasswordHash = "test"
            });
        }

        var owner = new User
        {
            Id = ownerId,
            FullName = "Tenant Owner",
            Email = $"owner-{ownerId:N}@qaly.test",
            PasswordHash = "test"
        };
        var organization = new Organization
        {
            Id = organizationId,
            Name = $"Budget Tenant {organizationId:N}",
            Code = $"BT{organizationId:N}"[..12],
            OwnerId = ownerId,
            Owner = owner
        };
        var firstProject = new Project
        {
            Id = firstProjectId,
            OrganizationId = organizationId,
            OwnerId = ownerId,
            Owner = owner,
            Name = "Budget Project A",
            Code = $"BPA{firstProjectId:N}"[..12]
        };
        var secondProject = new Project
        {
            Id = secondProjectId,
            OrganizationId = organizationId,
            OwnerId = ownerId,
            Owner = owner,
            Name = "Budget Project B",
            Code = $"BPB{secondProjectId:N}"[..12]
        };
        db.AddRange(owner, organization, firstProject, secondProject);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            Organization = organization,
            UserId = _factory.TestUserId,
            Role = currentUserRole
        });
        db.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = organizationId,
            DailyBudgetUsd = 10m,
            MonthlyBudgetUsd = 100m,
            WarnAtPercent = 70,
            HardStopEnabled = true
        });
        var now = DateTimeOffset.UtcNow;
        db.AiUsageLedger.AddRange(
            Usage(organizationId, firstProjectId, "project_progress_summary", "provider-a", "succeeded", 2m, true, now),
            Usage(organizationId, firstProjectId, "project_progress_summary", "provider-a", "failed", 1.5m, false, now),
            Usage(organizationId, secondProjectId, "sprint_progress_summary", "provider-b", "succeeded", 4m, false, now));
        await db.SaveChangesAsync();
        return new SeededTenant(organizationId, firstProjectId, secondProjectId);
    }

    private static AiUsageLedger Usage(
        Guid tenantId,
        Guid projectId,
        string jobType,
        string provider,
        string status,
        decimal cost,
        bool cacheHit,
        DateTimeOffset createdAt)
        => new()
        {
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = Guid.NewGuid(),
            JobType = jobType,
            ProviderName = provider,
            ModelName = "budget-test-model",
            InputTokens = 100,
            OutputTokens = 50,
            EstimatedCostUsd = cost,
            Status = status,
            CacheHit = cacheHit,
            CreatedAt = createdAt
        };

    private static HttpRequestMessage CreateUpdateRequest(
        Guid organizationId,
        string csrf,
        UpdateAiBudgetPolicyDto update,
        Guid? projectId = null)
    {
        var query = projectId.HasValue
            ? $"organizationId={organizationId}&projectId={projectId.Value}"
            : $"organizationId={organizationId}";
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/ai/budget?{query}")
        {
            Content = JsonContent.Create(update)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    private static async Task<ResultEnvelope<T>> ReadResultAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ResultEnvelope<T>>(JsonOptions))!;

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions);
        return payload!.Token;
    }

    private sealed record SeededTenant(Guid OrganizationId, Guid FirstProjectId, Guid SecondProjectId);
    private sealed record CsrfResponse(string Token);
    private sealed record ResultEnvelope<T>(
        bool IsSuccess,
        T? Data,
        string? Error,
        string? ErrorCode,
        int StatusCode);
}
