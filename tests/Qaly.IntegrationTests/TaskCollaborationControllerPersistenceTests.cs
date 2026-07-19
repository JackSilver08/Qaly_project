using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Attachment;
using Qaly.Application.DTOs.Comment;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class TaskCollaborationControllerPersistenceTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public TaskCollaborationControllerPersistenceTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Allow_CommentAttachmentAndNotification_ArePersistedAndReadable()
    {
        var seed = await SeedWorkspaceAsync();
        using var ownerClient = ClientFor(seed.OwnerId);
        using var assigneeClient = ClientFor(seed.AssigneeId);

        var commentResponse = await ownerClient.PostAsJsonAsync("/api/comments", new
        {
            taskItemId = seed.TaskId,
            content = "T1-TR-02 persisted comment",
            mentionedUserIds = new[] { seed.AssigneeId }
        });

        commentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = (await commentResponse.Content.ReadFromJsonAsync<ApiResult<CommentDto>>())!.Data!;
        comment.Content.Should().Be("T1-TR-02 persisted comment");

        using var attachmentContent = new MultipartFormDataContent();
        attachmentContent.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("T1-TR-02 attachment body")),
            "file",
            "t1-tr-02-evidence.txt");

        var attachmentResponse = await ownerClient.PostAsync($"/api/attachments/task/{seed.TaskId}", attachmentContent);
        attachmentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = (await attachmentResponse.Content.ReadFromJsonAsync<ApiResult<TaskAttachmentDto>>())!.Data!;
        attachment.FileName.Should().Be("t1-tr-02-evidence.txt");

        var commentsReadBack = (await (await ownerClient.GetAsync($"/api/comments/task/{seed.TaskId}"))
            .Content.ReadFromJsonAsync<ApiResult<IReadOnlyList<CommentDto>>>())!.Data!;
        commentsReadBack.Should().Contain(item => item.Id == comment.Id && item.Content == comment.Content);

        var attachmentsReadBack = (await (await ownerClient.GetAsync($"/api/attachments/task/{seed.TaskId}"))
            .Content.ReadFromJsonAsync<ApiResult<IReadOnlyList<TaskAttachmentDto>>>())!.Data!;
        attachmentsReadBack.Should().Contain(item => item.Id == attachment.Id && item.FileName == attachment.FileName);

        var notificationsReadBack = (await (await assigneeClient.GetAsync("/api/notifications"))
            .Content.ReadFromJsonAsync<ApiResult<IReadOnlyList<NotificationDto>>>())!.Data!;
        notificationsReadBack.Should().Contain(item =>
            item.RelatedEntityId == seed.TaskId &&
            (item.Type == "Mentioned" || item.Type == "CommentAdded"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var storedComment = await db.TaskComments.AsNoTracking().SingleAsync(item => item.Id == comment.Id);
        var storedAttachment = await db.TaskAttachments
            .AsNoTracking()
            .Include(item => item.PhysicalFile)
            .SingleAsync(item => item.Id == attachment.Id);
        var storedNotifications = await db.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == seed.AssigneeId && item.RelatedEntityId == seed.TaskId)
            .ToListAsync();

        storedComment.Content.Should().Be(comment.Content);
        storedAttachment.PhysicalFile.FileSize.Should().BeGreaterThan(0);
        File.Exists(storedAttachment.PhysicalFile.FilePath).Should().BeTrue("attachment content must be written to storage");
        storedNotifications.Should().Contain(item => item.Message.Contains(seed.TaskTitle, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deny_UserWithoutTaskAccess_CannotUploadAttachment()
    {
        var seed = await SeedWorkspaceAsync();
        using var forbiddenClient = ClientFor(Guid.Parse("c0000000-0000-0000-0000-000000000403"));

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("forbidden")), "file", "forbidden.txt");

        var response = await forbiddenClient.PostAsync($"/api/attachments/task/{seed.TaskId}", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Error_InvalidPayload_ReturnsBadRequest()
    {
        var seed = await SeedWorkspaceAsync();
        using var ownerClient = ClientFor(seed.OwnerId);

        var badComment = await ownerClient.PostAsJsonAsync("/api/comments", new
        {
            taskItemId = seed.TaskId,
            content = ""
        });
        badComment.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var emptyAttachment = new MultipartFormDataContent();
        var badAttachment = await ownerClient.PostAsync($"/api/attachments/task/{seed.TaskId}", emptyAttachment);
        badAttachment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private HttpClient ClientFor(Guid userId, string role = "User")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private async Task<SeededWorkspace> SeedWorkspaceAsync()
    {
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var taskTitle = $"T1-TR-02 Task {Guid.NewGuid():N}";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        db.Users.AddRange(
            new User
            {
                Id = ownerId,
                FullName = "T1 Owner",
                Email = $"{ownerId:N}@qaly.test",
                Role = "Member",
                IsActive = true
            },
            new User
            {
                Id = assigneeId,
                FullName = "T1 Assignee",
                Email = $"{assigneeId:N}@qaly.test",
                Role = "Member",
                IsActive = true
            });

        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "T1-TR-02 Integration Project",
            Code = $"T1{Guid.NewGuid():N}"[..10],
            OwnerId = ownerId
        });

        db.ProjectMembers.AddRange(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = ownerId,
                Role = "Owner",
                CanViewProjectTimeline = true,
                CanViewTaskRisk = true,
                CanNudgeAssignee = true,
                CanViewUnseenTaskSignal = true
            },
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = assigneeId,
                Role = "Member"
            });

        db.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            AssigneeId = assigneeId,
            Title = taskTitle,
            Description = "Seeded task for controller persistence tests",
            Priority = "High",
            Status = "Todo",
            IsPrivate = true
        });

        await db.SaveChangesAsync();
        return new SeededWorkspace(ownerId, assigneeId, projectId, taskId, taskTitle);
    }

    private sealed record SeededWorkspace(Guid OwnerId, Guid AssigneeId, Guid ProjectId, Guid TaskId, string TaskTitle);
    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);
}
#pragma warning restore CA1707
