using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Integrations.GitHub;

public interface IGitHubWebhookProcessor
{
    Task ProcessAsync(GitHubWebhookInbox inbox, CancellationToken ct = default);
}

public sealed class GitHubWebhookProcessor : IGitHubWebhookProcessor
{
    private readonly QalyDbContext _db;

    public GitHubWebhookProcessor(QalyDbContext db) => _db = db;

    public async Task ProcessAsync(GitHubWebhookInbox inbox, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(inbox.Payload);
        var root = document.RootElement;

        if (inbox.EventName == "installation")
        {
            await ProcessInstallationAsync(inbox, root, ct);
            return;
        }

        var connection = inbox.RepositoryExternalId.HasValue
            ? await _db.GitHubRepositoryConnections
                .Include(x => x.Project)
                .FirstOrDefaultAsync(x => x.RepositoryExternalId == inbox.RepositoryExternalId && x.IsActive, ct)
            : null;

        // Installation lifecycle events are accepted for audit now; repository events
        // require an explicit project mapping before data can enter a tenant.
        if (connection is null)
            return;

        inbox.OrganizationId = connection.OrganizationId;
        switch (inbox.EventName)
        {
            case "push":
                await ProcessPushAsync(connection, root, ct);
                break;
            case "pull_request":
                await ProcessPullRequestAsync(connection, root, ct);
                break;
            case "pull_request_review":
                await ProcessReviewAsync(connection, root, ct);
                break;
            case "release":
                await ProcessReleaseAsync(connection, root, ct);
                break;
            case "workflow_run":
                await ProcessWorkflowRunAsync(connection, root, ct);
                break;
        }

        connection.LastSyncedAt = DateTimeOffset.UtcNow;
    }

    private async Task ProcessInstallationAsync(GitHubWebhookInbox inbox, JsonElement root, CancellationToken ct)
    {
        if (!inbox.InstallationId.HasValue) return;
        var installation = await _db.GitHubInstallations
            .FirstOrDefaultAsync(x => x.InstallationId == inbox.InstallationId, ct);
        if (installation is null) return;
        inbox.OrganizationId = installation.OrganizationId;
        var action = ReadString(root, "action");
        installation.Status = action switch
        {
            "deleted" => "Removed",
            "suspend" => "Suspended",
            "unsuspend" => "Active",
            _ => installation.Status
        };
        if (installation.Status != "Active")
        {
            var connections = await _db.GitHubRepositoryConnections
                .Where(x => x.GitHubInstallationId == installation.Id).ToListAsync(ct);
            foreach (var connection in connections) connection.IsActive = false;
        }
    }

    private async Task ProcessPushAsync(GitHubRepositoryConnection connection, JsonElement root, CancellationToken ct)
    {
        var branch = ReadString(root, "ref")?.Replace("refs/heads/", string.Empty, StringComparison.Ordinal);
        if (!root.TryGetProperty("commits", out var commits) || commits.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in commits.EnumerateArray())
        {
            var sha = ReadString(item, "id");
            if (string.IsNullOrWhiteSpace(sha)) continue;

            var commit = await _db.GitHubCommits
                .FirstOrDefaultAsync(x => x.RepositoryConnectionId == connection.Id && x.Sha == sha, ct);
            if (commit is null)
            {
                commit = new GitHubCommit
                {
                    OrganizationId = connection.OrganizationId,
                    RepositoryConnectionId = connection.Id,
                    Sha = sha,
                    CommittedAt = ReadDate(item, "timestamp") ?? DateTimeOffset.UtcNow
                };
                _db.GitHubCommits.Add(commit);
            }

            commit.Message = ReadString(item, "message") ?? string.Empty;
            commit.BranchName = branch;
            commit.Url = ReadString(item, "url") ?? string.Empty;
            if (item.TryGetProperty("author", out var author))
            {
                commit.AuthorLogin = ReadString(author, "username");
                var email = ReadString(author, "email");
                commit.AuthorEmailHash = string.IsNullOrWhiteSpace(email) ? null : HashEmail(email);
            }

            await LinkTasksAsync(connection, "Commit", $"{connection.RepositoryExternalId}:{sha}",
                string.Join(' ', branch, commit.Message), "CommitMessage", ct);
        }
    }

