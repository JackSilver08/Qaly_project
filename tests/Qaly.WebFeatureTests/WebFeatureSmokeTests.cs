using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Project;
using Qaly.Application.DTOs.Task;
using Qaly.Application.DTOs.Wiki;
using Qaly.Domain.Entities;
using Qaly.Domain.Enums;
using Qaly.Infrastructure.Data;

namespace Qaly.WebFeatureTests;

[TestFixture]
public sealed class WebFeatureSmokeTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        _client.Dispose();
        _factory.Dispose();
    }

    [TestCase("/images/groups/qaly-product.png")]
    [TestCase("/images/groups/nova-retail.png")]
    [TestCase("/images/demo/qaly-product-board.jpg")]
    [TestCase("/images/demo/nova-retail-board.jpg")]
    public async Task Demo_static_assets_load_without_404(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().StartWith("image/");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Api_features_require_authentication()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        request.Headers.Add("X-Test-Auth", "None");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public void In_memory_health_check_does_not_register_external_sql_or_redis()
    {
        var options = _factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>()
            .Value;

        options.Registrations.Select(registration => registration.Name)
            .Should().BeEquivalentTo("vector_outbox");
    }

    [Test]
    public async Task Project_feature_can_create_list_and_read_project()
    {
        await EnsureUserExistsAsync(_factory.TestUserId, "Feature Owner", "owner@qaly.test");
        var create = new CreateProjectDto(
            "Feature Smoke Project",
            $"feature-{Guid.NewGuid():N}",
            "Project created by the NUnit web feature smoke suite.",
            "/images/projects/qaly-workos-demo.png",
            DateTimeOffset.UtcNow.Date,
            DateTimeOffset.UtcNow.Date.AddDays(21));

        var createResponse = await _client.PostAsJsonAsync("/api/projects", create, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadApiResultAsync<ProjectDto>(createResponse);
        created.IsSuccess.Should().BeTrue(created.Error);
        created.Data.Should().NotBeNull();

        var listResponse = await _client.GetAsync("/api/projects?page=1&pageSize=10");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await ReadApiResultAsync<PagedResult<ProjectDto>>(listResponse);
        list.IsSuccess.Should().BeTrue(list.Error);
        list.Data!.Items.Should().Contain(project => project.Id == created.Data!.Id);

        var detailResponse = await _client.GetAsync($"/api/projects/{created.Data!.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadApiResultAsync<ProjectDto>(detailResponse);
        detail.IsSuccess.Should().BeTrue(detail.Error);
        detail.Data!.Name.Should().Be(create.Name);
    }

    [Test]
    public async Task Task_wiki_dashboard_analytics_and_erumi_features_load_from_live_database()
    {
        var seed = await SeedProjectWorkspaceAsync();

        var taskListResponse = await _client.GetAsync($"/api/tasks/project/{seed.ProjectId}?page=1&pageSize=20");
        taskListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskList = await ReadApiResultAsync<PagedResult<TaskItemDto>>(taskListResponse);
        taskList.IsSuccess.Should().BeTrue(taskList.Error);
        taskList.Data!.Items.Should().HaveCount(3);

        var kanbanResponse = await _client.GetAsync($"/api/tasks/project/{seed.ProjectId}/kanban");
        kanbanResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var kanban = await ReadApiResultAsync<KanbanBoardDto>(kanbanResponse);
        kanban.IsSuccess.Should().BeTrue(kanban.Error);
        kanban.Data!.Columns.SelectMany(column => column.Tasks).Should().Contain(task => task.Id == seed.OverdueTaskId);

        var wikiResponse = await _client.GetAsync($"/api/projects/{seed.ProjectId}/wiki");
        wikiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wiki = await ReadApiResultAsync<List<WikiPageDto>>(wikiResponse);
        wiki.IsSuccess.Should().BeTrue(wiki.Error);
        wiki.Data!.Should().Contain(page => page.Title == "Demo runbook");

        var dashboardResponse = await _client.GetAsync($"/api/dashboard/v2/projects/{seed.ProjectId}/summary");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await ReadApiResultAsync<ProjectDashboardSummaryResponse>(dashboardResponse);
        dashboard.IsSuccess.Should().BeTrue(dashboard.Error);
        dashboard.Data!.Metrics.TotalTasks.Should().Be(3);
        dashboard.Data.Metrics.OverdueTasks.Should().Be(1);

        var analyticsResponse = await _client.GetAsync($"/api/analytics/projects/{seed.ProjectId}");
        analyticsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var analytics = await ReadApiResultAsync<ProjectAnalyticsResponse>(analyticsResponse);
        analytics.IsSuccess.Should().BeTrue(analytics.Error);
        analytics.Data!.TotalTasks.Should().Be(3);

        var erumiResponse = await _client.PostAsJsonAsync(
            "/api/ai/chat/fast",
            new ErumiChatRequestDto("Tom tat ngan gon, khong can bieu do", seed.ProjectId),
            JsonOptions);
        erumiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var erumi = await erumiResponse.Content.ReadFromJsonAsync<ErumiChatResponseDto>(JsonOptions);
        erumi.Should().NotBeNull();
        erumi!.UsedAi.Should().BeFalse();
        erumi.Tables.Should().BeEmpty();
        erumi.Charts.Should().BeEmpty();
    }

    [Test]
    public async Task Group_feature_loads_members_invitations_messages_and_shell_route()
    {
        var seed = await SeedGroupWorkspaceAsync();

        var groupsResponse = await _client.GetAsync("/api/groups?page=1&pageSize=20");
        groupsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = await ReadApiResultAsync<PagedResult<GroupDto>>(groupsResponse);
        groups.IsSuccess.Should().BeTrue(groups.Error);
        groups.Data!.Items.Should().Contain(group => group.Id == seed.GroupId);

        var detailResponse = await _client.GetAsync($"/api/groups/{seed.GroupId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadApiResultAsync<GroupDto>(detailResponse);
        detail.IsSuccess.Should().BeTrue(detail.Error);
        detail.Data!.MemberCount.Should().Be(2);

        var membersResponse = await _client.GetAsync($"/api/groups/{seed.GroupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var members = await ReadApiResultAsync<List<GroupMemberDto>>(membersResponse);
        members.IsSuccess.Should().BeTrue(members.Error);
        members.Data!.Should().Contain(member => member.UserId == _factory.TestUserId);

        var invitationsResponse = await _client.GetAsync($"/api/groups/{seed.GroupId}/invitations?status=Pending");
        invitationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var invitations = await ReadApiResultAsync<List<GroupInvitationDto>>(invitationsResponse);
        invitations.IsSuccess.Should().BeTrue(invitations.Error);
        invitations.Data!.Should().Contain(invitation => invitation.Email == "customer@example.com");

        var messagesResponse = await _client.GetAsync($"/api/groups/{seed.GroupId}/messages?page=1&pageSize=50");
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await ReadApiResultAsync<PagedResult<GroupMessageDto>>(messagesResponse);
        messages.IsSuccess.Should().BeTrue(messages.Error);
        messages.Data!.Items.Should().Contain(message => message.Content.Contains("kickoff", StringComparison.OrdinalIgnoreCase));
        messages.Data.Items.SelectMany(message => message.Reactions).Should().BeEmpty();

        var shellResponse = await _client.GetAsync($"/groups/{seed.GroupId}");
        shellResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        shellResponse.Content.Headers.ContentType?.MediaType.Should().Contain("html");
    }

    [Test]
    public async Task Users_feature_returns_active_users_for_mentions_and_assignment_controls()
    {
        await EnsureUserExistsAsync(_factory.TestUserId, "Feature Owner", "owner@qaly.test");
        await EnsureUserExistsAsync(Guid.NewGuid(), "Active Team Member", "member@qaly.test");

        var response = await _client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await ReadApiResultAsync<List<UserResult>>(response);
        users.IsSuccess.Should().BeTrue(users.Error);
        users.Data!.Should().Contain(user => user.Email == "owner@qaly.test");
        users.Data.Should().Contain(user => user.Email == "member@qaly.test");
    }

    private async Task EnsureUserExistsAsync(Guid userId, string fullName, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (await db.Users.AnyAsync(user => user.Id == userId))
        {
            return;
        }

        db.Users.Add(new User
        {
            Id = userId,
            FullName = fullName,
            Email = email,
            Role = "Member",
            IsActive = true,
            AvatarUrl = "/images/avatars/demo-admin.png"
        });

        await db.SaveChangesAsync();
    }

    private async Task<SeededProject> SeedProjectWorkspaceAsync()
    {
        var ownerId = _factory.TestUserId;
        await EnsureUserExistsAsync(ownerId, "Feature Owner", "owner@qaly.test");

        var projectId = Guid.NewGuid();
        var overdueTaskId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "NUnit Feature Project",
            Code = $"nunit-feature-{Guid.NewGuid():N}",
            Description = "Seeded feature smoke project.",
            LogoUrl = "/images/projects/qaly-workos-demo.png",
            OwnerId = ownerId,
            StartDate = now.Date,
            EndDate = now.Date.AddDays(30)
        });

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = ownerId,
            Role = "Owner",
            CanViewProjectTimeline = true,
            CanViewTaskRisk = true,
            CanNudgeAssignee = true,
            CanViewUnseenTaskSignal = true
        });

        db.TaskItems.AddRange(
            new TaskItem
            {
                Id = overdueTaskId,
                ProjectId = projectId,
                ReporterId = ownerId,
                AssigneeId = ownerId,
                Title = "Resolve demo load regression",
                Description = "Keep the group page stable before the customer demo.",
                Status = "InProgress",
                Priority = "High",
                StartDate = now.AddDays(-4),
                DueDate = now.AddDays(-1),
                EstimatedHours = 6,
                SortOrder = 1
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ReporterId = ownerId,
                AssigneeId = ownerId,
                Title = "Prepare demo checklist",
                Status = "Todo",
                Priority = "Medium",
                DueDate = now.AddDays(2),
                EstimatedHours = 3,
                SortOrder = 2
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ReporterId = ownerId,
                AssigneeId = ownerId,
                Title = "Validate seed dataset",
                Status = "Done",
                Priority = "Low",
                DueDate = now.AddDays(1),
                EstimatedHours = 2,
                ActualHours = 2,
                UpdatedAt = now,
                SortOrder = 3
            });

        db.WikiPages.Add(new WikiPage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AuthorId = ownerId,
            Title = "Demo runbook",
            Content = "Open groups, review messages, inspect dashboard, then ask Erumi for a short summary.",
            Visibility = "internal",
            UpdatedAt = now
        });

        await db.SaveChangesAsync();
        return new SeededProject(projectId, overdueTaskId);
    }

    private async Task<SeededGroup> SeedGroupWorkspaceAsync()
    {
        var ownerId = _factory.TestUserId;
        var memberId = Guid.NewGuid();
        await EnsureUserExistsAsync(ownerId, "Feature Owner", "owner@qaly.test");
        await EnsureUserExistsAsync(memberId, "Demo Member", "demo.member@qaly.test");

        var groupId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        db.WorkGroups.Add(new WorkGroup
        {
            Id = groupId,
            Name = "NUnit Demo War Room",
            AvatarUrl = "/images/groups/nova-retail.png",
            BackgroundImageUrl = "/images/demo/nova-retail-board.jpg",
            BackgroundTheme = "light",
            Color = "#2563EB",
            OwnerId = ownerId,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now
        });

        db.WorkGroupMembers.AddRange(
            new WorkGroupMember
            {
                WorkGroupId = groupId,
                UserId = ownerId,
                Role = "Owner",
                JoinedAt = now.AddDays(-2)
            },
            new WorkGroupMember
            {
                WorkGroupId = groupId,
                UserId = memberId,
                Role = "Member",
                JoinedAt = now.AddDays(-1)
            });

        db.GroupInvitations.Add(new GroupInvitation
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Email = "customer@example.com",
            Token = $"invite-{Guid.NewGuid():N}",
            Status = GroupInvitationStatus.Pending,
            CreatedAt = now,
            ExpiredAt = now.AddDays(7)
        });

        db.GroupMessages.AddRange(
            new GroupMessage
            {
                Id = Guid.NewGuid(),
                WorkGroupId = groupId,
                UserId = ownerId,
                Content = "Demo kickoff message with a legacy reaction payload.",
                MessageType = "Text",
                ReactionSummaryJson = "[{\"emoji\":\"thumbs_up\",\"count\":2}]",
                CreatedAt = now.AddMinutes(-30)
            },
            new GroupMessage
            {
                Id = Guid.NewGuid(),
                WorkGroupId = groupId,
                UserId = memberId,
                Content = "Checklist is ready for customer review.",
                MessageType = "Text",
                ReactionSummaryJson = "[]",
                CreatedAt = now.AddMinutes(-10)
            });

        await db.SaveChangesAsync();
        return new SeededGroup(groupId);
    }

    private static async Task<ApiResult<T>> ReadApiResultAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResult<T>>(body, JsonOptions);
        result.Should().NotBeNull(body);
        return result!;
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);

    private sealed record SeededProject(Guid ProjectId, Guid OverdueTaskId);

    private sealed record SeededGroup(Guid GroupId);

    private sealed record UserResult(Guid Id, string FullName, string Email, string Role, bool IsActive, string? AvatarUrl);

    private sealed record ProjectDashboardSummaryResponse(ProjectDashboardMetricsResponse Metrics);

    private sealed record ProjectDashboardMetricsResponse(
        int TotalTasks,
        int OpenTasks,
        int BacklogTasks,
        int InProgressTasks,
        int DoneTasks,
        int CancelledTasks,
        decimal CompletionRate,
        int OverdueTasks,
        int DueSoon24h);

    private sealed record ProjectAnalyticsResponse(
        int TotalTasks,
        int DoneTasks,
        int InProgressTasks,
        int OverdueTasks,
        double TotalEstimatedHours,
        double TotalActualHours);
}
