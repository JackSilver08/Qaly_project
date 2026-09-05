using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class TaskReviewWorkflowApiTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Theory]
    [InlineData("status", "InProgress", "Done", 400)]
    [InlineData("kanban", "InProgress", "Done", 400)]
    [InlineData("batch", "InProgress", "Done", 400)]
    [InlineData("edit", "InProgress", "Done", 400)]
    [InlineData("status", "InReview", "Done", 403)]
    [InlineData("kanban", "InReview", "Done", 403)]
    [InlineData("batch", "InReview", "Done", 403)]
    [InlineData("edit", "InReview", "Done", 403)]
    [InlineData("status", "Done", "InReview", 400)]
    [InlineData("kanban", "Done", "InReview", 400)]
    [InlineData("batch", "Done", "InReview", 400)]
    [InlineData("edit", "Done", "InReview", 400)]
    public async Task Assignee_CannotBypassReviewOrReopen_ThroughAnyMutationPath(string path, string oldStatus, string newStatus, int expected)
    {
        var data = await SeedAsync(oldStatus);
        using var client = await ClientAsync(data.MemberId);
        using var response = await TransitionAsync(client, data, path, oldStatus, newStatus);
        ((int)response.StatusCode).Should().Be(expected, await response.Content.ReadAsStringAsync());
        await AssertStatusAsync(client, data.TaskId, oldStatus);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.WebhookOutboxMessages.CountAsync(e => e.ProjectId == data.ProjectId)).Should().Be(0);
    }

    [Fact]
    public async Task Submit_ReviewerReturn_Resubmit_Approve_ReadBack_AndLockCompletedTask()
    {
        var data = await SeedAsync("InProgress");
        using var member = await ClientAsync(data.MemberId);
        using var reviewer = await ClientAsync(data.ReviewerId);
        using var manager = await ClientAsync(data.OwnerId);
        (await TransitionAsync(member, data, "kanban", "InProgress", "InReview")).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertStatusAsync(member, data.TaskId, "InReview");
        (await TransitionAsync(reviewer, data, "status", "InReview", "InProgress")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await TransitionAsync(member, data, "status", "InProgress", "InReview")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await TransitionAsync(reviewer, data, "kanban", "InReview", "Done")).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertStatusAsync(member, data.TaskId, "Done");
        foreach (var target in new[] { "Todo", "InProgress", "OnHold", "InReview", "Cancelled" })
            (await TransitionAsync(manager, data, "status", "Done", target)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertStatusAsync(manager, data.TaskId, "Done");
    }

    [Fact]
    public async Task ReviewDisabled_DoesNotGiveMemberCompletionAuthority()
    {
        var data = await SeedAsync("InProgress", enableReview: false);
        using var member = await ClientAsync(data.MemberId);
        using var manager = await ClientAsync(data.OwnerId);
        (await TransitionAsync(member, data, "status", "InProgress", "Done")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await TransitionAsync(member, data, "status", "InProgress", "InReview")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TransitionAsync(manager, data, "status", "InProgress", "Done")).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertStatusAsync(member, data.TaskId, "Done");
    }

    [Fact]
    public async Task ManagerStillCannotSkipEnabledReview_AndEvidenceRequirementIsNotBypassed()
    {
        var data = await SeedAsync("InProgress", requireEvidence: true);
        using var manager = await ClientAsync(data.OwnerId);
        using var reviewer = await ClientAsync(data.ReviewerId);
        (await TransitionAsync(manager, data, "status", "InProgress", "Done")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TransitionAsync(manager, data, "status", "InProgress", "InReview")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var response = await TransitionAsync(reviewer, data, "status", "InReview", "Done");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("minh chứng");
        await AssertStatusAsync(manager, data.TaskId, "InReview");
    }

    [Fact]
    public async Task ApprovingFinalEvidence_AutomaticallyCompletesTask_ThroughCanonicalWorkflow()
    {
        var data = await SeedAsync("InReview", requireEvidence: true);
        var attachmentId = Guid.NewGuid();
        var physicalFileId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.PhysicalFiles.Add(new PhysicalFile
            {
                Id = physicalFileId,
                ContentHash = Guid.NewGuid().ToString("N"),
                FilePath = $"/workflow/{attachmentId:N}.png",
                FileSize = 1024
            });
            db.TaskAttachments.Add(new TaskAttachment
            {
                Id = attachmentId,
                TaskItemId = data.TaskId,
                UploadedById = data.MemberId,
                PhysicalFileId = physicalFileId,
                FileName = "acceptance-evidence.png",
                ContentType = "image/png",
                IsEvidence = true,
                EvidenceApprovalStatus = "Pending"
            });
            await db.SaveChangesAsync();
        }

        using var reviewer = await ClientAsync(data.ReviewerId);
        using var response = await reviewer.PostAsJsonAsync(
            $"/api/attachments/{attachmentId}/evidence/review",
            new { approve = true, reviewNote = "Đạt tiêu chí nghiệm thu." });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await AssertStatusAsync(reviewer, data.TaskId, "Done");

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var evidence = await verificationDb.TaskAttachments.AsNoTracking().SingleAsync(item => item.Id == attachmentId);
        evidence.EvidenceApprovalStatus.Should().Be("Approved");
        (await verificationDb.WebhookOutboxMessages.CountAsync(item => item.ProjectId == data.ProjectId))
            .Should().BeGreaterThan(0, "auto-completion must use the same audited integration path as a manual status change");
    }

    [Fact]
    public async Task BatchWithOneUnreviewedTask_IsAtomic()
    {
        var data = await SeedAsync("InReview");
        var otherId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.TaskItems.Add(new TaskItem { Id = otherId, ProjectId = data.ProjectId, ReporterId = data.OwnerId, Title = "Not submitted", Status = "InProgress" });
            await db.SaveChangesAsync();
        }
        using var manager = await ClientAsync(data.OwnerId);
        using var response = await manager.PostAsJsonAsync("/api/tasks/batch-status", new { ids = new[] { data.TaskId, otherId }, status = "Done" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertStatusAsync(manager, data.TaskId, "InReview");
        await AssertStatusAsync(manager, otherId, "InProgress");
    }

    [Theory]
    [InlineData("Member", false, false, false)]
    [InlineData("Reviewer", true, false, false)]
    [InlineData("Reviewer", false, true, false)]
    [InlineData("Reviewer", false, false, true)]
    [InlineData("Member", false, false, true)]
    [InlineData("Viewer", false, false, true)]
    public async Task CompletionAuthority_RespectsRoleSelfReviewRemovalAndDelegation(string role, bool selfAssigned, bool removed, bool delegated)
    {
        var data = await SeedAsync("InReview");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var membership = await db.ProjectMembers.SingleAsync(m => m.ProjectId == data.ProjectId && m.UserId == data.ReviewerId);
            membership.Role = role;
            if (removed) db.ProjectMembers.Remove(membership);
            var task = await db.TaskItems.FindAsync(data.TaskId);
            if (selfAssigned) task!.AssigneeId = data.ReviewerId;
            if (delegated) task!.ReviewerId = data.ReviewerId;
            await db.SaveChangesAsync();
        }
        using var reviewer = await ClientAsync(data.ReviewerId);
        using var response = await TransitionAsync(reviewer, data, "status", "InReview", "Done");
        var allowed = !selfAssigned && !removed && role != "Viewer" && (role == "Reviewer" || delegated);
        response.StatusCode.Should().Be(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        using var manager = await ClientAsync(data.OwnerId);
        await AssertStatusAsync(manager, data.TaskId, allowed ? "Done" : "InReview");
    }

    private async Task<Seed> SeedAsync(string status, bool enableReview = true, bool requireEvidence = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var data = new Seed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        foreach (var id in new[] { data.OwnerId, data.MemberId, data.ReviewerId })
            db.Users.Add(new User { Id = id, Email = $"workflow-{id:N}@qaly.test", FullName = "Workflow tester", IsActive = true, PasswordHash = "test" });
        db.Projects.Add(new Project { Id = data.ProjectId, OwnerId = data.OwnerId, Name = "Review workflow", Code = $"R{data.ProjectId:N}"[..12], EnableInReview = enableReview, RequireEvidenceToDone = requireEvidence });
        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = data.ProjectId, UserId = data.MemberId, Role = "Member" },
            new ProjectMember { ProjectId = data.ProjectId, UserId = data.ReviewerId, Role = "Reviewer" });
        db.TaskItems.Add(new TaskItem { Id = data.TaskId, ProjectId = data.ProjectId, ReporterId = data.OwnerId, AssigneeId = data.MemberId, Title = "Review workflow task", Status = status });
        await db.SaveChangesAsync();
        return data;
    }

    private async Task<HttpClient> ClientAsync(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        var csrf = await client.GetFromJsonAsync<Csrf>("/api/security/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.Token);
        return client;
    }

    private static Task<HttpResponseMessage> TransitionAsync(HttpClient client, Seed data, string path, string oldStatus, string newStatus) => path switch
    {
        "kanban" => client.PatchAsJsonAsync($"/api/tasks/project/{data.ProjectId}/kanban/move", new { taskId = data.TaskId, fromStatus = oldStatus, toStatus = newStatus }),
        "batch" => client.PostAsJsonAsync("/api/tasks/batch-status", new { ids = new[] { data.TaskId }, status = newStatus }),
        "edit" => client.PutAsJsonAsync($"/api/tasks/{data.TaskId}", new UpdateTaskDto("Review workflow task", null, newStatus, "Medium", null, null, null, data.MemberId, false)),
        _ => client.PatchAsJsonAsync($"/api/tasks/{data.TaskId}/status", new { status = newStatus })
    };

    private static async Task AssertStatusAsync(HttpClient client, Guid taskId, string expected)
    {
        var readBack = await client.GetFromJsonAsync<Envelope<TaskItemDto>>($"/api/tasks/{taskId}");
        readBack!.Data!.Status.Should().Be(expected);
    }

    private sealed record Seed(Guid ProjectId, Guid TaskId, Guid OwnerId, Guid MemberId, Guid ReviewerId);
    private sealed record Csrf(string Token);
    private sealed record Envelope<T>(T? Data);
}