    private async Task ProcessPullRequestAsync(GitHubRepositoryConnection connection, JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("pull_request", out var value)) return;
        var number = ReadInt32(value, "number") ?? ReadInt32(root, "number");
        if (!number.HasValue) return;

        var pullRequest = await _db.GitHubPullRequests
            .FirstOrDefaultAsync(x => x.RepositoryConnectionId == connection.Id && x.Number == number, ct);
        if (pullRequest is null)
        {
            pullRequest = new GitHubPullRequest
            {
                OrganizationId = connection.OrganizationId,
                RepositoryConnectionId = connection.Id,
                Number = number.Value,
                OpenedAt = ReadDate(value, "created_at") ?? DateTimeOffset.UtcNow
            };
            _db.GitHubPullRequests.Add(pullRequest);
        }

        pullRequest.Title = ReadString(value, "title") ?? string.Empty;
        pullRequest.State = ReadBool(value, "merged") == true ? "Merged" : Capitalize(ReadString(value, "state") ?? "open");
        pullRequest.IsDraft = ReadBool(value, "draft") ?? false;
        pullRequest.Url = ReadString(value, "html_url") ?? string.Empty;
        pullRequest.GitHubUpdatedAt = ReadDate(value, "updated_at");
        pullRequest.MergedAt = ReadDate(value, "merged_at");
        if (value.TryGetProperty("user", out var user)) pullRequest.AuthorLogin = ReadString(user, "login");
        if (value.TryGetProperty("merged_by", out var mergedBy) && mergedBy.ValueKind == JsonValueKind.Object)
            pullRequest.MergedByLogin = ReadString(mergedBy, "login");
        if (value.TryGetProperty("head", out var head)) pullRequest.HeadBranch = ReadString(head, "ref") ?? string.Empty;
        if (value.TryGetProperty("base", out var @base)) pullRequest.BaseBranch = ReadString(@base, "ref") ?? string.Empty;

