using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Seeds;

namespace Qaly.UnitTests;

public sealed class RichDemoSeedTests
{
    [Fact]
    public async Task SeedAsync_SatisfiesGraduationDemoManifestD01ThroughD07()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Seed:UseRichDemoSeed"] = "true",
                ["Seed:AdminPassword"] = "QalyDemoAdmin!2026",
                ["Seed:DefaultUserPassword"] = "QalyDemoUser!2026"
            })
            .Build();
        var seeder = new DataSeeder(db, configuration, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();

        var demoOrganization = await db.Organizations.SingleAsync(item => item.Code == "qaly-demo-2026");
        var demoProject = await db.Projects.SingleAsync(item => item.Code == "qaly-workos-demo");

        // D01: a real Project graph with schedule, dependency, member, workload and deadline.
        (await db.Set<Sprint>().AnyAsync(item =>
            item.ProjectId == demoProject.Id && item.EndDate > item.StartDate))
            .Should().BeTrue();
        (await db.TaskDependencies.AnyAsync(item =>
            item.Predecessor.ProjectId == demoProject.Id && item.Successor.ProjectId == demoProject.Id))
            .Should().BeTrue();
        (await db.ProjectMembers.CountAsync(item => item.ProjectId == demoProject.Id))
            .Should().BeGreaterThanOrEqualTo(5);
        (await db.OrganizationMembers.AnyAsync(item =>
            item.OrganizationId == demoOrganization.Id &&
            (item.Role == "Admin" || item.Role == "Manager")))
            .Should().BeFalse("demo memberships must use the canonical organization role vocabulary");
        (await db.OrganizationMembers.SingleAsync(item =>
            item.OrganizationId == demoOrganization.Id && item.UserId == demoOrganization.OwnerId))
            .Role.Should().Be("Owner");
        (await db.TaskItems.AnyAsync(item =>
            item.ProjectId == demoProject.Id && item.DueDate != null && item.EstimatedHours > 0))
            .Should().BeTrue();
        (await db.TaskItems.AnyAsync(item =>
            item.Project.OrganizationId == demoOrganization.Id &&
            item.AssigneeId != null &&
            !item.Assignees.Any(assignment => assignment.UserId == item.AssigneeId)))
            .Should().BeFalse("the primary assignee is part of the canonical assignment set");
        (await db.TaskAssignments.AnyAsync(item =>
            item.TaskItem.Project.OrganizationId == demoOrganization.Id &&
            !item.TaskItem.Project.Members.Any(member => member.UserId == item.UserId)))
            .Should().BeFalse("a task cannot be assigned to a user outside its project");
        (await db.TaskItems.AnyAsync(item =>
            item.Project.OrganizationId == demoOrganization.Id &&
            item.Status == "Done" && item.ReviewerId == null))
            .Should().BeFalse("completed demo tasks need canonical review attribution");
        (await db.TimeEntries.AnyAsync(item => item.Task.ProjectId == demoProject.Id))
            .Should().BeTrue();

        // D02-D03: open/Done Tasks, verified skill evidence, declared capacity and a real absence window.
        (await db.TaskItems.AnyAsync(item =>
            item.Project.OrganizationId == demoOrganization.Id &&
            item.Status != "Done" &&
            item.SkillRequirements.Any()))
            .Should().BeTrue();
        (await db.TaskItems.AnyAsync(item =>
            item.Project.OrganizationId == demoOrganization.Id && item.Status == "Done"))
            .Should().BeTrue();
        (await db.TaskCompletionAttributions.AnyAsync(item =>
            item.TaskItem.Project.OrganizationId == demoOrganization.Id &&
            item.Status == TaskCompletionAttribution.Confirmed &&
            item.TaskItem.SkillRequirements.Any()))
            .Should().BeTrue();
        (await db.OrganizationMemberCapacityProfiles.CountAsync(item =>
            item.OrganizationId == demoOrganization.Id && item.WeeklyCapacityHours > 0))
            .Should().Be(12);
        (await db.OrganizationMembers.AnyAsync(member =>
            member.OrganizationId == demoOrganization.Id &&
            member.User.Email.EndsWith("@qaly.dev") &&
            !db.OrganizationMemberCapacityProfiles.Any(profile =>
                profile.OrganizationId == member.OrganizationId && profile.UserId == member.UserId)))
            .Should().BeFalse("every seeded demo member has an explicit capacity declaration");
        (await db.MemberAvailabilityWindows.AnyAsync(item =>
            item.Profile.OrganizationId == demoOrganization.Id &&
            item.Kind == MemberAvailabilityWindow.Unavailable))
            .Should().BeTrue();

        // D04: source-bearing Wiki, manageable Poll group and meeting transcript.
        (await db.WikiPages.AnyAsync(item =>
            item.Project.OrganizationId == demoOrganization.Id &&
            item.Content.Contains("1.")))
            .Should().BeTrue();
        (await db.GroupPolls.AnyAsync(item =>
            item.Group.OrganizationId == demoOrganization.Id && item.Options.Count >= 2))
            .Should().BeTrue();
        (await db.WorkGroupMembers.AnyAsync(item =>
            item.WorkGroup.OrganizationId == demoOrganization.Id &&
            (item.Role == "Owner" || item.Role == "Manager")))
            .Should().BeTrue();
        (await db.MeetingImports.AnyAsync(item =>
            item.Project.OrganizationId == demoOrganization.Id &&
            item.TranscriptText != null && item.TranscriptText != string.Empty))
            .Should().BeTrue();

        // D05: a real platform Member whose selected demo Project capability is read-only.
        var readOnlyMember = await db.Users.SingleAsync(item => item.Email == "yen.nhi@qaly.dev");
        readOnlyMember.Role.Should().Be("Member");
        (await db.ProjectMembers.AnyAsync(item =>
            item.ProjectId == demoProject.Id &&
            item.UserId == readOnlyMember.Id &&
            item.Role == "Viewer"))
            .Should().BeTrue();

        // A previously-created demo database can predate the expanded project-role matrix.
        // Re-running the seed must repair those canonical memberships without a destructive reseed.
        var staleDeveloper = await db.ProjectMembers.SingleAsync(item =>
            item.ProjectId == demoProject.Id && item.User.Email == "linh.chi@qaly.dev");
        staleDeveloper.Role = "Member";
        var missingCustomer = await db.ProjectMembers.SingleAsync(item =>
            item.ProjectId == demoProject.Id && item.User.Email == "viet.long@qaly.dev");
        db.ProjectMembers.Remove(missingCustomer);
        await db.SaveChangesAsync();

        await seeder.SeedAsync();

        var repairedRoles = await db.ProjectMembers
            .Where(item => item.ProjectId == demoProject.Id)
            .ToDictionaryAsync(item => item.User.Email, item => item.Role);
        repairedRoles.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["admin@qaly.dev"] = "Owner",
            ["minh.anh@qaly.dev"] = "Manager",
            ["bao.ngoc@qaly.dev"] = "ScrumMaster",
            ["linh.chi@qaly.dev"] = "Developer",
            ["quoc.huy@qaly.dev"] = "Developer",
            ["tuan.kiet@qaly.dev"] = "Tester",
            ["thanh.tam@qaly.dev"] = "Reviewer",
            ["mai.phuong@qaly.dev"] = "Member",
            ["yen.nhi@qaly.dev"] = "Viewer",
            ["viet.long@qaly.dev"] = "Customer",
            ["gia.khang@qaly.dev"] = "Developer"
        });
        // D07: canonical project-management defense states are backed by real rows.
        demoProject.EnableOnHold.Should().BeTrue();
        demoProject.EnableInReview.Should().BeTrue();
        demoProject.RequireEvidenceToDone.Should().BeTrue();
        demoProject.RestrictTransitionsToAdmin.Should().BeFalse();

        var defenseTasks = await db.TaskItems
            .Where(item => item.ProjectId == demoProject.Id && item.Title.StartsWith("DEMO-QA "))
            .Include(item => item.Assignees)
            .Include(item => item.AcceptanceChecklist)
            .Include(item => item.Attachments)
            .OrderBy(item => item.Title)
            .ToListAsync();
        defenseTasks.Should().HaveCount(7);
        defenseTasks.Select(item => item.Status).Should().Contain([
            "Todo", "InProgress", "OnHold", "InReview", "Done", "Cancelled"
        ]);

        var unassignedTask = defenseTasks.Single(item => item.Title.Contains("Task mới chờ phân công"));
        unassignedTask.AssigneeId.Should().BeNull("calendar gaps are not treated as declared capacity");
        unassignedTask.Assignees.Should().BeEmpty();

        var reviewTask = defenseTasks.Single(item => item.Title.Contains("Chờ reviewer duyệt evidence"));
        reviewTask.AssigneeId.Should().NotBeNull();
        reviewTask.ReviewerId.Should().NotBeNull();
        (reviewTask.ReviewerId == reviewTask.AssigneeId).Should().BeFalse();
        reviewTask.AcceptanceChecklist.Should().NotBeEmpty();
        reviewTask.Attachments.Should().ContainSingle(item =>
            item.IsEvidence && item.EvidenceApprovalStatus == "Pending");

        var returnedTask = defenseTasks.Single(item => item.Title.Contains("Reviewer đã trả lại để sửa"));
        returnedTask.Status.Should().Be("InProgress");
        returnedTask.Attachments.Should().ContainSingle(item =>
            item.IsEvidence && item.EvidenceApprovalStatus == "Rejected" &&
            item.EvidenceReviewedById == returnedTask.ReviewerId);
        (await db.AuditLogs.AnyAsync(item =>
            item.EntityType == nameof(TaskItem) && item.EntityId == returnedTask.Id.ToString() &&
            item.ChangesJson != null && item.ChangesJson.Contains("\"from\":\"InReview\"") &&
            item.ChangesJson.Contains("\"to\":\"InProgress\"")))
            .Should().BeTrue();

        var completedTask = defenseTasks.Single(item => item.Title.Contains("Hoàn thành sau duyệt hợp lệ"));
        completedTask.ReviewerId.Should().NotBeNull();
        (completedTask.ReviewerId == completedTask.AssigneeId).Should().BeFalse();
        completedTask.AcceptanceChecklist.Should().NotBeEmpty()
            .And.OnlyContain(item => item.IsCompleted);
        completedTask.Attachments.Should().ContainSingle(item =>
            item.IsEvidence && item.EvidenceApprovalStatus == "Approved" &&
            item.EvidenceReviewedById == completedTask.ReviewerId);
        (await db.TaskCompletionAttributions.AnyAsync(item =>
            item.TaskItemId == completedTask.Id && item.Status == TaskCompletionAttribution.Confirmed))
            .Should().BeTrue();

        var blockedTask = defenseTasks.Single(item => item.Title.Contains("Tạm dừng vì phụ thuộc bên ngoài"));
        blockedTask.Status.Should().Be("OnHold");
        blockedTask.DueDate.Should().BeBefore(DateTimeOffset.UtcNow);
        (await db.TaskComments.AnyAsync(item =>
            item.TaskItemId == blockedTask.Id && item.Content.Contains("API key sandbox")))
            .Should().BeTrue();
        (await db.TaskDependencies.CountAsync(item =>
            defenseTasks.Select(task => task.Id).Contains(item.PredecessorId) &&
            defenseTasks.Select(task => task.Id).Contains(item.SuccessorId)))
            .Should().Be(2);
        (await db.Notifications.AnyAsync(item => item.IdempotencyKey == "demo:defense:review-requested"))
            .Should().BeTrue();
        (await db.Notifications.AnyAsync(item => item.IdempotencyKey == "demo:defense:changes-requested"))
            .Should().BeTrue();
        (await db.WikiPages.AnyAsync(item =>
            item.ProjectId == demoProject.Id && item.Title == "Kịch bản phản biện workflow quản lý dự án"))
            .Should().BeTrue();

        // D08: the dashboard has a real portfolio mix instead of five near-empty 0% bars.
        var dashboardTasks = await db.TaskItems
            .Where(item => item.Title.StartsWith(DataSeeder.DashboardScenarioPrefix))
            .ToListAsync();
        dashboardTasks.Should().HaveCount(12);
        dashboardTasks.Select(item => item.Status).Should().Contain(["Done", "Todo", "InProgress", "InReview", "OnHold"]);
        var seededActiveProjectIds = await db.Projects
            .Where(item => item.OrganizationId == demoOrganization.Id && item.Status == "Active")
            .Select(item => item.Id)
            .ToListAsync();
        foreach (var projectId in seededActiveProjectIds)
        {
            (await db.TaskItems.AnyAsync(item => item.ProjectId == projectId && item.Status == "Done"))
                .Should().BeTrue("every active presentation Project needs visible completed work");
            (await db.TaskItems.AnyAsync(item => item.ProjectId == projectId && item.Status != "Done" && item.Status != "Cancelled"))
                .Should().BeTrue("every active presentation Project needs visible work remaining");
        }
    }

    [Fact]
    public async Task SeedAsync_CreatesOnlyCanonicalAiJobLifecycleData()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Seed:UseRichDemoSeed"] = "true",
                ["Seed:AdminPassword"] = "QalyDemoAdmin!2026",
                ["Seed:DefaultUserPassword"] = "QalyDemoUser!2026"
            })
            .Build();
        var seeder = new DataSeeder(db, configuration, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();

        var jobs = await db.AiJobs
            .Include(job => job.Dispatch)
            .Include(job => job.Sources)
            .OrderBy(job => job.CreatedAt)
            .ToListAsync();
        var legacyQueue = await db.AiJobQueue.ToListAsync();

        jobs.Should().HaveCount(2);
        legacyQueue.Should().BeEmpty();
        jobs.Should().OnlyContain(job =>
            !string.IsNullOrWhiteSpace(job.JobType) &&
            !string.IsNullOrWhiteSpace(job.SchemaId) &&
            !string.IsNullOrWhiteSpace(job.SchemaVersion) &&
            !string.IsNullOrWhiteSpace(job.RequestHash) &&
            !string.IsNullOrWhiteSpace(job.IdempotencyKey) &&
            !string.IsNullOrWhiteSpace(job.CacheKey));
        jobs.Should().OnlyContain(job => job.Dispatch != null && job.Sources.Count > 0);
        jobs.Should().ContainSingle(job =>
            job.Status == AiJobStatuses.Succeeded &&
            job.Dispatch!.CompletedAt != null);
        jobs.Should().ContainSingle(job =>
            job.Status == AiJobStatuses.Queued &&
            job.Dispatch!.CompletedAt == null);
    }

    [Fact]
    public async Task SeedAsync_CreatesIdempotentAiNativeEvidenceForAllFiveActiveProjects()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Seed:UseRichDemoSeed"] = "true",
                ["Seed:AdminPassword"] = "QalyDemoAdmin!2026",
                ["Seed:DefaultUserPassword"] = "QalyDemoUser!2026"
            })
            .Build();
        var seeder = new DataSeeder(db, configuration, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();

        var organization = await db.Organizations.SingleAsync(item => item.Code == "qaly-demo-2026");
        var activeProjectIds = await db.Projects
            .Where(item => item.OrganizationId == organization.Id && item.Status == "Active")
            .Select(item => item.Id)
            .ToListAsync();
        var evidenceProjectIds = await db.TaskCompletionAttributions
            .Where(item => item.Status == TaskCompletionAttribution.Confirmed)
            .Select(item => item.TaskItem.ProjectId)
            .Distinct()
            .ToListAsync();
        var requirementProjectIds = await db.TaskSkillRequirements
            .Select(item => item.TaskItem.ProjectId)
            .Distinct()
            .ToListAsync();

        activeProjectIds.Should().HaveCount(5);
        evidenceProjectIds.Should().BeEquivalentTo(activeProjectIds);
        requirementProjectIds.Should().Contain(activeProjectIds);
        var skillCatalog = await db.OrganizationSkills
            .Where(item => item.OrganizationId == organization.Id)
            .ToListAsync();
        skillCatalog.Should().HaveCount(17);
        skillCatalog.Should().OnlyContain(item => item.IsSystemSeed &&
            !string.IsNullOrWhiteSpace(item.Category) &&
            !string.IsNullOrWhiteSpace(item.DefaultRequiredLevel) &&
            item.AliasesJson != "[]");
        skillCatalog.Should().Contain(item => item.NormalizedName == "project-product-management" && item.Category == "Quản lý sản phẩm và dự án");
        skillCatalog.Should().Contain(item => item.NormalizedName == "security-auth-privacy" && item.Category == "Bảo mật và tuân thủ");
        skillCatalog.Should().Contain(item => item.NormalizedName == "communication-leadership" && item.Category == "Giao tiếp và lãnh đạo");
        (await db.TaskSkillRequirements.CountAsync()).Should().BeGreaterThanOrEqualTo(59);
        (await db.TaskCompletionAttributions.CountAsync(item => item.Status == TaskCompletionAttribution.Confirmed)).Should().BeGreaterThanOrEqualTo(18);
        var activeMemberIds = await db.OrganizationMembers
            .Where(item => item.OrganizationId == organization.Id)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync();
        var contributorsWithConfirmedEvidence = await db.TaskCompletionAttributions
            .Where(item => item.Status == TaskCompletionAttribution.Confirmed &&
                           item.TaskItem.Project.OrganizationId == organization.Id &&
                           item.TaskItem.SkillRequirements.Any())
            .Select(item => item.ContributorUserId)
            .Distinct()
            .ToListAsync();
        contributorsWithConfirmedEvidence.Should().Contain(activeMemberIds,
            "mọi thành viên demo cần ít nhất một Task canonical đã hoàn thành để Project Launch có thể kiểm tra skill evidence");
        (await db.TaskItems.CountAsync(item => item.Title.StartsWith("Evidence:") && item.Status == "Done"))
            .Should().Be(11);
        (await db.OrganizationMemberCapacityProfiles.CountAsync(item => item.OrganizationId == organization.Id)).Should().Be(12);
        (await db.ProfessionalProfileDefinitions.CountAsync(item => item.OrganizationId == organization.Id && item.IsActive))
            .Should().Be(20);
        var professionalAssignments = await db.OrganizationMemberProfessionalProfiles
            .Where(item => item.OrganizationId == organization.Id)
            .ToListAsync();
        professionalAssignments.Should().HaveCount(27);
        professionalAssignments.Should().OnlyContain(item =>
            item.VerificationStatus == OrganizationMemberProfessionalProfile.Verified
            && item.VerifiedByUserId != null
            && item.VerifiedAt != null);
        professionalAssignments.GroupBy(item => item.UserId).Should().HaveCount(12);
        professionalAssignments.GroupBy(item => item.UserId).Should().OnlyContain(group => group.Count() >= 2);
        (await db.MemberAvailabilityWindows.CountAsync()).Should().Be(5);
        (await db.OrganizationWorkRuleSets.CountAsync(item =>
            item.OrganizationId == organization.Id && item.Status == "active")).Should().Be(1);
        (await db.TaskItems.CountAsync(item => item.Title == "Xác nhận dữ liệu POS Wave 1" && item.Status == "Done")).Should().Be(1);
        (await db.WikiPages.CountAsync(item => item.Project.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(3);
        (await db.GroupPolls.CountAsync(item => item.Group.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.GroupMeetingSessions.CountAsync(item => item.WorkGroup.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.MeetingImports.CountAsync(item => item.Project.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.GitHubPullRequests.CountAsync(item => item.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(2);
        (await db.GitHubReleases.CountAsync(item => item.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);
        (await db.ProjectDigestSubscriptions.CountAsync(item => item.Project.OrganizationId == organization.Id)).Should().BeGreaterThanOrEqualTo(1);

        var countsBeforeSecondSeed = new
        {
            Skills = await db.OrganizationSkills.CountAsync(),
            Requirements = await db.TaskSkillRequirements.CountAsync(),
            Attributions = await db.TaskCompletionAttributions.CountAsync(),
            Profiles = await db.OrganizationMemberCapacityProfiles.CountAsync(),
            ProfessionalDefinitions = await db.ProfessionalProfileDefinitions.CountAsync(),
            ProfessionalAssignments = await db.OrganizationMemberProfessionalProfiles.CountAsync(),
            Availability = await db.MemberAvailabilityWindows.CountAsync(),
            ProjectMembers = await db.ProjectMembers.CountAsync(),
            TaskAssignments = await db.TaskAssignments.CountAsync(),
            Tasks = await db.TaskItems.CountAsync(),
            Checklists = await db.TaskAcceptanceChecklistItems.CountAsync(),
            Attachments = await db.TaskAttachments.CountAsync(),
            Comments = await db.TaskComments.CountAsync(),
            Dependencies = await db.TaskDependencies.CountAsync(),
            TimeEntries = await db.TimeEntries.CountAsync(),
            AuditLogs = await db.AuditLogs.CountAsync(),
            Notifications = await db.Notifications.CountAsync(),
            Wikis = await db.WikiPages.CountAsync()
        };

        await seeder.SeedAsync();

        var countsAfterSecondSeed = new
        {
            Skills = await db.OrganizationSkills.CountAsync(),
            Requirements = await db.TaskSkillRequirements.CountAsync(),
            Attributions = await db.TaskCompletionAttributions.CountAsync(),
            Profiles = await db.OrganizationMemberCapacityProfiles.CountAsync(),
            ProfessionalDefinitions = await db.ProfessionalProfileDefinitions.CountAsync(),
            ProfessionalAssignments = await db.OrganizationMemberProfessionalProfiles.CountAsync(),
            Availability = await db.MemberAvailabilityWindows.CountAsync(),
            ProjectMembers = await db.ProjectMembers.CountAsync(),
            TaskAssignments = await db.TaskAssignments.CountAsync(),
            Tasks = await db.TaskItems.CountAsync(),
            Checklists = await db.TaskAcceptanceChecklistItems.CountAsync(),
            Attachments = await db.TaskAttachments.CountAsync(),
            Comments = await db.TaskComments.CountAsync(),
            Dependencies = await db.TaskDependencies.CountAsync(),
            TimeEntries = await db.TimeEntries.CountAsync(),
            AuditLogs = await db.AuditLogs.CountAsync(),
            Notifications = await db.Notifications.CountAsync(),
            Wikis = await db.WikiPages.CountAsync()
        };
        countsAfterSecondSeed.Should().BeEquivalentTo(countsBeforeSecondSeed);
    }
}
