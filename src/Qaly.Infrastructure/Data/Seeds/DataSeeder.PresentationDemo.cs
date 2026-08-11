using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    private const string PresentationProjectCode = "qaly-workos-demo";
    private const string PresentationProjectName = "Qaly Release 4.0";
    private static readonly string[] PresentationMemberEmails =
    [
        "admin@qaly.dev", "minh.anh@qaly.dev", "bao.ngoc@qaly.dev",
        "linh.chi@qaly.dev", "tuan.kiet@qaly.dev", "yen.nhi@qaly.dev"
    ];

    /// <summary>
    /// Enriches an existing rich-demo database with the exact, repeatable data used by
    /// Qaly_Demo_Script_30_Minutes.pdf. This method never replaces user-created data.
    /// </summary>
    private async Task<bool> EnsurePresentationDemoSeedAsync()
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Code == PresentationProjectCode && !item.IsDeleted);
        if (project?.OrganizationId is not Guid organizationId)
        {
            return false;
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;
        if (!string.Equals(project.Name, PresentationProjectName, StringComparison.Ordinal))
        {
            project.Name = PresentationProjectName;
            project.Description = "Dự án mẫu chính thức cho demo Qaly Release 4.0: roadmap, milestone, task, GitHub, AI Assistant và AI Planner theo ngữ cảnh.";
            project.UpdatedAt = now;
            changed = true;
        }

        var users = await _context.Users
            .Where(item => item.Email.EndsWith("@qaly.dev"))
            .ToDictionaryAsync(item => item.Email, StringComparer.OrdinalIgnoreCase);
        if (!users.TryGetValue("admin@qaly.dev", out var admin) ||
            !users.TryGetValue("minh.anh@qaly.dev", out var manager))
        {
            return changed;
        }

        var sprints = await _context.Set<Sprint>()
            .Where(item => item.ProjectId == project.Id)
            .ToListAsync();

        async Task<Sprint> EnsureSprintAsync(string name, string status, int startOffset, int endOffset, string goal)
        {
            var sprint = sprints.SingleOrDefault(item => item.Name == name);
            if (sprint != null)
            {
                return sprint;
            }

            sprint = new Sprint
            {
                ProjectId = project.Id,
                Name = name,
                Status = status,
                StartDate = now.AddDays(startOffset),
                EndDate = now.AddDays(endOffset),
                Goal = goal,
                CreatedAt = now.AddDays(startOffset)
            };
            await _context.Set<Sprint>().AddAsync(sprint);
            sprints.Add(sprint);
            changed = true;
            return sprint;
        }

        var completedSprint = await EnsureSprintAsync(
            "Milestone 1 - Nền tảng ổn định", "Completed", -35, -15,
            "Hoàn tất xác thực, phân quyền, dashboard và luồng task nền tảng.");
        var activeSprint = sprints.FirstOrDefault(item => item.Status == "Active") ??
            await EnsureSprintAsync("Milestone 2 - Release Candidate", "Active", -8, 6,
                "Hoàn thiện roadmap, GitHub và trải nghiệm demo Release 4.0.");

        const string riskMilestoneName = "Milestone 3 - Go-live có rủi ro";
        var legacyNextSprint = sprints.SingleOrDefault(item => item.Name == "Sprint 07 - Enterprise handoff");
        var duplicateRiskSprint = sprints.SingleOrDefault(item => item.Name == riskMilestoneName);
        Sprint riskSprint;
        if (legacyNextSprint != null)
        {
            riskSprint = legacyNextSprint;
            if (duplicateRiskSprint != null && duplicateRiskSprint.Id != legacyNextSprint.Id)
            {
                var duplicateTasks = await _context.TaskItems
                    .IgnoreQueryFilters()
                    .Where(item => item.SprintId == duplicateRiskSprint.Id)
                    .ToListAsync();
                foreach (var task in duplicateTasks)
                {
                    task.SprintId = legacyNextSprint.Id;
                }
                _context.Set<Sprint>().Remove(duplicateRiskSprint);
                sprints.Remove(duplicateRiskSprint);
            }
        }
        else
        {
            riskSprint = duplicateRiskSprint ?? await EnsureSprintAsync(
                riskMilestoneName, "AtRisk", -14, -1,
                "Đóng các rủi ro phát hành, kiểm thử hồi quy và triển khai production.");
        }

        if (riskSprint.Name != riskMilestoneName || riskSprint.Status != "AtRisk" || riskSprint.EndDate >= now)
        {
            riskSprint.Name = riskMilestoneName;
            riskSprint.Status = "AtRisk";
            riskSprint.StartDate = now.AddDays(-14);
            riskSprint.EndDate = now.AddDays(-1);
            riskSprint.Goal = "Đóng các rủi ro phát hành, kiểm thử hồi quy và triển khai production.";
            riskSprint.UpdatedAt = now;
            changed = true;
        }

        await _context.SaveChangesAsync();

        var existingTitles = await _context.TaskItems
            .IgnoreQueryFilters()
            .Where(item => item.ProjectId == project.Id && !item.IsDeleted)
            .Select(item => item.Title)
            .ToHashSetAsync(StringComparer.Ordinal);

        var assignees = PresentationMemberEmails
            .Where(users.ContainsKey)
            .Select(email => users[email])
            .ToArray();

        var taskDefinitions = new[]
        {
            ("Chốt API contract Release 4.0", completedSprint, "Done", "High", -30, -18),
            ("Kiểm thử phân quyền Manager và Member", completedSprint, "Done", "High", -27, -16),
            ("Hoàn thiện Roadmap và Milestone", activeSprint, "InProgress", "High", -7, 2),
            ("Liên kết Pull Request với task", activeSprint, "InReview", "Medium", -6, 3),
            ("Xác minh CI/CD release candidate", activeSprint, "Todo", "Critical", -3, 4),
            ("Đánh giá rủi ro tiến độ bằng AI", activeSprint, "Todo", "High", -2, 5),
            ("Regression test luồng demo 30 phút", riskSprint, "Todo", "Critical", 7, 12),
            ("Triển khai và giám sát production", riskSprint, "Todo", "High", 10, 18)
        };

        for (var index = 0; index < taskDefinitions.Length; index++)
        {
            var definition = taskDefinitions[index];
            if (existingTitles.Contains(definition.Item1))
            {
                continue;
            }

            var assignee = assignees[index % assignees.Length];
            await _context.TaskItems.AddAsync(new TaskItem
            {
                ProjectId = project.Id,
                SprintId = definition.Item2.Id,
                Title = definition.Item1,
                Description = $"Dữ liệu demo Release 4.0 cho bước: {definition.Item1}.",
                Status = definition.Item3,
                Priority = definition.Item4,
                AssigneeId = assignee.Id,
                ReporterId = manager.Id,
                StartDate = now.AddDays(definition.Item5),
                DueDate = now.AddDays(definition.Item6),
                EstimatedHours = 8 + index,
                ActualHours = definition.Item3 == "Done" ? 7 + index : null,
                SortOrder = 100 + index * 10,
                IsPinned = index is 4 or 6,
                UpvoteCount = definition.Item4 == "Critical" ? 4 : 2,
                CreatedAt = now.AddDays(definition.Item5)
            });
            changed = true;
        }

        await _context.SaveChangesAsync();
        changed |= await EnsurePresentationGitHubSeedAsync(project, organizationId, admin, now);
        return changed;
    }

    private async Task<bool> EnsurePresentationGitHubSeedAsync(
        Project project,
        Guid organizationId,
        User admin,
        DateTimeOffset now)
    {
        var changed = false;
        var installation = await _context.GitHubInstallations
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.AccountLogin == "qaly-demo");
        if (installation == null)
        {
            installation = new GitHubInstallation
            {
                OrganizationId = organizationId,
                InstallationId = 4_000_001,
                AccountId = 4_000_001,
                AccountLogin = "qaly-demo",
                AccountType = "Organization",
                InstalledByUserId = admin.Id,
                Status = "Active",
                CreatedAt = now.AddDays(-30)
            };
            await _context.GitHubInstallations.AddAsync(installation);
            await _context.SaveChangesAsync();
            changed = true;
        }

        var connection = await _context.GitHubRepositoryConnections
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ProjectId == project.Id && item.RepositoryExternalId == 4_000_001);
        if (connection == null)
        {
            connection = new GitHubRepositoryConnection
            {
                OrganizationId = organizationId,
                ProjectId = project.Id,
                GitHubInstallationId = installation.Id,
                RepositoryExternalId = 4_000_001,
                Owner = "qaly-demo",
                Name = "qaly-release-4",
                FullName = "qaly-demo/qaly-release-4",
                DefaultBranch = "main",
                IsPrivate = true,
                IsActive = true,
                LastSyncedAt = now.AddMinutes(-8),
                CreatedAt = now.AddDays(-28)
            };
            await _context.GitHubRepositoryConnections.AddAsync(connection);
            await _context.SaveChangesAsync();
            changed = true;
        }

        var pullRequests = new[]
        {
            (401, "feat: hoàn thiện roadmap Release 4.0", "Open", "viet-minh", "feature/release-4-roadmap", false, -2),
            (398, "fix: ổn định AI Assistant fallback", "Open", "quoc-bao", "fix/assistant-fallback", false, -4),
            (392, "test: bổ sung E2E demo 30 phút", "Merged", "doan-trung", "test/demo-30m", false, -6)
        };
        var existingPrNumbers = await _context.GitHubPullRequests
            .Where(item => item.RepositoryConnectionId == connection.Id)
            .Select(item => item.Number)
            .ToHashSetAsync();
        foreach (var item in pullRequests.Where(item => !existingPrNumbers.Contains(item.Item1)))
        {
            await _context.GitHubPullRequests.AddAsync(new GitHubPullRequest
            {
                OrganizationId = organizationId,
                RepositoryConnectionId = connection.Id,
                Number = item.Item1,
                Title = item.Item2,
                State = item.Item3,
                AuthorLogin = item.Item4,
                HeadBranch = item.Item5,
                BaseBranch = "main",
                IsDraft = item.Item6,
                OpenedAt = now.AddDays(item.Item7),
                GitHubUpdatedAt = now.AddHours(-3),
                MergedAt = item.Item3 == "Merged" ? now.AddDays(-2) : null,
                MergedByLogin = item.Item3 == "Merged" ? "quang-tuan" : null,
                Url = $"https://github.com/qaly-demo/qaly-release-4/pull/{item.Item1}",
                CreatedAt = now.AddDays(item.Item7)
            });
            changed = true;
        }

        var existingRunIds = await _context.GitHubWorkflowRuns
            .Where(item => item.RepositoryConnectionId == connection.Id)
            .Select(item => item.RunExternalId)
            .ToHashSetAsync();
        if (!existingRunIds.Contains(4_001))
        {
            await _context.GitHubWorkflowRuns.AddAsync(new GitHubWorkflowRun
            {
                OrganizationId = organizationId, RepositoryConnectionId = connection.Id,
                RunExternalId = 4_001, WorkflowName = "Release Candidate", DisplayTitle = "Build & E2E Release 4.0",
                Branch = "main", CommitSha = "4a1f00dcafe401", Status = "completed", Conclusion = "success",
                StartedAt = now.AddHours(-5), CompletedAt = now.AddHours(-5).AddMinutes(11),
                Url = "https://github.com/qaly-demo/qaly-release-4/actions/runs/4001", CreatedAt = now.AddHours(-5)
            });
            changed = true;
        }
        if (!existingRunIds.Contains(3_998))
        {
            await _context.GitHubWorkflowRuns.AddAsync(new GitHubWorkflowRun
            {
                OrganizationId = organizationId, RepositoryConnectionId = connection.Id,
                RunExternalId = 3_998, WorkflowName = "Security Scan", DisplayTitle = "Dependency audit",
                Branch = "fix/assistant-fallback", CommitSha = "398badcafe398", Status = "completed", Conclusion = "failure",
                StartedAt = now.AddDays(-1), CompletedAt = now.AddDays(-1).AddMinutes(7),
                Url = "https://github.com/qaly-demo/qaly-release-4/actions/runs/3998", CreatedAt = now.AddDays(-1)
            });
            changed = true;
        }

        if (!await _context.GitHubReleases.AnyAsync(item => item.RepositoryConnectionId == connection.Id && item.ReleaseExternalId == 4_000))
        {
            await _context.GitHubReleases.AddAsync(new GitHubRelease
            {
                OrganizationId = organizationId, RepositoryConnectionId = connection.Id,
                ReleaseExternalId = 4_000, TagName = "v4.0.0-rc.1", Name = "Qaly Release 4.0 RC1",
                IsDraft = false, IsPrerelease = true, PublishedAt = now.AddDays(-2),
                Url = "https://github.com/qaly-demo/qaly-release-4/releases/tag/v4.0.0-rc.1", CreatedAt = now.AddDays(-2)
            });
            changed = true;
        }

        await SaveIfChangedAsync();
        return changed;
    }
}
