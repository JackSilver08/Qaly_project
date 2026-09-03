using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Project;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class ProjectResourceBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;

    public ProjectResourceBoundaryIntegrationTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task WorkloadAndSprintMetrics_ExcludeUnrelatedPrivateTasks_AndCustomerCannotReadInternalWorkload()
    {
        var data = await SeedAsync();
        using var manager = CreateClient(data.DeveloperId);

        var workloadResponse = await manager.GetAsync($"/api/projects/{data.ProjectId}/workload");
        workloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var workload = await ReadResultAsync<ProjectWorkloadDto>(workloadResponse);
        workload.MembersWorkload.Sum(member => member.TaskCount).Should().Be(1,
            "the manager is not a participant of the private task");

        var sprintResponse = await manager.GetAsync($"/api/projects/{data.ProjectId}/sprints");
        sprintResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sprints = await ReadResultAsync<List<SprintDto>>(sprintResponse);
        sprints.Should().ContainSingle();
        sprints[0].TaskCount.Should().Be(1);

        using var customer = CreateClient(data.CustomerId);
        (await customer.GetAsync($"/api/projects/{data.ProjectId}/workload"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignTasksToSprint_WhenOneTaskIsPrivate_FailsAtomicallyWithoutPartialAssignment()
    {
        var data = await SeedAsync(assignTasksToSprint: false);
        using var manager = CreateClient(data.OrganizationAdminId);
        var csrf = (await manager.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/sprints/{data.SprintId}/tasks")
        {
            Content = JsonContent.Create(new AssignSprintTasksRequest([data.PublicTaskId, data.PrivateTaskId]))
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        var response = await manager.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var stored = await db.TaskItems.AsNoTracking()
            .Where(task => task.Id == data.PublicTaskId || task.Id == data.PrivateTaskId)
            .ToListAsync();
        stored.Should().OnlyContain(task => task.SprintId == null);
    }

    [Fact]
    public async Task CreateTask_EnforcesCanonicalProjectRoleCapability()
    {
        var data = await SeedAsync();
        var payload = new CreateTaskDto(
            "Capability boundary task",
            "Created only by a role with CanCreateTask.",
            "Medium",
            DateTimeOffset.UtcNow.AddDays(7),
            4,
            data.ProjectId,
            null);

        foreach (var deniedUserId in new[] { data.MemberId, data.ViewerId, data.CustomerId })
        {
            using var denied = CreateClient(deniedUserId);
            var deniedResponse = await PostWithCsrfAsync(denied, "/api/tasks", payload);
            deniedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using var developer = CreateClient(data.DeveloperId);
        var allowedResponse = await PostWithCsrfAsync(developer, "/api/tasks", payload);
        allowedResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(task =>
            task.ProjectId == data.ProjectId &&
            task.Title == payload.Title)).Should().Be(1);
    }

    [Fact]
    public async Task SprintMutations_RejectInvalidDatesAndBlankStatusWithoutPersistingChanges()
    {
        var data = await SeedAsync();
        using var manager = CreateClient(data.ManagerId);
        var now = DateTimeOffset.UtcNow;

        var invalidCreate = await PostWithCsrfAsync(
            manager,
            $"/api/projects/{data.ProjectId}/sprints",
            new CreateSprintRequest("Invalid schedule", now.AddDays(2), now, null));
        invalidCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var invalidUpdate = await SendWithCsrfAsync(
            manager,
            HttpMethod.Patch,
            $"/api/sprints/{data.SprintId}",
            new UpdateSprintRequest("Boundary Sprint", now, now.AddDays(7), null!, null));
        invalidUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Set<Sprint>().CountAsync(sprint => sprint.ProjectId == data.ProjectId))
            .Should().Be(1);
        (await db.Set<Sprint>().AsNoTracking().SingleAsync(sprint => sprint.Id == data.SprintId))
            .Status.Should().Be("Planning");
    }

    [Fact]
    public async Task SprintPreset_UsesProjectWindowAndRejectsDuplicatePresetAtomically()
    {
        var data = await SeedAsync();
        var windowStart = DateTimeOffset.UtcNow.AddDays(2);
        var windowEnd = windowStart.AddDays(20);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var project = await db.Projects.SingleAsync(item => item.Id == data.ProjectId);
            project.StartDate = windowStart;
            project.EndDate = windowEnd;
            await db.SaveChangesAsync();
        }

        using var manager = CreateClient(data.ManagerId);
        var created = await PostWithCsrfAsync(
            manager,
            $"/api/projects/{data.ProjectId}/sprints/presets",
            new CreateSprintPresetRequest("waterfall"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var presets = await db.Set<Sprint>().AsNoTracking()
                .Where(sprint => sprint.ProjectId == data.ProjectId && sprint.Id != data.SprintId)
                .OrderBy(sprint => sprint.StartDate)
                .ToListAsync();
            presets.Should().HaveCount(4);
            presets.Should().OnlyContain(sprint =>
                sprint.StartDate >= windowStart && sprint.EndDate <= windowEnd && sprint.EndDate >= sprint.StartDate);
            presets.Zip(presets.Skip(1)).Should().OnlyContain(pair => pair.First.EndDate < pair.Second.StartDate);
        }

        var duplicate = await PostWithCsrfAsync(
            manager,
            $"/api/projects/{data.ProjectId}/sprints/presets",
            new CreateSprintPresetRequest("waterfall"));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.Set<Sprint>().CountAsync(sprint => sprint.ProjectId == data.ProjectId))
            .Should().Be(5);
    }

    [Fact]
    public async Task CreateTask_RejectsOversizedTitleNegativeEstimateAndForeignSprint()
    {
        var data = await SeedAsync();
        var foreignProjectId = Guid.NewGuid();
        var foreignSprintId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Projects.Add(new Project
            {
                Id = foreignProjectId,
                Name = "Foreign Sprint project",
                Code = $"FS-{Guid.NewGuid():N}"[..12],
                OwnerId = data.DeveloperId,
                Status = "Active"
            });
            db.Set<Sprint>().Add(new Sprint
            {
                Id = foreignSprintId,
                ProjectId = foreignProjectId,
                Name = "Foreign Sprint",
                StartDate = DateTimeOffset.UtcNow,
                EndDate = DateTimeOffset.UtcNow.AddDays(7)
            });
            await db.SaveChangesAsync();
        }

        using var developer = CreateClient(data.DeveloperId);
        var invalidPayloads = new[]
        {
            new CreateTaskDto(new string('x', 301), null, "Medium", null, 1, data.ProjectId, null),
            new CreateTaskDto("Negative estimate", null, "Medium", null, -1, data.ProjectId, null),
            new CreateTaskDto("Foreign Sprint", null, "Medium", null, 1, data.ProjectId, null, SprintId: foreignSprintId)
        };

        foreach (var payload in invalidPayloads)
        {
            var response = await PostWithCsrfAsync(developer, "/api/tasks", payload);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.TaskItems.CountAsync(task => task.ProjectId == data.ProjectId))
            .Should().Be(2);
    }

    private async Task<SeededData> SeedAsync(bool assignTasksToSprint = true)
    {
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var developerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var organizationAdminId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        var publicTaskId = Guid.NewGuid();
        var privateTaskId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        db.Users.AddRange(
            User(ownerId, "Resource owner"),
            User(managerId, "Resource manager"),
            User(developerId, "Resource developer"),
            User(memberId, "Resource member"),
            User(viewerId, "Resource viewer"),
            User(customerId, "Resource customer"),
            User(organizationAdminId, "Resource organization admin"));
        db.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Resource boundary tenant",
            Code = $"RB-{Guid.NewGuid():N}"[..12],
            OwnerId = ownerId,
            IsActive = true
        });
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organizationId, UserId = managerId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = developerId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = memberId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = viewerId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = customerId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = organizationAdminId, Role = OrganizationRoleRules.OrganizationAdmin });
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Resource boundary project",
            Code = $"RP-{Guid.NewGuid():N}"[..12],
            OwnerId = ownerId,
            OrganizationId = organizationId,
            Status = "Active"
        });
        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = managerId, Role = ProjectRoleRules.Manager },
            new ProjectMember { ProjectId = projectId, UserId = developerId, Role = ProjectRoleRules.Developer },
            new ProjectMember { ProjectId = projectId, UserId = memberId, Role = ProjectRoleRules.Member },
            new ProjectMember { ProjectId = projectId, UserId = viewerId, Role = ProjectRoleRules.Viewer },
            new ProjectMember { ProjectId = projectId, UserId = customerId, Role = ProjectRoleRules.Customer });
        db.Set<Sprint>().Add(new Sprint
        {
            Id = sprintId,
            ProjectId = projectId,
            Name = "Boundary Sprint",
            StartDate = DateTimeOffset.UtcNow.Date,
            EndDate = DateTimeOffset.UtcNow.Date.AddDays(14)
        });
        db.TaskItems.AddRange(
            new TaskItem
            {
                Id = publicTaskId,
                ProjectId = projectId,
                SprintId = assignTasksToSprint ? sprintId : null,
                ReporterId = ownerId,
                AssigneeId = managerId,
                Title = "Visible assigned task",
                Status = "Todo",
                EstimatedHours = 5
            },
            new TaskItem
            {
                Id = privateTaskId,
                ProjectId = projectId,
                SprintId = assignTasksToSprint ? sprintId : null,
                ReporterId = ownerId,
                AssigneeId = ownerId,
                Title = "Unrelated private task",
                Status = "Todo",
                EstimatedHours = 13,
                IsPrivate = true
            });
        await db.SaveChangesAsync();
        return new SeededData(
            projectId,
            sprintId,
            publicTaskId,
            privateTaskId,
            managerId,
            developerId,
            memberId,
            viewerId,
            customerId,
            organizationAdminId);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync<T>(
        HttpClient client,
        string requestUri,
        T payload)
        => await SendWithCsrfAsync(client, HttpMethod.Post, requestUri, payload);

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpClient client,
        HttpMethod method,
        string requestUri,
        T payload)
    {
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static User User(Guid id, string name) => new()
    {
        Id = id,
        FullName = name,
        Email = $"{id:N}@qaly.test",
        PasswordHash = "not-used",
        Role = "Member",
        IsActive = true
    };

    private static async Task<T> ReadResultAsync<T>(HttpResponseMessage response)
    {
        var envelope = (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions))!;
        envelope.IsSuccess.Should().BeTrue(envelope.Error);
        envelope.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    private sealed record SeededData(
        Guid ProjectId,
        Guid SprintId,
        Guid PublicTaskId,
        Guid PrivateTaskId,
        Guid ManagerId,
        Guid DeveloperId,
        Guid MemberId,
        Guid ViewerId,
        Guid CustomerId,
        Guid OrganizationAdminId);
    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
