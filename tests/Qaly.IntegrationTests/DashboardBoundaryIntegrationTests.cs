using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Application.Services;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class DashboardBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public DashboardBoundaryIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetOverview_WhenUserHasNoProjects_DoesNotLeakOtherWorkspaceData()
    {
        var otherUserId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();

        await EnsureUserExists(_factory.TestUserId, "Test User", "test-user@qaly.dev");
        await EnsureUserExists(otherUserId, "Foreign Owner", "foreign-owner@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = foreignProjectId,
                Name = "Secret Project",
                Code = $"secret-{Guid.NewGuid():N}",
                OwnerId = otherUserId
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/dashboard/overview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Projects.Should().BeEmpty();
        payload.Team.Should().ContainSingle(member => member.Id == _factory.TestUserId);
        payload.Team.Should().NotContain(member => member.Email == "foreign-owner@qaly.dev");
    }

    [Fact]
    public async Task GetRecentActivities_WhenUserCannotAccessForeignProject_HidesForeignEntries()
    {
        var otherUserId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var visibleTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var foreignTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1);

        await EnsureUserExists(_factory.TestUserId, "Test User", "test-user@qaly.dev");
        await EnsureUserExists(otherUserId, "Foreign Owner", "foreign-owner@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = foreignProjectId,
                Name = "Secret Project",
                Code = $"secret-{Guid.NewGuid():N}",
                OwnerId = otherUserId
            });

            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(User),
                    EntityId = _factory.TestUserId.ToString(),
                    UserId = _factory.TestUserId,
                    ChangesJson = """{"title":"Profile update"}""",
                    Timestamp = visibleTimestamp
                },
                new AuditLog
                {
                    Action = "Create",
                    EntityType = nameof(Project),
                    EntityId = foreignProjectId.ToString(),
                    UserId = otherUserId,
                    ChangesJson = $$"""{"projectId":"{{foreignProjectId}}","title":"Secret Project"}""",
                    Timestamp = foreignTimestamp
                });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/dashboard/recent-activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RecentActivitiesResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.LatestActivities.Should().ContainSingle();
        payload.LatestActivities.Should().Contain(activity => activity.ActorName == "Test User");
        payload.LatestActivities.Should().NotContain(activity => activity.ProjectId == foreignProjectId);
    }

    [Fact]
    public async Task GetOverview_CustomRoleUsesInheritedPermissionsAndKeepsCustomLabel()
    {
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var roleKey = $"delivery-lead-{Guid.NewGuid():N}";
        await EnsureUserExists(_factory.TestUserId, "Custom role user", $"custom-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(ownerId, "Owner", $"owner-{Guid.NewGuid():N}@qaly.dev");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Custom role tenant",
                Code = $"custom-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = _factory.TestUserId,
                Role = "Member"
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Custom role project",
                Code = $"project-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = _factory.TestUserId,
                Role = roleKey
            });
            db.ProjectRoleDefinitions.Add(new ProjectRoleDefinition
            {
                OrganizationId = organizationId,
                Key = roleKey,
                DisplayName = "Delivery Lead",
                BaseRole = "Manager",
                CreatedByUserId = ownerId,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/dashboard/overview");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>(JsonOptions);
        var project = payload!.Projects.Single(item => item.Id == projectId);

        project.Permissions.Role.Should().Be(roleKey);
        project.Permissions.RoleLabel.Should().Be("Delivery Lead");
        project.Permissions.CanManageProject.Should().BeTrue();
        project.Permissions.CanManageMembers.Should().BeTrue();
    }

    [Fact]
    public async Task GetOverview_OrganizationAdminSeesPortfolioProjectWithManagementCapabilities()
    {
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await EnsureUserExists(_factory.TestUserId, "Organization admin", $"org-admin-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(ownerId, "Owner", $"owner-{Guid.NewGuid():N}@qaly.dev");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Portfolio tenant",
                Code = $"portfolio-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = _factory.TestUserId,
                Role = OrganizationRoleRules.OrganizationAdmin
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Portfolio project",
                Code = $"project-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/dashboard/overview");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>(JsonOptions);
        var project = payload!.Projects.Single(item => item.Id == projectId);

        project.Permissions.Role.Should().Be(OrganizationRoleRules.OrganizationAdmin);
        project.Permissions.CanManageProject.Should().BeTrue();
        project.Permissions.CanManageMembers.Should().BeTrue();
    }

    [Fact]
    public async Task AttentionAndStrategicOverview_OrganizationAdminGetsPortfolioButNotUnrelatedPrivateTaskMetrics()
    {
        var organizationAdminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var publicTaskId = Guid.NewGuid();
        var privateTaskId = Guid.NewGuid();
        await EnsureUserExists(organizationAdminId, "Portfolio admin", $"portfolio-admin-{Guid.NewGuid():N}@qaly.dev");
        await EnsureUserExists(ownerId, "Private task owner", $"private-owner-{Guid.NewGuid():N}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Metric boundary tenant",
                Code = $"metric-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = organizationAdminId,
                Role = OrganizationRoleRules.OrganizationAdmin
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Metric boundary project",
                Code = $"metric-project-{Guid.NewGuid():N}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            db.TaskItems.AddRange(
                new TaskItem
                {
                    Id = publicTaskId,
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = "Visible overdue task",
                    Status = "Todo",
                    Priority = "High",
                    DueDate = DateTimeOffset.UtcNow.AddDays(-2)
                },
                new TaskItem
                {
                    Id = privateTaskId,
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = "Confidential overdue task",
                    Status = "Todo",
                    Priority = "Critical",
                    DueDate = DateTimeOffset.UtcNow.AddDays(-3),
                    IsPrivate = true
                });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", organizationAdminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var attention = await client.GetFromJsonAsync<AttentionSummaryResponse>(
            "/api/dashboard/attention-summary", JsonOptions);
        attention!.OverdueTasks.Should().Be(1);

        var strategic = await client.GetFromJsonAsync<StrategicOverviewResponse>(
            "/api/dashboard/strategic-overview", JsonOptions);
        strategic!.ActiveProjectCount.Should().Be(1);
        strategic.OverdueTaskCount.Should().Be(1);
        strategic.TopPriorityTasks.Should().ContainSingle(task => task.Id == publicTaskId);
        strategic.TopPriorityTasks.Should().NotContain(task => task.Id == privateTaskId);
    }

    [Fact]
    public async Task RecentActivities_OrganizationAdminDoesNotSeeUnrelatedPrivateTaskAudit()
    {
        var organizationAdminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var publicTaskId = Guid.NewGuid();
        var privateTaskId = Guid.NewGuid();
        var marker = Guid.NewGuid().ToString("N");
        await EnsureUserExists(organizationAdminId, "Activity admin", $"activity-admin-{marker}@qaly.dev");
        await EnsureUserExists(ownerId, "Activity owner", $"activity-owner-{marker}@qaly.dev");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = $"Activity tenant {marker}",
                Code = $"activity-{marker}",
                OwnerId = ownerId,
                IsActive = true
            });
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = organizationAdminId,
                Role = OrganizationRoleRules.OrganizationAdmin
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = $"Activity project {marker}",
                Code = $"activity-project-{marker}",
                OwnerId = ownerId,
                OrganizationId = organizationId
            });
            db.TaskItems.AddRange(
                new TaskItem
                {
                    Id = publicTaskId,
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = $"Visible activity {marker}",
                    Status = "Todo"
                },
                new TaskItem
                {
                    Id = privateTaskId,
                    ProjectId = projectId,
                    ReporterId = ownerId,
                    Title = $"Secret activity {marker}",
                    Status = "Todo",
                    IsPrivate = true
                });
            db.AuditLogs.AddRange(
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(TaskItem),
                    EntityId = publicTaskId.ToString(),
                    UserId = ownerId,
                    ChangesJson = $$"""{"title":"Visible activity {{marker}}","projectId":"{{projectId}}"}""",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(1)
                },
                new AuditLog
                {
                    Action = "Update",
                    EntityType = nameof(TaskItem),
                    EntityId = privateTaskId.ToString(),
                    UserId = ownerId,
                    ChangesJson = $$"""{"title":"Secret activity {{marker}}","projectId":"{{projectId}}"}""",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(2)
                });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", organizationAdminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var response = await client.GetFromJsonAsync<RecentActivitiesResponse>(
            "/api/dashboard/recent-activities", JsonOptions);

        response!.LatestActivities.Should().Contain(activity => activity.Title.Contains($"Visible activity {marker}"));
        response.LatestActivities.Should().NotContain(activity => activity.Title.Contains($"Secret activity {marker}"));
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!db.Users.Any(user => user.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = name,
                Email = email,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed record DashboardOverviewResponse(
        IReadOnlyList<DashboardProjectResponse> Projects,
        IReadOnlyList<DashboardMemberResponse> Team);

    private sealed record DashboardProjectResponse(Guid Id, string Name, ProjectPermissionsResponse Permissions);

    private sealed record ProjectPermissionsResponse(
        string Role,
        string RoleLabel,
        bool CanManageProject,
        bool CanManageMembers);

    private sealed record DashboardMemberResponse(Guid Id, string FullName, string Email);

    private sealed record RecentActivitiesResponse(
        int TodayCount,
        int WeekCount,
        IReadOnlyList<ActivityByDayResponse> ActivityByDay,
        IReadOnlyList<RecentActivityResponse> LatestActivities);

    private sealed record ActivityByDayResponse(string Date, int Count);

    private sealed record RecentActivityResponse(
        string Type,
        string Title,
        string ActorName,
        string? ProjectName,
        Guid? ProjectId,
        string CreatedAt);

    private sealed record AttentionSummaryResponse(int OverdueTasks);

    private sealed record StrategicOverviewResponse(
        int OverdueTaskCount,
        int ActiveProjectCount,
        IReadOnlyList<StrategicTaskResponse> TopPriorityTasks);

    private sealed record StrategicTaskResponse(Guid Id, string Title);
}
#pragma warning restore CA1707
