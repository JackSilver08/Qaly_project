using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.Services.GitHub;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed record GitHubProjectPullRequest(Guid Id, int Number, string Title, string State, bool IsDraft,
    string? AuthorLogin, string HeadBranch, string BaseBranch, int ReviewCount, int ApprovalCount,
    DateTimeOffset? UpdatedAt, string Url, string Repository);
public sealed record GitHubProjectWorkflow(long RunId, string Name, string? Title, string Branch, string Status,
    string? Conclusion, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string Url, string Repository);
public sealed record GitHubProjectRelease(string TagName, string? Name, bool IsPrerelease,
    DateTimeOffset? PublishedAt, string Url, string Repository);
public sealed record GitHubProjectManagement(int RepositoryCount, int OpenPullRequests, int WaitingForReview,
    int FailedWorkflows, DateTimeOffset? LastSyncedAt, IReadOnlyList<GitHubProjectPullRequest> PullRequests,
    IReadOnlyList<GitHubProjectWorkflow> Workflows, IReadOnlyList<GitHubProjectRelease> Releases);

public interface IGitHubProjectManagementService
{
    Task<Result<GitHubProjectManagement>> GetAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<GitHubProjectManagement>> SyncAsync(Guid projectId, CancellationToken ct = default);
}

public sealed class GitHubProjectManagementService : IGitHubProjectManagementService
{
    private readonly QalyDbContext _db;
    private readonly IGitHubAccessGuard _guard;
    private readonly IGitHubAppClient _client;

    public GitHubProjectManagementService(QalyDbContext db, IGitHubAccessGuard guard, IGitHubAppClient client)
    { _db = db; _guard = guard; _client = client; }

    public async Task<Result<GitHubProjectManagement>> GetAsync(Guid projectId, CancellationToken ct = default)
    {
        var auth = await _guard.AuthorizeProjectAsync(projectId, false, ct);
        if (!auth.IsSuccess) return Result.Failure<GitHubProjectManagement>(auth.Error!, auth.StatusCode);
        return Result.Success(await BuildAsync(projectId, auth.Data!.OrganizationId, ct));
    }

