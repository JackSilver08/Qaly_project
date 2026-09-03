using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class VoteBoundaryIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public VoteBoundaryIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "TEST-RBAC-VOTE-READONLY-DENY-01")]
    public async Task Vote_ReadOnlyRolesAreDenied_WhileWritableMemberCanVote()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.Users.AddRange(
                User(ownerId, "Vote owner"),
                User(memberId, "Vote member"),
                User(viewerId, "Vote viewer"),
                User(customerId, "Vote customer"));
            db.Projects.Add(new Project { Id = projectId, Name = "Vote boundary", Code = $"VOT-{Guid.NewGuid():N}", OwnerId = ownerId });
            db.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = projectId, UserId = memberId, Role = "Member" },
                new ProjectMember { ProjectId = projectId, UserId = viewerId, Role = "Viewer" },
                new ProjectMember { ProjectId = projectId, UserId = customerId, Role = "Customer" });
            db.TaskItems.Add(new TaskItem { Id = taskId, ProjectId = projectId, ReporterId = ownerId, Title = "Vote target" });
            await db.SaveChangesAsync();
        }

        using var memberResponse = await VoteAsync(memberId, taskId);
        using var viewerResponse = await VoteAsync(viewerId, taskId);
        using var customerResponse = await VoteAsync(customerId, taskId);

        memberResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        viewerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        verifyDb.Votes.Should().ContainSingle(vote => vote.UserId == memberId && vote.TargetId == taskId);
    }

    private async Task<HttpResponseMessage> VoteAsync(Guid userId, Guid taskId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/votes/task/{taskId:D}")
        {
            Content = JsonContent.Create(new { value = 1 })
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        var response = await client.SendAsync(request);
        client.Dispose();
        return response;
    }

    private static User User(Guid id, string name)
        => new()
        {
            Id = id,
            FullName = name,
            Email = $"{id:N}@vote.qaly.test",
            PasswordHash = "integration-only",
            Role = "Member",
            IsActive = true
        };

    private sealed record CsrfResponse(string Token);
}