        await LinkTasksAsync(connection, "PullRequest", $"{connection.RepositoryExternalId}:pr:{number}",
            string.Join(' ', pullRequest.Title, pullRequest.HeadBranch, ReadString(value, "body")), "PullRequest", ct);
    }

    private async Task ProcessReviewAsync(GitHubRepositoryConnection connection, JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("pull_request", out var prValue) ||
            !root.TryGetProperty("review", out var reviewValue)) return;
        var number = ReadInt32(prValue, "number");
        var reviewId = ReadInt64(reviewValue, "id");
        if (!number.HasValue || !reviewId.HasValue) return;

        var pullRequest = await _db.GitHubPullRequests
            .FirstOrDefaultAsync(x => x.RepositoryConnectionId == connection.Id && x.Number == number, ct);
        if (pullRequest is null) return;

        var review = await _db.GitHubPullRequestReviews
            .FirstOrDefaultAsync(x => x.PullRequestId == pullRequest.Id && x.ReviewExternalId == reviewId, ct);
        if (review is null)
        {
            review = new GitHubPullRequestReview
            {
                OrganizationId = connection.OrganizationId,
                PullRequestId = pullRequest.Id,
                ReviewExternalId = reviewId.Value
            };
            _db.GitHubPullRequestReviews.Add(review);
        }

        review.State = NormalizeReviewState(ReadString(reviewValue, "state"));
        review.SubmittedAt = ReadDate(reviewValue, "submitted_at") ?? DateTimeOffset.UtcNow;
        review.Url = ReadString(reviewValue, "html_url") ?? string.Empty;
        if (reviewValue.TryGetProperty("user", out var user)) review.ReviewerLogin = ReadString(user, "login");
    }

    private async Task ProcessReleaseAsync(GitHubRepositoryConnection connection, JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("release", out var value)) return;
        var externalId = ReadInt64(value, "id");
        if (!externalId.HasValue) return;

        var release = await _db.GitHubReleases.FirstOrDefaultAsync(x =>
            x.RepositoryConnectionId == connection.Id && x.ReleaseExternalId == externalId, ct);
        if (release is null)
        {
            release = new GitHubRelease
            {
                OrganizationId = connection.OrganizationId,
                RepositoryConnectionId = connection.Id,
                ReleaseExternalId = externalId.Value
            };
            _db.GitHubReleases.Add(release);
        }

        release.TagName = ReadString(value, "tag_name") ?? string.Empty;
        release.Name = ReadString(value, "name");
        release.IsDraft = ReadBool(value, "draft") ?? false;
        release.IsPrerelease = ReadBool(value, "prerelease") ?? false;
        release.PublishedAt = ReadDate(value, "published_at");
        release.Url = ReadString(value, "html_url") ?? string.Empty;
        await LinkTasksAsync(connection, "Release", $"{connection.RepositoryExternalId}:release:{externalId}",
            string.Join(' ', release.TagName, release.Name, ReadString(value, "body")), "TaskKey", ct);
    }

    private async Task ProcessWorkflowRunAsync(GitHubRepositoryConnection connection, JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("workflow_run", out var value)) return;
        var externalId = ReadInt64(value, "id");
        if (!externalId.HasValue) return;
        var run = await _db.GitHubWorkflowRuns.FirstOrDefaultAsync(x =>
            x.RepositoryConnectionId == connection.Id && x.RunExternalId == externalId, ct);
        if (run is null)
        {
            run = new GitHubWorkflowRun
            {
                OrganizationId = connection.OrganizationId,
                RepositoryConnectionId = connection.Id,
                RunExternalId = externalId.Value
            };
            _db.GitHubWorkflowRuns.Add(run);
        }
        run.WorkflowName = ReadString(value, "name") ?? string.Empty;
        run.DisplayTitle = ReadString(value, "display_title");
        run.Branch = ReadString(value, "head_branch") ?? string.Empty;
        run.CommitSha = ReadString(value, "head_sha") ?? string.Empty;
        run.Status = ReadString(value, "status") ?? string.Empty;
        run.Conclusion = ReadString(value, "conclusion");
        run.StartedAt = ReadDate(value, "run_started_at") ?? ReadDate(value, "created_at") ?? DateTimeOffset.UtcNow;
        run.CompletedAt = string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? ReadDate(value, "updated_at") : null;
        run.Url = ReadString(value, "html_url") ?? string.Empty;
        var searchable = string.Join(' ', ReadString(value, "name"), ReadString(value, "head_branch"),
            ReadString(value, "display_title"));
        await LinkTasksAsync(connection, "WorkflowRun", $"{connection.RepositoryExternalId}:workflow:{externalId}",
            searchable, "TaskKey", ct);
    }

    private async Task LinkTasksAsync(GitHubRepositoryConnection connection, string entityType,
        string externalId, string searchable, string source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connection.Project.Code) || string.IsNullOrWhiteSpace(searchable)) return;
        var pattern = $@"(?<![A-Z0-9]){Regex.Escape(connection.Project.Code)}-(?<number>\d+)(?!\d)";
        var numbers = Regex.Matches(searchable, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(x => int.Parse(x.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct().ToArray();
        if (numbers.Length == 0) return;

        var tasks = await _db.TaskItems.Where(x => x.ProjectId == connection.ProjectId && numbers.Contains(x.Number))
            .Select(x => x.Id).ToListAsync(ct);
        foreach (var taskId in tasks)
        {
            var exists = await _db.TaskDevelopmentLinks.AnyAsync(x => x.TaskId == taskId &&
                x.EntityType == entityType && x.ExternalEntityId == externalId, ct);
            if (!exists)
            {
                _db.TaskDevelopmentLinks.Add(new TaskDevelopmentLink
                {
                    OrganizationId = connection.OrganizationId,
                    TaskId = taskId,
                    EntityType = entityType,
                    ExternalEntityId = externalId,
                    LinkSource = source,
                    Confidence = 1
                });
            }
        }
    }

    private static string HashEmail(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()))).ToLowerInvariant();
    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    private static string NormalizeReviewState(string? value) => value?.ToUpperInvariant() switch
    {
        "APPROVED" => "Approved", "CHANGES_REQUESTED" => "ChangesRequested",
        "DISMISSED" => "Dismissed", _ => "Commented"
    };
    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? ReadInt32(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static long? ReadInt64(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : null;
    private static bool? ReadBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && (value.ValueKind is JsonValueKind.True or JsonValueKind.False) ? value.GetBoolean() : null;
    private static DateTimeOffset? ReadDate(JsonElement element, string name)
        => DateTimeOffset.TryParse(ReadString(element, name), out var value) ? value : null;
}
