using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.GitHub;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services.GitHub;

public sealed class TaskDevelopmentService : ITaskDevelopmentService
{
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<TaskDevelopmentLink> _links;
    private readonly IRepository<GitHubRepositoryConnection> _repositories;
    private readonly IRepository<GitHubCommit> _commits;
    private readonly IRepository<GitHubPullRequest> _pullRequests;
    private readonly IRepository<GitHubRelease> _releases;
    private readonly IRepository<GitHubWorkflowRun> _workflowRuns;
    private readonly IGitHubAccessGuard _guard;

    public TaskDevelopmentService(IRepository<TaskItem> tasks, IRepository<TaskDevelopmentLink> links,
        IRepository<GitHubRepositoryConnection> repositories, IRepository<GitHubCommit> commits,
        IRepository<GitHubPullRequest> pullRequests, IRepository<GitHubRelease> releases,
        IRepository<GitHubWorkflowRun> workflowRuns,
        IGitHubAccessGuard guard)
    {
        _tasks = tasks;
        _links = links;
        _repositories = repositories;
        _commits = commits;
        _pullRequests = pullRequests;
        _releases = releases;
        _workflowRuns = workflowRuns;
        _guard = guard;
    }

    public async Task<Result<TaskDevelopmentDto>> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _tasks.GetQueryable().AsNoTracking()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null) return Result.NotFound<TaskDevelopmentDto>("Không tìm thấy task.");

        var auth = await _guard.AuthorizeProjectAsync(task.ProjectId, requireManage: false, ct);
        if (!auth.IsSuccess) return Result.Failure<TaskDevelopmentDto>(auth.Error!, auth.StatusCode);
        var organizationId = auth.Data!.OrganizationId;

        var links = await _links.GetQueryable().AsNoTracking()
            .Where(x => x.TaskId == taskId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var repositories = await _repositories.GetQueryable().AsNoTracking()
            .Where(x => x.ProjectId == task.ProjectId && x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.RepositoryExternalId, ct);
        var taskKey = task.Key ?? string.Empty;
        var currentTaskReference = new GitHubTaskReferenceDto(task.Id, taskKey);

        var commitKeys = links.Where(x => x.EntityType == "Commit").Select(x => x.ExternalEntityId).ToHashSet();
        var prKeys = links.Where(x => x.EntityType == "PullRequest").Select(x => x.ExternalEntityId).ToHashSet();
        var releaseKeys = links.Where(x => x.EntityType == "Release").Select(x => x.ExternalEntityId).ToHashSet();
        var workflowKeys = links.Where(x => x.EntityType == "WorkflowRun").Select(x => x.ExternalEntityId).ToHashSet();

        var commits = await _commits.GetQueryable().AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && repositories.Keys.Contains(x.RepositoryConnection.RepositoryExternalId))
            .OrderByDescending(x => x.CommittedAt).ToListAsync(ct);
        var prs = await _pullRequests.GetQueryable().AsNoTracking().Include(x => x.Reviews)
            .Where(x => x.OrganizationId == organizationId && repositories.Keys.Contains(x.RepositoryConnection.RepositoryExternalId))
            .OrderByDescending(x => x.OpenedAt).ToListAsync(ct);
        var releases = await _releases.GetQueryable().AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && repositories.Keys.Contains(x.RepositoryConnection.RepositoryExternalId))
            .OrderByDescending(x => x.PublishedAt).ToListAsync(ct);
        var workflows = await _workflowRuns.GetQueryable().AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && repositories.Keys.Contains(x.RepositoryConnection.RepositoryExternalId))
            .OrderByDescending(x => x.StartedAt).ToListAsync(ct);

        var dto = new TaskDevelopmentDto(taskId, $"{task.Project.Code}-{task.Number}",
            commits.Where(x => commitKeys.Contains($"{repositoriesByConnection(repositories, x.RepositoryConnectionId)}:{x.Sha}")
                || GitHubTaskKeyMatcher.ContainsTaskKey(string.Join(' ', x.Message, x.BranchName), taskKey))
                .Select(x => new TaskDevelopmentCommitDto(x.Sha, x.Message, x.AuthorLogin, x.CommittedAt,
                    x.BranchName, x.Url, RepositoryName(repositories, x.RepositoryConnectionId))).ToList(),
            prs.Where(x => prKeys.Contains($"{repositoriesByConnection(repositories, x.RepositoryConnectionId)}:pr:{x.Number}")
                || GitHubTaskKeyMatcher.ContainsTaskKey(string.Join(' ', x.Title, x.HeadBranch, x.BaseBranch), taskKey))
            .Select(x => new TaskDevelopmentPullRequestDto(x.Number, x.Title, x.State, x.IsDraft,
                x.AuthorLogin, x.HeadBranch, x.BaseBranch, x.Reviews.Count,
                x.Reviews.Count(r => r.State == "Approved"), x.MergedAt, x.Url,
                RepositoryName(repositories, x.RepositoryConnectionId),
                new[] { currentTaskReference })).ToList(),
            workflows.Where(x => workflowKeys.Contains($"{repositoriesByConnection(repositories, x.RepositoryConnectionId)}:workflow:{x.RunExternalId}")
                || GitHubTaskKeyMatcher.ContainsTaskKey(string.Join(' ', x.WorkflowName, x.DisplayTitle, x.Branch), taskKey))
            .Select(x => new TaskDevelopmentWorkflowRunDto(x.RunExternalId, x.WorkflowName, x.DisplayTitle,
                x.Branch, x.Status, x.Conclusion, x.StartedAt, x.CompletedAt, x.Url,
                RepositoryName(repositories, x.RepositoryConnectionId),
                new[] { currentTaskReference })).ToList(),
            releases.Where(x => releaseKeys.Contains($"{repositoriesByConnection(repositories, x.RepositoryConnectionId)}:release:{x.ReleaseExternalId}")
                || GitHubTaskKeyMatcher.ContainsTaskKey(string.Join(' ', x.TagName, x.Name), taskKey))
                .Select(x => new TaskDevelopmentReleaseDto(x.TagName, x.Name, x.PublishedAt, x.Url,
                    RepositoryName(repositories, x.RepositoryConnectionId))).ToList(),
            links.Select(x => new TaskDevelopmentLinkDto(x.EntityType, x.ExternalEntityId, x.LinkSource, x.CreatedAt)).ToList());
        return Result.Success(dto);
    }

    private static long repositoriesByConnection(IReadOnlyDictionary<long, GitHubRepositoryConnection> repositories, Guid id)
        => repositories.Values.FirstOrDefault(x => x.Id == id)?.RepositoryExternalId ?? 0;

    private static string RepositoryName(IReadOnlyDictionary<long, GitHubRepositoryConnection> repositories, Guid id)
        => repositories.Values.FirstOrDefault(x => x.Id == id)?.FullName ?? string.Empty;
}