    public async Task<Result<GitHubProjectManagement>> SyncAsync(Guid projectId, CancellationToken ct = default)
    {
        var auth = await _guard.AuthorizeProjectAsync(projectId, true, ct);
        if (!auth.IsSuccess) return Result.Failure<GitHubProjectManagement>(auth.Error!, auth.StatusCode);
        var organizationId = auth.Data!.OrganizationId;
        var connections = await _db.GitHubRepositoryConnections.Include(x => x.Installation)
            .Where(x => x.ProjectId == projectId && x.OrganizationId == organizationId && x.IsActive && !x.IsDeleted)
            .ToListAsync(ct);

        foreach (var connection in connections)
        {
            var installationId = connection.Installation.InstallationId;
            var pullRequests = await _client.GetPullRequestsAsync(installationId, connection.Owner, connection.Name, ct);
            foreach (var item in pullRequests)
            {
                var entity = await _db.GitHubPullRequests.Include(x => x.Reviews).FirstOrDefaultAsync(x =>
                    x.RepositoryConnectionId == connection.Id && x.Number == item.Number, ct);
                if (entity is null)
                {
                    entity = new GitHubPullRequest { OrganizationId = organizationId, RepositoryConnectionId = connection.Id, Number = item.Number };
                    _db.GitHubPullRequests.Add(entity);
                }
                entity.Title = item.Title; entity.State = item.MergedAt.HasValue ? "Merged" : item.State;
                entity.IsDraft = item.IsDraft; entity.AuthorLogin = item.AuthorLogin; entity.HeadBranch = item.HeadBranch;
                entity.BaseBranch = item.BaseBranch; entity.OpenedAt = item.OpenedAt; entity.GitHubUpdatedAt = item.UpdatedAt;
                entity.MergedAt = item.MergedAt; entity.MergedByLogin = item.MergedByLogin; entity.Url = item.Url;
            }

            foreach (var item in await _client.GetWorkflowRunsAsync(installationId, connection.Owner, connection.Name, ct))
            {
                var entity = await _db.GitHubWorkflowRuns.FirstOrDefaultAsync(x => x.RepositoryConnectionId == connection.Id && x.RunExternalId == item.Id, ct);
                if (entity is null) { entity = new GitHubWorkflowRun { OrganizationId = organizationId, RepositoryConnectionId = connection.Id, RunExternalId = item.Id }; _db.GitHubWorkflowRuns.Add(entity); }
                entity.WorkflowName = item.Name; entity.DisplayTitle = item.Title; entity.Branch = item.Branch;
                entity.CommitSha = item.Sha; entity.Status = item.Status; entity.Conclusion = item.Conclusion;
                entity.StartedAt = item.StartedAt; entity.CompletedAt = item.CompletedAt; entity.Url = item.Url;
            }

            foreach (var item in await _client.GetReleasesAsync(installationId, connection.Owner, connection.Name, ct))
            {
                var entity = await _db.GitHubReleases.FirstOrDefaultAsync(x => x.RepositoryConnectionId == connection.Id && x.ReleaseExternalId == item.Id, ct);
                if (entity is null) { entity = new GitHubRelease { OrganizationId = organizationId, RepositoryConnectionId = connection.Id, ReleaseExternalId = item.Id }; _db.GitHubReleases.Add(entity); }
                entity.TagName = item.TagName; entity.Name = item.Name; entity.IsDraft = item.IsDraft;
                entity.IsPrerelease = item.IsPrerelease; entity.PublishedAt = item.PublishedAt; entity.Url = item.Url;
            }
            connection.LastSyncedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Success(await BuildAsync(projectId, organizationId, ct));
    }

    private async Task<GitHubProjectManagement> BuildAsync(Guid projectId, Guid organizationId, CancellationToken ct)
    {
        var connections = _db.GitHubRepositoryConnections.AsNoTracking().Where(x => x.ProjectId == projectId && x.OrganizationId == organizationId && x.IsActive && !x.IsDeleted);
        var ids = connections.Select(x => x.Id);
        var prs = await _db.GitHubPullRequests.AsNoTracking().Where(x => ids.Contains(x.RepositoryConnectionId))
            .OrderByDescending(x => x.GitHubUpdatedAt ?? x.OpenedAt).Take(50)
            .Select(x => new GitHubProjectPullRequest(x.Id, x.Number, x.Title, x.State, x.IsDraft, x.AuthorLogin,
                x.HeadBranch, x.BaseBranch, x.Reviews.Count, x.Reviews.Count(r => r.State == "Approved"),
                x.GitHubUpdatedAt, x.Url, x.RepositoryConnection.FullName)).ToListAsync(ct);
        var workflows = await _db.GitHubWorkflowRuns.AsNoTracking().Where(x => ids.Contains(x.RepositoryConnectionId))
            .OrderByDescending(x => x.StartedAt).Take(50)
            .Select(x => new GitHubProjectWorkflow(x.RunExternalId, x.WorkflowName, x.DisplayTitle, x.Branch, x.Status,
                x.Conclusion, x.StartedAt, x.CompletedAt, x.Url, x.RepositoryConnection.FullName)).ToListAsync(ct);
        var releases = await _db.GitHubReleases.AsNoTracking().Where(x => ids.Contains(x.RepositoryConnectionId) && !x.IsDraft)
            .OrderByDescending(x => x.PublishedAt).Take(20)
            .Select(x => new GitHubProjectRelease(x.TagName, x.Name, x.IsPrerelease, x.PublishedAt, x.Url, x.RepositoryConnection.FullName)).ToListAsync(ct);
        var syncTimes = await connections.Select(x => x.LastSyncedAt).ToListAsync(ct);
        return new(await connections.CountAsync(ct), prs.Count(x => x.State.Equals("open", StringComparison.OrdinalIgnoreCase)),
            prs.Count(x => x.State.Equals("open", StringComparison.OrdinalIgnoreCase) && x.ApprovalCount == 0 && !x.IsDraft),
            workflows.Count(x => x.Conclusion is "failure" or "timed_out" or "cancelled"),
            syncTimes.Where(x => x.HasValue).Max(), prs, workflows, releases);
    }
}
