using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

/// <summary>
/// Phase 2B regression tests for P0 cross-tenant data leakage found in DashboardController.
///
/// P0-1: GET /api/dashboard/recent-activities previously loaded ALL project names into a
///       dictionary before filtering, leaking project identity (name) across tenant boundaries.
///
/// P0-2: GET /api/dashboard/strategic-overview previously called _context.Users.Count() which
///       counts all users system-wide. This exposed global user count to any authenticated user
///       (used for teamWorkloadLevel calculation). Fixed to count only accessible project members.
/// </summary>
#pragma warning disable CA1707
public class DashboardTenantIsolationIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IntegrationTestFactory _factory;

    public DashboardTenantIsolationIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // P0-1: recent-activities — projectNames must not bleed across tenants
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scenario: Org A user performs an activity referencing Project A.
    ///           Org B user performs an activity referencing Project B.
    ///           When Org A user calls /api/dashboard/recent-activities, the
    ///           response must NOT contain Project B's name.
    ///
    /// Test Matrix:
    ///   Org A user → own activity with Project A name = PASS (visible)
    ///   Org A user → Org B activity with Project B name = BLOCK (hidden)
    /// </summary>
    [Fact]
    public async Task GetRecentActivities_ProjectName_DoesNotLeakAcrossTenants()
    {
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await EnsureUserExistsAsync(userAId, "Tenant A User", $"user-a-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExistsAsync(userBId, "Tenant B User", $"user-b-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

            db.Organizations.AddRange(
                new Organization
                {
                    Id = organizationAId,
                    Name = "Organization A",
                    Code = $"org-a-{Guid.NewGuid():N}",
                    OwnerId = userAId,
                    IsActive = true
                },
                new Organization
                {
                    Id = organizationBId,
                    Name = "Organization B",
                    Code = $"org-b-{Guid.NewGuid():N}",
                    OwnerId = userBId,
                    IsActive = true
                });

            db.OrganizationMembers.AddRange(
                new OrganizationMember
                {
                    OrganizationId = organizationAId,
                    UserId = userAId,
                    Role = "OrganizationAdmin"
                },
                new OrganizationMember
                {
                    OrganizationId = organizationBId,
                    UserId = userBId,
                    Role = "OrganizationAdmin"
                });

            db.Projects.AddRange(
                new Project
                {
                    Id = projectAId,
                    Name = "ALPHA_PROJECT_TENANT_A",
                    Code = $"alpha-{Guid.NewGuid():N}",
                    OwnerId = userAId,
                    OrganizationId = organizationAId
                },
                new Project
                {
                    Id = projectBId,
                    Name = "BETA_PROJECT_TENANT_B",
                    Code = $"beta-{Guid.NewGuid():N}",
                    OwnerId = userBId,
                    OrganizationId = organizationBId
                });

            db.AuditLogs.AddRange(
                // Org A: activity on Project A
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(Project),
                    EntityId = projectAId.ToString(),
                    UserId = userAId,
                    ChangesJson = $$"""{"projectId":"{{projectAId}}","title":"Alpha task update"}""",
                    Timestamp = now.AddMinutes(-5)
                },
                // Org B: activity on Project B (foreign to Org A)
                new AuditLog
                {
                    Action = "Create",
                    EntityType = nameof(Project),
                    EntityId = projectBId.ToString(),
                    UserId = userBId,
                    ChangesJson = $$"""{"projectId":"{{projectBId}}","title":"Beta task created"}""",
                    Timestamp = now.AddMinutes(-3)
                });

            await db.SaveChangesAsync();
        }

        // Act: call as Org A user
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/recent-activities");
        request.Headers.Add("X-Test-UserId", userAId.ToString());
        request.Headers.Add("X-Test-Role", "User");

        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RecentActivitiesResponse>(JsonOptions);
        payload.Should().NotBeNull();

        // Org A user must see their own activity
        payload!.LatestActivities.Should().Contain(a => a.ProjectId == projectAId,
            "Org A user should see activity on Project A");

        // Org A user must NOT see Org B's project name in any activity
        payload.LatestActivities.Should().NotContain(a => a.ProjectName == "BETA_PROJECT_TENANT_B",
            "Project B's name must not leak to Org A user");

        // Org A user must NOT see Org B's project ID in any activity
        payload.LatestActivities.Should().NotContain(a => a.ProjectId == projectBId,
            "Org B's project ID must not appear in Org A user's activities");
    }

    /// <summary>
    /// Symmetrical test: Org B user must not see Org A's project name.
    /// </summary>
    [Fact]
    public async Task GetRecentActivities_ProjectName_IsolatedInBothDirections()
    {
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await EnsureUserExistsAsync(userAId, "User A Cross", $"user-a-cross-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExistsAsync(userBId, "User B Cross", $"user-b-cross-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

            db.Projects.AddRange(
                new Project
                {
                    Id = projectAId,
                    Name = "GAMMA_PROJECT_TENANT_A",
                    Code = $"gamma-{Guid.NewGuid():N}",
                    OwnerId = userAId
                },
                new Project
                {
                    Id = projectBId,
                    Name = "DELTA_PROJECT_TENANT_B",
                    Code = $"delta-{Guid.NewGuid():N}",
                    OwnerId = userBId
                });

            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(Project),
                    EntityId = projectAId.ToString(),
                    UserId = userAId,
                    ChangesJson = $$"""{"projectId":"{{projectAId}}","title":"Gamma update"}""",
                    Timestamp = now.AddMinutes(-10)
                },
                new AuditLog
                {
                    Action = "Create",
                    EntityType = nameof(Project),
                    EntityId = projectBId.ToString(),
                    UserId = userBId,
                    ChangesJson = $$"""{"projectId":"{{projectBId}}","title":"Delta created"}""",
                    Timestamp = now.AddMinutes(-8)
                });

            await db.SaveChangesAsync();
        }

        // Org B user perspective
        using var clientB = _factory.CreateClient();
        using var requestB = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/recent-activities");
        requestB.Headers.Add("X-Test-UserId", userBId.ToString());
        requestB.Headers.Add("X-Test-Role", "User");

        var responseB = await clientB.SendAsync(requestB);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var payloadB = await responseB.Content.ReadFromJsonAsync<RecentActivitiesResponse>(JsonOptions);
        payloadB.Should().NotBeNull();

        // Org B sees their own project
        payloadB!.LatestActivities.Should().Contain(a => a.ProjectId == projectBId,
            "Org B user should see their own activity on Project B");

        // Org B must NOT see Org A's project name
        payloadB.LatestActivities.Should().NotContain(a => a.ProjectName == "GAMMA_PROJECT_TENANT_A",
            "Project A's name must not leak to Org B user");

        payloadB.LatestActivities.Should().NotContain(a => a.ProjectId == projectAId,
            "Org A's project must not appear in Org B user's response");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // P0-2: strategic-overview — teamWorkloadLevel must not use global user count
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scenario: Many users exist in the system across different tenants.
    ///           A user with 1 accessible project and 0 tasks should see "Medium" workload level.
    ///           Previously, _context.Users.Count() would count ALL users globally (a cross-tenant
    ///           information disclosure). Now it only counts users in accessible projects.
    ///
    /// The P0 here is that teamWorkloadLevel = tasks / userCount. If userCount is artificially
    /// inflated with foreign-tenant users, the metric becomes wrong — but more critically, the
    /// total count itself leaks information about system scale to any tenant.
    /// </summary>
    [Fact]
    public async Task GetStrategicOverview_TeamWorkloadLevel_UsesOnlyAccessibleProjectMembers()
    {
        var tenantUserAId = Guid.NewGuid();
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var foreignUserIds = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToArray();
        var projectAId = Guid.NewGuid();

        await EnsureUserExistsAsync(tenantUserAId, "Strategic User A", $"strategic-a-{Guid.NewGuid():N}@qaly.dev");
        for (var i = 0; i < foreignUserIds.Length; i++)
        {
            await EnsureUserExistsAsync(foreignUserIds[i], $"Foreign {i + 1}", $"foreign{i + 1}-{Guid.NewGuid():N}@qaly.dev");
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Organizations.AddRange(
                new Organization
                {
                    Id = organizationAId,
                    Name = "Organization A",
                    Code = $"org-a-{Guid.NewGuid():N}",
                    OwnerId = tenantUserAId,
                    IsActive = true
                },
                new Organization
                {
                    Id = organizationBId,
                    Name = "Organization B",
                    Code = $"org-b-{Guid.NewGuid():N}",
                    OwnerId = foreignUserIds[0],
                    IsActive = true
                });

            db.OrganizationMembers.AddRange(
                new OrganizationMember
                {
                    OrganizationId = organizationAId,
                    UserId = tenantUserAId,
                    Role = "OrganizationAdmin"
                },
                new OrganizationMember
                {
                    OrganizationId = organizationBId,
                    UserId = foreignUserIds[0],
                    Role = "OrganizationAdmin"
                },
                new OrganizationMember
                {
                    OrganizationId = organizationBId,
                    UserId = foreignUserIds[1],
                    Role = "Member"
                });

            db.Projects.Add(new Project
            {
                Id = projectAId,
                Name = "Strategic Project A",
                Code = $"strat-a-{Guid.NewGuid():N}",
                OwnerId = tenantUserAId,
                OrganizationId = organizationAId
            });

            db.Projects.Add(new Project
            {
                Id = projectBId,
                Name = "Strategic Project B",
                Code = $"strat-b-{Guid.NewGuid():N}",
                OwnerId = foreignUserIds[0],
                OrganizationId = organizationBId
            });

            db.TaskItems.AddRange(
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectAId,
                    Title = "Tenant A Task 1",
                    Status = "Todo",
                    Priority = "High",
                    ReporterId = tenantUserAId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectAId,
                    Title = "Tenant A Task 2",
                    Status = "InProgress",
                    Priority = "Medium",
                    ReporterId = tenantUserAId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectAId,
                    Title = "Tenant A Task 3",
                    Status = "Todo",
                    Priority = "Medium",
                    ReporterId = tenantUserAId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectAId,
                    Title = "Tenant A Task 4",
                    Status = "Todo",
                    Priority = "Medium",
                    ReporterId = tenantUserAId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectAId,
                    Title = "Tenant A Task 5",
                    Status = "Todo",
                    Priority = "Low",
                    ReporterId = tenantUserAId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectAId,
                    Title = "Tenant A Task 6",
                    Status = "Todo",
                    Priority = "Low",
                    ReporterId = tenantUserAId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectBId,
                    Title = "Tenant B Task 1",
                    Status = "Todo",
                    Priority = "High",
                    ReporterId = foreignUserIds[0],
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectBId,
                    Title = "Tenant B Task 2",
                    Status = "Todo",
                    Priority = "High",
                    ReporterId = foreignUserIds[0],
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectBId,
                    Title = "Tenant B Task 3",
                    Status = "InProgress",
                    Priority = "High",
                    ReporterId = foreignUserIds[0],
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                });

            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectBId,
                UserId = foreignUserIds[1],
                Role = "Member"
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/strategic-overview");
        request.Headers.Add("X-Test-UserId", tenantUserAId.ToString());
        request.Headers.Add("X-Test-Role", "User");

        var response = await client.SendAsync(request);

        // The endpoint should succeed even after the P0 fix
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<StrategicOverviewResponse>(JsonOptions);
        payload.Should().NotBeNull();

        // teamWorkloadLevel should reflect only accessible project context.
        // With 6 open tasks and 1 accessible user (the owner), ratio = 6/1 = 6 > 5 → "High".
        // If the old global count were used with 10 foreign users, the ratio would drop below 5.
        payload!.TeamWorkloadLevel.Should().Be("High");

        // The critical assertion: accessible project count for tenant A user
        payload.ActiveProjectCount.Should().Be(1,
            "Tenant A user has exactly 1 active project");
        payload.TaskCompletionRate.Should().Be(0,
            "Tenant A's tasks are all open");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Cross-Tenant Test Matrix: GET /api/dashboard/recent-activities
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full matrix test with two organizations and two users.
    ///
    /// | Scenario                                  | Expected | Actual |
    /// |-------------------------------------------|----------|--------|
    /// | Org A user → own project activities       | PASS     | verify |
    /// | Org A user → Org B project name in resp   | BLOCK    | verify |
    /// | Org B user → own project activities       | PASS     | verify |
    /// | Org B user → Org A project name in resp   | BLOCK    | verify |
    /// </summary>
    [Fact]
    public async Task GetRecentActivities_CrossTenantMatrix_BothDirectionsBlocked()
    {
        var orgAOwnerId = Guid.NewGuid();
        var orgBOwnerId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await EnsureUserExistsAsync(orgAOwnerId, "Matrix Owner A", $"matrix-a-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExistsAsync(orgBOwnerId, "Matrix Owner B", $"matrix-b-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

            db.Projects.AddRange(
                new Project { Id = projectAId, Name = "MATRIX_PROJ_A", Code = $"mpa-{Guid.NewGuid():N}", OwnerId = orgAOwnerId },
                new Project { Id = projectBId, Name = "MATRIX_PROJ_B", Code = $"mpb-{Guid.NewGuid():N}", OwnerId = orgBOwnerId }
            );

            db.AuditLogs.AddRange(
                new AuditLog { Action = "Create", EntityType = nameof(Project), EntityId = projectAId.ToString(), UserId = orgAOwnerId, ChangesJson = $$"""{"projectId":"{{projectAId}}","title":"Matrix A"}""", Timestamp = now.AddMinutes(-15) },
                new AuditLog { Action = "Create", EntityType = nameof(Project), EntityId = projectBId.ToString(), UserId = orgBOwnerId, ChangesJson = $$"""{"projectId":"{{projectBId}}","title":"Matrix B"}""", Timestamp = now.AddMinutes(-12) }
            );

            await db.SaveChangesAsync();
        }

        // Org A view
        var payloadA = await GetRecentActivitiesAsync(orgAOwnerId);
        payloadA.Should().NotBeNull();
        payloadA!.LatestActivities.Should().Contain(a => a.ProjectId == projectAId, "Org A → own project = PASS");
        payloadA.LatestActivities.Should().NotContain(a => a.ProjectName == "MATRIX_PROJ_B", "Org A → Org B project name = BLOCK");
        payloadA.LatestActivities.Should().NotContain(a => a.ProjectId == projectBId, "Org A → Org B project ID = BLOCK");

        // Org B view
        var payloadB = await GetRecentActivitiesAsync(orgBOwnerId);
        payloadB.Should().NotBeNull();
        payloadB!.LatestActivities.Should().Contain(a => a.ProjectId == projectBId, "Org B → own project = PASS");
        payloadB.LatestActivities.Should().NotContain(a => a.ProjectName == "MATRIX_PROJ_A", "Org B → Org A project name = BLOCK");
        payloadB.LatestActivities.Should().NotContain(a => a.ProjectId == projectAId, "Org B → Org A project ID = BLOCK");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    private async Task<RecentActivitiesResponse?> GetRecentActivitiesAsync(Guid userId)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/recent-activities");
        request.Headers.Add("X-Test-UserId", userId.ToString());
        request.Headers.Add("X-Test-Role", "User");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<RecentActivitiesResponse>(JsonOptions);
    }

    private async Task EnsureUserExistsAsync(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new User { Id = userId, FullName = name, Email = email, IsActive = true });
            await db.SaveChangesAsync();
        }
    }

    // DTOs matching DashboardController response shapes
    private sealed record RecentActivitiesResponse(
        int TodayCount,
        int WeekCount,
        IReadOnlyList<ActivityByDayDto> ActivityByDay,
        IReadOnlyList<RecentActivityDto> LatestActivities);

    private sealed record ActivityByDayDto(string Date, int Count);

    private sealed record RecentActivityDto(
        string Type,
        string Title,
        string ActorName,
        string? ProjectName,
        Guid? ProjectId,
        string CreatedAt);

    private sealed record StrategicOverviewResponse(
        int WorkspaceHealthScore,
        int AverageProjectProgress,
        int TaskCompletionRate,
        int RiskProjectCount,
        int OverdueTaskCount,
        int DueSoonTaskCount,
        int ActiveProjectCount,
        string TeamWorkloadLevel,
        string RiskLevel,
        IReadOnlyList<object> TopPriorityTasks);
}
#pragma warning restore CA1707
