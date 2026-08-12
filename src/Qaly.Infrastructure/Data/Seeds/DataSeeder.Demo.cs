using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Enums;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    private const string DemoSeedMarkerTitle = "Demo Data Handbook - Qaly 2026";

    private static readonly string[] LegacyDemoProjectCodes =
    [
        "qaly-mvp",
        "ai-lab",
        "smart-city-qaly",
        "fintech-security-2026"
    ];

    private async Task<bool> EnsureRichDemoSeedAsync()
    {
        if (await HasCurrentDemoSeedAsync())
        {
            var presentationSeedChanged = await EnsurePresentationDemoSeedAsync();
            var aiEvidenceChanged = await EnsureAiNativeDemoEvidenceAsync();
            return presentationSeedChanged || aiEvidenceChanged;
        }

        var isDatabaseEmpty =
            !await _context.Users.AnyAsync() &&
            !await _context.Projects.IgnoreQueryFilters().AnyAsync() &&
            !await _context.WorkGroups.IgnoreQueryFilters().AnyAsync();

        if (!isDatabaseEmpty && await HasLegacyDemoSeedAsync())
        {
            LogStaleDataDetected(_logger);
            await ClearDemoDataAsync();
            isDatabaseEmpty = true;
        }

        if (!isDatabaseEmpty)
        {
            return false;
        }

        await SeedRichDemoDataAsync();
        await EnsureAiNativeDemoEvidenceAsync();
        return true;
    }

    private Task<bool> HasCurrentDemoSeedAsync()
    {
        return _context.WikiPages
            .IgnoreQueryFilters()
            .AnyAsync(page => page.Title == DemoSeedMarkerTitle);
    }

    private async Task<bool> HasLegacyDemoSeedAsync()
    {
        return await _context.Users.AnyAsync(user => user.Email.EndsWith("@qaly.dev")) ||
               await _context.Projects.IgnoreQueryFilters().AnyAsync(project => LegacyDemoProjectCodes.Contains(project.Code));
    }

    private async Task ClearDemoDataAsync()
    {
        await RemoveEntitiesAsync(_context.GroupPollVotes);
        await RemoveEntitiesAsync(_context.GroupPollOptions);
        await RemoveEntitiesAsync(_context.GroupPolls);
        await RemoveEntitiesAsync(_context.GroupMessageUserStates);
        await RemoveEntitiesAsync(_context.GroupAttachments);
        await RemoveEntitiesAsync(_context.GroupInvitations);
        await RemoveEntitiesAsync(_context.GroupMessages.IgnoreQueryFilters());
        await RemoveEntitiesAsync(_context.GroupMeetingSessions);
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.MeetingActionItemMappings);
        await RemoveEntitiesAsync(_context.MeetingImports);
        await RemoveEntitiesAsync(_context.AiGeneratedDrafts);
        await RemoveEntitiesAsync(_context.AiJobActivityEvents);
        await RemoveEntitiesAsync(_context.AiUsageLedger);
        await RemoveEntitiesAsync(_context.AiPromptCache);
        await RemoveEntitiesAsync(_context.AiJobQueue);
        await RemoveEntitiesAsync(_context.AiJobs);
        await RemoveEntitiesAsync(_context.AiAuditEvents);
        await RemoveEntitiesAsync(_context.AiBudgetPolicies);
        await RemoveEntitiesAsync(_context.AiProviderConfigs);
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.WebhookDeliveryLogs);
        await RemoveEntitiesAsync(_context.WebhookSubscriptions.IgnoreQueryFilters());
        await RemoveEntitiesAsync(_context.ApiKeys);
        await RemoveEntitiesAsync(_context.Notifications);
        await RemoveEntitiesAsync(_context.PushSubscriptions);
        await RemoveEntitiesAsync(_context.AuditLogs);
        await RemoveEntitiesAsync(_context.PrivacyConsents);
        await RemoveEntitiesAsync(_context.DataSubjectRequests);
        await RemoveEntitiesAsync(_context.VectorSyncOutbox);
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.Votes);
        await RemoveEntitiesAsync(_context.MemberAvailabilityWindows);
        await RemoveEntitiesAsync(_context.OrganizationMemberCapacityProfiles);
        await RemoveEntitiesAsync(_context.TaskCompletionAttributions);
        await RemoveEntitiesAsync(_context.TaskSkillRequirements);
        await RemoveEntitiesAsync(_context.TaskAttentionSignals);
        await RemoveEntitiesAsync(_context.TaskViewEvents);
        await RemoveEntitiesAsync(_context.TimeEntries);
        await RemoveEntitiesAsync(_context.TaskDependencies);
        await RemoveEntitiesAsync(_context.TaskLabels);
        await RemoveEntitiesAsync(_context.TaskAssignments);
        await RemoveEntitiesAsync(_context.TaskAttachments.IgnoreQueryFilters());
        await SaveIfChangedAsync();

        var comments = await _context.TaskComments.IgnoreQueryFilters().ToListAsync();
        foreach (var comment in comments)
        {
            comment.ParentCommentId = null;
        }
        await SaveIfChangedAsync();
        _context.TaskComments.RemoveRange(comments);
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.TaskItems.IgnoreQueryFilters());
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.ImportSessions);
        await RemoveEntitiesAsync(_context.ProjectLabels);
        await RemoveEntitiesAsync(_context.WikiPages.IgnoreQueryFilters());
        await RemoveEntitiesAsync(_context.Set<Sprint>());
        await RemoveEntitiesAsync(_context.ProjectMembers);
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.Projects.IgnoreQueryFilters());
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.WorkGroupMembers);
        await RemoveEntitiesAsync(_context.WorkGroups.IgnoreQueryFilters());
        await RemoveEntitiesAsync(_context.OrganizationWorkRuleDecisions);
        await RemoveEntitiesAsync(_context.OrganizationWorkRuleSets);
        await RemoveEntitiesAsync(_context.OrganizationSkills);
        await RemoveEntitiesAsync(_context.OrganizationMembers);
        await RemoveEntitiesAsync(_context.Organizations);
        await SaveIfChangedAsync();

        await RemoveEntitiesAsync(_context.Users);
        await SaveIfChangedAsync();
    }

    private async Task RemoveEntitiesAsync<TEntity>(IQueryable<TEntity> query) where TEntity : class
    {
        var entities = await query.ToListAsync();
        if (entities.Count > 0)
        {
            _context.Set<TEntity>().RemoveRange(entities);
        }
    }

    private Task SaveIfChangedAsync()
    {
        return _context.ChangeTracker.HasChanges()
            ? _context.SaveChangesAsync()
            : Task.CompletedTask;
    }

    private async Task SeedRichDemoDataAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var adminPassword = GetRequiredSeedSecret("Seed:AdminPassword", "QALY_SEED_ADMIN_PASSWORD");
        var userPassword = GetRequiredSeedSecret("Seed:DefaultUserPassword", "QALY_SEED_DEFAULT_USER_PASSWORD");

        var users = CreateDemoUsers(adminPassword, userPassword, now);
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        LogSeededUsers(_logger, users.Count);

        var userByEmail = users.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
        User U(string email) => userByEmail[email];

        var organization = new Organization
        {
            Name = "Qaly Demo Customer Success",
            Code = "qaly-demo-2026",
            Description = "Không gian demo nội bộ mô phỏng một khách hàng đang triển khai Qaly cho nhiều đội sản phẩm, vận hành và bảo mật.",
            OwnerId = U("admin@qaly.dev").Id,
            CreatedAt = now.AddDays(-70)
        };
        await _context.Organizations.AddAsync(organization);
        await _context.SaveChangesAsync();

        await _context.OrganizationMembers.AddRangeAsync(users.Select((user, index) => new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = index == 0 ? "Admin" : index <= 2 ? "Manager" : "Member",
            JoinedAt = now.AddDays(-68 + index),
            CreatedAt = now.AddDays(-68 + index)
        }));

        var groups = new List<WorkGroup>
        {
            new()
            {
                Name = "Nova Retail Pilot War Room",
                OwnerId = U("minh.anh@qaly.dev").Id,
                OrganizationId = organization.Id,
                AvatarUrl = "/images/groups/nova-retail.png",
                Color = "#0F766E",
                BackgroundTheme = "teal",
                BackgroundImageUrl = "/images/demo/nova-retail-board.jpg",
                CreatedAt = now.AddDays(-48)
            },
            new()
            {
                Name = "Qaly Product & Engineering",
                OwnerId = U("admin@qaly.dev").Id,
                OrganizationId = organization.Id,
                AvatarUrl = "/images/groups/qaly-product.png",
                Color = "#2563EB",
                BackgroundTheme = "blue",
                BackgroundImageUrl = "/images/demo/qaly-product-board.jpg",
                CreatedAt = now.AddDays(-62)
            }
        };
        await _context.WorkGroups.AddRangeAsync(groups);
        await _context.SaveChangesAsync();

        await SeedGroupCollaborationAsync(groups, userByEmail, now);

        var projectByCode = await SeedProjectsAndMembersAsync(organization, groups, userByEmail, now);
        var labelByKey = await SeedProjectLabelsAsync(projectByCode, now);
        var sprintByKey = await SeedSprintsAsync(projectByCode, now);
        var taskByKey = await SeedTasksAsync(projectByCode, sprintByKey, userByEmail, now);

        await SeedTaskCollaborationAsync(taskByKey, labelByKey, userByEmail, now);
        await SeedKnowledgeAndOperationsAsync(projectByCode, taskByKey, organization, userByEmail, now);
        await EnsurePresentationDemoSeedAsync();
        await EnsureAiNativeDemoEvidenceAsync();
    }

    private static List<User> CreateDemoUsers(string adminPassword, string userPassword, DateTimeOffset now)
    {
        return
        [
            new() { FullName = "Quản trị viên Qaly", Email = "admin@qaly.dev", PasswordHash = HashPassword(adminPassword), Role = "Admin", IsActive = true, AvatarUrl = "/images/avatars/demo-admin.png", CreatedAt = now.AddDays(-80) },
            new() { FullName = "Nguyễn Minh Anh", Email = "minh.anh@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Manager", IsActive = true, AvatarUrl = "/images/avatars/minh-anh.png", CreatedAt = now.AddDays(-78) },
            new() { FullName = "Trần Bảo Ngọc", Email = "bao.ngoc@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Manager", IsActive = true, AvatarUrl = "/images/avatars/bao-ngoc.png", CreatedAt = now.AddDays(-77) },
            new() { FullName = "Lê Quốc Huy", Email = "quoc.huy@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/quoc-huy.png", CreatedAt = now.AddDays(-76) },
            new() { FullName = "Phạm Thu Hà", Email = "thu.ha@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/thu-ha.png", CreatedAt = now.AddDays(-75) },
            new() { FullName = "Đặng Gia Khang", Email = "gia.khang@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/gia-khang.png", CreatedAt = now.AddDays(-74) },
            new() { FullName = "Vũ Linh Chi", Email = "linh.chi@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/linh-chi.png", CreatedAt = now.AddDays(-73) },
            new() { FullName = "Hoàng Tuấn Kiệt", Email = "tuan.kiet@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/tuan-kiet.png", CreatedAt = now.AddDays(-72) },
            new() { FullName = "Bùi Mai Phương", Email = "mai.phuong@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/mai-phuong.png", CreatedAt = now.AddDays(-71) },
            new() { FullName = "Đỗ Thanh Tâm", Email = "thanh.tam@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/thanh-tam.png", CreatedAt = now.AddDays(-70) },
            new() { FullName = "Ngô Việt Long", Email = "viet.long@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/viet-long.png", CreatedAt = now.AddDays(-69) },
            new() { FullName = "Dương Yến Nhi", Email = "yen.nhi@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true, AvatarUrl = "/images/avatars/yen-nhi.png", CreatedAt = now.AddDays(-68) }
        ];
    }

    private async Task SeedGroupCollaborationAsync(List<WorkGroup> groups, Dictionary<string, User> users, DateTimeOffset now)
    {
        User U(string email) => users[email];
        var novaGroup = groups.Single(group => group.Name == "Nova Retail Pilot War Room");
        var productGroup = groups.Single(group => group.Name == "Qaly Product & Engineering");

        var novaMembers = new[]
        {
            ("minh.anh@qaly.dev", "Owner"),
            ("bao.ngoc@qaly.dev", "Manager"),
            ("quoc.huy@qaly.dev", "Member"),
            ("thu.ha@qaly.dev", "Member"),
            ("gia.khang@qaly.dev", "Member"),
            ("mai.phuong@qaly.dev", "Member"),
            ("yen.nhi@qaly.dev", "Viewer")
        };

        var productMembers = new[]
        {
            ("admin@qaly.dev", "Owner"),
            ("minh.anh@qaly.dev", "Manager"),
            ("linh.chi@qaly.dev", "Member"),
            ("tuan.kiet@qaly.dev", "Member"),
            ("thanh.tam@qaly.dev", "Member"),
            ("viet.long@qaly.dev", "Member"),
            ("yen.nhi@qaly.dev", "Member")
        };

        await _context.WorkGroupMembers.AddRangeAsync(novaMembers.Select((member, index) => new WorkGroupMember
        {
            WorkGroupId = novaGroup.Id,
            UserId = U(member.Item1).Id,
            Role = member.Item2,
            JoinedAt = now.AddDays(-46 + index),
            CreatedAt = now.AddDays(-46 + index)
        }));

        await _context.WorkGroupMembers.AddRangeAsync(productMembers.Select((member, index) => new WorkGroupMember
        {
            WorkGroupId = productGroup.Id,
            UserId = U(member.Item1).Id,
            Role = member.Item2,
            JoinedAt = now.AddDays(-60 + index),
            CreatedAt = now.AddDays(-60 + index)
        }));

        var messages = new List<GroupMessage>
        {
            new()
            {
                WorkGroupId = novaGroup.Id,
                UserId = U("minh.anh@qaly.dev").Id,
                Content = "Demo hôm nay tập trung vào 3 điểm: tiến độ pilot, rủi ro tồn kho và cách Erumi trả lời bằng dữ liệu realtime.",
                IsPinned = true,
                PinnedAt = now.AddDays(-3),
                PinnedByUserId = U("bao.ngoc@qaly.dev").Id,
                ReactionSummaryJson = "[]",
                CreatedAt = now.AddDays(-3).AddHours(2)
            },
            new()
            {
                WorkGroupId = novaGroup.Id,
                UserId = U("quoc.huy@qaly.dev").Id,
                Content = "API đồng bộ POS đã qua staging, còn 2 cửa hàng cần xác nhận mapping mã SKU.",
                ReactionSummaryJson = "[]",
                CreatedAt = now.AddDays(-2).AddHours(9)
            },
            new()
            {
                WorkGroupId = productGroup.Id,
                UserId = U("linh.chi@qaly.dev").Id,
                Content = "Bản build Erumi local đã phân biệt câu hỏi tổng quan, truy vấn task và câu hỏi cần khuyến nghị hành động.",
                IsPinned = true,
                PinnedAt = now.AddDays(-1).AddHours(4),
                PinnedByUserId = U("admin@qaly.dev").Id,
                ReactionSummaryJson = "[]",
                CreatedAt = now.AddDays(-1).AddHours(3)
            },
            new()
            {
                WorkGroupId = productGroup.Id,
                UserId = U("tuan.kiet@qaly.dev").Id,
                Content = "Đã thêm dashboard seed để khách hàng thấy dữ liệu rõ hơn ngay lần mở đầu.",
                CreatedAt = now.AddHours(-18)
            }
        };
        await _context.GroupMessages.AddRangeAsync(messages);
        await _context.SaveChangesAsync();

        await _context.GroupMessageUserStates.AddRangeAsync(
            new GroupMessageUserState { GroupMessageId = messages[1].Id, UserId = U("yen.nhi@qaly.dev").Id, HiddenAt = now.AddHours(-6), CreatedAt = now.AddHours(-6) },
            new GroupMessageUserState { GroupMessageId = messages[3].Id, UserId = U("bao.ngoc@qaly.dev").Id, CreatedAt = now.AddHours(-5) });

        await _context.GroupAttachments.AddRangeAsync(
            new GroupAttachment
            {
                WorkGroupId = novaGroup.Id,
                UploadedById = U("bao.ngoc@qaly.dev").Id,
                FileName = "nova-retail-pilot-brief.pdf",
                FilePath = "/uploads/demo/groups/nova-retail-pilot-brief.pdf",
                ContentType = "application/pdf",
                FileSize = 842_128,
                CreatedAt = now.AddDays(-5)
            },
            new GroupAttachment
            {
                WorkGroupId = productGroup.Id,
                UploadedById = U("linh.chi@qaly.dev").Id,
                FileName = "erumi-local-intent-notes.md",
                FilePath = "/uploads/demo/groups/erumi-local-intent-notes.md",
                ContentType = "text/markdown",
                FileSize = 24_680,
                CreatedAt = now.AddDays(-2)
            });

        var poll = new GroupPoll
        {
            GroupId = novaGroup.Id,
            CreatedByUserId = U("minh.anh@qaly.dev").Id,
            Question = "Mốc demo nào nên được ưu tiên cho khách hàng Nova Retail?",
            AllowMultiple = false,
            Status = GroupPollStatus.Open,
            ExpiredAt = now.AddDays(3),
            CreatedAt = now.AddDays(-2)
        };
        await _context.GroupPolls.AddAsync(poll);
        await _context.SaveChangesAsync();

        var pollOptions = new List<GroupPollOption>
        {
            new() { PollId = poll.Id, Content = "Tình trạng pilot theo cửa hàng", SortOrder = 1, CreatedAt = now.AddDays(-2) },
            new() { PollId = poll.Id, Content = "Rủi ro task trễ hạn", SortOrder = 2, CreatedAt = now.AddDays(-2) },
            new() { PollId = poll.Id, Content = "Hỏi đáp Erumi bằng dữ liệu realtime", SortOrder = 3, CreatedAt = now.AddDays(-2) }
        };
        await _context.GroupPollOptions.AddRangeAsync(pollOptions);
        await _context.SaveChangesAsync();

        await _context.GroupPollVotes.AddRangeAsync(
            new GroupPollVote { PollId = poll.Id, OptionId = pollOptions[2].Id, UserId = U("minh.anh@qaly.dev").Id, CreatedAt = now.AddDays(-1) },
            new GroupPollVote { PollId = poll.Id, OptionId = pollOptions[2].Id, UserId = U("bao.ngoc@qaly.dev").Id, CreatedAt = now.AddDays(-1).AddHours(1) },
            new GroupPollVote { PollId = poll.Id, OptionId = pollOptions[1].Id, UserId = U("quoc.huy@qaly.dev").Id, CreatedAt = now.AddDays(-1).AddHours(2) },
            new GroupPollVote { PollId = poll.Id, OptionId = pollOptions[0].Id, UserId = U("thu.ha@qaly.dev").Id, CreatedAt = now.AddDays(-1).AddHours(3) });

        await _context.GroupMeetingSessions.AddRangeAsync(
            new GroupMeetingSession
            {
                WorkGroupId = novaGroup.Id,
                StartedByUserId = U("minh.anh@qaly.dev").Id,
                Provider = "Meetily",
                RoomId = "nova-pilot-weekly-202606",
                JoinUrl = "https://demo.qaly.local/meet/nova-pilot-weekly",
                Status = "Ended",
                StartedAt = now.AddDays(-1).AddHours(-2),
                EndedAt = now.AddDays(-1).AddHours(-1),
                TranscriptSourceId = "meetily:nova-pilot-weekly-202606",
                Summary = "Chốt dữ liệu demo: 3 task có rủi ro, 1 task cần quyết định từ khách hàng, POS staging ổn định.",
                CreatedAt = now.AddDays(-1).AddHours(-2)
            },
            new GroupMeetingSession
            {
                WorkGroupId = productGroup.Id,
                StartedByUserId = U("admin@qaly.dev").Id,
                Provider = "External",
                RoomId = "qaly-erumi-local-demo",
                JoinUrl = "https://demo.qaly.local/meet/erumi-local",
                Status = "Active",
                StartedAt = now.AddMinutes(-35),
                Summary = "Phiên thử nghiệm Erumi local với dữ liệu seed realtime.",
                CreatedAt = now.AddMinutes(-35)
            });

        await _context.GroupInvitations.AddAsync(new GroupInvitation
        {
            GroupId = novaGroup.Id,
            Email = "customer.observer@novaretail.example",
            Token = "demo-nova-observer-2026",
            Status = GroupInvitationStatus.Pending,
            ExpiredAt = now.AddDays(7),
            CreatedAt = now.AddHours(-12)
        });

        await _context.SaveChangesAsync();
    }

    private async Task<Dictionary<string, Project>> SeedProjectsAndMembersAsync(
        Organization organization,
        List<WorkGroup> groups,
        Dictionary<string, User> users,
        DateTimeOffset now)
    {
        User U(string email) => users[email];
        var novaGroup = groups.Single(group => group.Name == "Nova Retail Pilot War Room");
        var productGroup = groups.Single(group => group.Name == "Qaly Product & Engineering");

        var projects = new List<Project>
        {
            new()
            {
                Name = "Qaly Work OS - Customer Demo",
                Code = "qaly-workos-demo",
                Description = "Dự án chính dùng để trình bày workflow Qaly: quản lý task, phân quyền, timeline, bình luận, evidence và báo cáo tiến độ.",
                LogoUrl = "/images/projects/qaly-workos-demo.png",
                Status = "Active",
                OwnerId = U("admin@qaly.dev").Id,
                OrganizationId = organization.Id,
                SourceGroupId = productGroup.Id,
                StartDate = now.AddDays(-45),
                EndDate = now.AddDays(45),
                CreatedAt = now.AddDays(-45)
            },
            new()
            {
                Name = "Erumi Local Analytics Engine",
                Code = "erumi-local-analytics",
                Description = "Thay thế phản hồi AI cloud bằng bộ phân tích local đọc dữ liệu realtime từ database, nhận diện intent và trả lời đúng ngữ cảnh.",
                LogoUrl = "/images/projects/erumi-local-analytics.png",
                Status = "Active",
                OwnerId = U("linh.chi@qaly.dev").Id,
                OrganizationId = organization.Id,
                SourceGroupId = productGroup.Id,
                StartDate = now.AddDays(-20),
                EndDate = now.AddDays(25),
                CreatedAt = now.AddDays(-20)
            },
            new()
            {
                Name = "Nova Retail Pilot Rollout",
                Code = "nova-retail-pilot",
                Description = "Triển khai Qaly cho chuỗi bán lẻ Nova Retail tại 12 cửa hàng, có dữ liệu POS, checklist vận hành và các task rủi ro để demo dashboard.",
                LogoUrl = "/images/projects/nova-retail.png",
                Status = "Active",
                OwnerId = U("minh.anh@qaly.dev").Id,
                OrganizationId = organization.Id,
                SourceGroupId = novaGroup.Id,
                StartDate = now.AddDays(-35),
                EndDate = now.AddDays(30),
                CreatedAt = now.AddDays(-35)
            },
            new()
            {
                Name = "Field Ops Mobile App",
                Code = "field-ops-mobile",
                Description = "Ứng dụng mobile cho đội vận hành hiện trường: nhận checklist, upload bằng chứng, đồng bộ offline và báo cáo thời gian thực.",
                LogoUrl = "/images/projects/field-ops-mobile.png",
                Status = "Active",
                OwnerId = U("tuan.kiet@qaly.dev").Id,
                OrganizationId = organization.Id,
                StartDate = now.AddDays(-18),
                EndDate = now.AddDays(52),
                CreatedAt = now.AddDays(-18)
            },
            new()
            {
                Name = "Ops & Compliance Readiness",
                Code = "ops-compliance-readiness",
                Description = "Bộ chuẩn bị vận hành trước khi demo khách hàng enterprise: audit log, quyền riêng tư, webhook, API key và kiểm soát chi phí AI.",
                LogoUrl = "/images/projects/ops-compliance.png",
                Status = "Active",
                OwnerId = U("thanh.tam@qaly.dev").Id,
                OrganizationId = organization.Id,
                StartDate = now.AddDays(-12),
                EndDate = now.AddDays(38),
                CreatedAt = now.AddDays(-12)
            },
            new()
            {
                Name = "Legacy CRM Cleanup",
                Code = "legacy-crm-cleanup",
                Description = "Dự án đã hoàn tất dùng để demo bộ lọc archived/completed và dữ liệu lịch sử cho báo cáo.",
                LogoUrl = "/images/projects/legacy-crm.png",
                Status = "Archived",
                OwnerId = U("bao.ngoc@qaly.dev").Id,
                OrganizationId = organization.Id,
                StartDate = now.AddDays(-120),
                EndDate = now.AddDays(-20),
                CreatedAt = now.AddDays(-120)
            }
        };

        await _context.Projects.AddRangeAsync(projects);
        await _context.SaveChangesAsync();

        var members = new List<ProjectMember>();
        void AddMembers(Project project, params (string Email, string Role)[] entries)
        {
            foreach (var (email, role) in entries)
            {
                members.Add(new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = U(email).Id,
                    Role = role,
                    JoinedAt = project.StartDate?.AddDays(1) ?? now.AddDays(-10),
                    CanViewProjectTimeline = ProjectRoleRules.CanManageProject(role),
                    CanViewTaskRisk = ProjectRoleRules.CanManageProject(role),
                    CanNudgeAssignee = ProjectRoleRules.CanManageProject(role),
                    CanViewUnseenTaskSignal = ProjectRoleRules.CanManageProject(role),
                    CreatedAt = project.StartDate?.AddDays(1) ?? now.AddDays(-10)
                });
            }
        }

        var projectByCode = projects.ToDictionary(project => project.Code, StringComparer.OrdinalIgnoreCase);
        // The customer demo project carries one account per project role so the whole permission
        // matrix can be walked live instead of described.
        AddMembers(projectByCode["qaly-workos-demo"],
            ("admin@qaly.dev", "Owner"),
            ("minh.anh@qaly.dev", "Manager"),
            ("bao.ngoc@qaly.dev", "ScrumMaster"),
            ("linh.chi@qaly.dev", "Developer"),
            ("quoc.huy@qaly.dev", "Developer"),
            ("tuan.kiet@qaly.dev", "Tester"),
            ("thanh.tam@qaly.dev", "Reviewer"),
            ("mai.phuong@qaly.dev", "Member"),
            ("yen.nhi@qaly.dev", "Viewer"),
            ("viet.long@qaly.dev", "Customer"));
        AddMembers(projectByCode["erumi-local-analytics"],
            ("linh.chi@qaly.dev", "Owner"),
            ("admin@qaly.dev", "Manager"),
            ("quoc.huy@qaly.dev", "Member"),
            ("thanh.tam@qaly.dev", "Member"),
            ("viet.long@qaly.dev", "Member"));
        AddMembers(projectByCode["nova-retail-pilot"],
            ("minh.anh@qaly.dev", "Owner"),
            ("bao.ngoc@qaly.dev", "Manager"),
            ("quoc.huy@qaly.dev", "Member"),
            ("thu.ha@qaly.dev", "Member"),
            ("gia.khang@qaly.dev", "Member"),
            ("mai.phuong@qaly.dev", "Member"),
            ("yen.nhi@qaly.dev", "Viewer"));
        AddMembers(projectByCode["field-ops-mobile"],
            ("tuan.kiet@qaly.dev", "Owner"),
            ("gia.khang@qaly.dev", "Member"),
            ("linh.chi@qaly.dev", "Member"),
            ("mai.phuong@qaly.dev", "Member"));
        AddMembers(projectByCode["ops-compliance-readiness"],
            ("thanh.tam@qaly.dev", "Owner"),
            ("admin@qaly.dev", "Manager"),
            ("viet.long@qaly.dev", "Member"),
            ("bao.ngoc@qaly.dev", "Member"));
        AddMembers(projectByCode["legacy-crm-cleanup"],
            ("bao.ngoc@qaly.dev", "Owner"),
            ("thu.ha@qaly.dev", "Member"));

        await _context.ProjectMembers.AddRangeAsync(members);
        await _context.SaveChangesAsync();

        LogSeededProject(_logger, "Qaly Demo Portfolio", projects.Count);
        return projectByCode;
    }

    private async Task<Dictionary<string, ProjectLabel>> SeedProjectLabelsAsync(Dictionary<string, Project> projects, DateTimeOffset now)
    {
        var labels = new List<ProjectLabel>();
        void Add(string projectCode, string name, string color)
        {
            labels.Add(new ProjectLabel
            {
                ProjectId = projects[projectCode].Id,
                Name = name,
                Color = color,
                CreatedAt = now.AddDays(-15)
            });
        }

        foreach (var projectCode in projects.Keys)
        {
            Add(projectCode, "Customer Demo", "#2563EB");
            Add(projectCode, "High Risk", "#DC2626");
            Add(projectCode, "Backend", "#0F766E");
            Add(projectCode, "Frontend", "#7C3AED");
            Add(projectCode, "Data", "#EA580C");
            Add(projectCode, "Operations", "#475569");
        }

        await _context.ProjectLabels.AddRangeAsync(labels);
        await _context.SaveChangesAsync();

        return labels.ToDictionary(label => $"{projects.Single(pair => pair.Value.Id == label.ProjectId).Key}:{label.Name}", StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Sprint>> SeedSprintsAsync(Dictionary<string, Project> projects, DateTimeOffset now)
    {
        var sprints = new List<Sprint>();
        void Add(string key, string projectCode, string name, string status, int startOffset, int endOffset, string goal)
        {
            sprints.Add(new Sprint
            {
                ProjectId = projects[projectCode].Id,
                Name = name,
                Status = status,
                StartDate = now.AddDays(startOffset),
                EndDate = now.AddDays(endOffset),
                Goal = goal,
                CreatedAt = now.AddDays(startOffset)
            });
        }

        Add("workos-current", "qaly-workos-demo", "Sprint 06 - Demo polish", "Active", -8, 6, "Hoàn thiện trải nghiệm demo và dữ liệu báo cáo cho khách hàng.");
        Add("workos-next", "qaly-workos-demo", "Sprint 07 - Enterprise handoff", "Planning", 7, 21, "Chuẩn bị tài liệu bàn giao và checklist enterprise.");
        Add("erumi-current", "erumi-local-analytics", "Sprint Local Intelligence", "Active", -6, 8, "Phân loại intent, truy vấn DB realtime và phản hồi tự nhiên.");
        Add("nova-current", "nova-retail-pilot", "Pilot Wave 2", "Active", -10, 4, "Đưa 8/12 cửa hàng lên quy trình vận hành mới.");
        Add("mobile-current", "field-ops-mobile", "Offline Evidence", "Active", -5, 9, "Đồng bộ checklist offline và bằng chứng ảnh.");
        Add("ops-current", "ops-compliance-readiness", "Compliance Pack", "Active", -4, 10, "Hoàn thiện log, webhook, privacy consent và policy AI.");
        Add("legacy-closed", "legacy-crm-cleanup", "Closure", "Completed", -55, -25, "Chốt dữ liệu lịch sử và lưu trữ.");

        await _context.Set<Sprint>().AddRangeAsync(sprints);
        await _context.SaveChangesAsync();

        return new Dictionary<string, Sprint>(StringComparer.OrdinalIgnoreCase)
        {
            ["workos-current"] = sprints[0],
            ["workos-next"] = sprints[1],
            ["erumi-current"] = sprints[2],
            ["nova-current"] = sprints[3],
            ["mobile-current"] = sprints[4],
            ["ops-current"] = sprints[5],
            ["legacy-closed"] = sprints[6]
        };
    }

    private async Task<Dictionary<string, TaskItem>> SeedTasksAsync(
        Dictionary<string, Project> projects,
        Dictionary<string, Sprint> sprints,
        Dictionary<string, User> users,
        DateTimeOffset now)
    {
        User U(string email) => users[email];
        var tasks = new Dictionary<string, TaskItem>(StringComparer.OrdinalIgnoreCase);

        TaskItem Add(
            string key,
            string projectCode,
            string sprintKey,
            string title,
            string description,
            string status,
            string priority,
            string assigneeEmail,
            string reporterEmail,
            int startOffset,
            int dueOffset,
            int estimate,
            int? actual,
            int sortOrder,
            bool pinned = false,
            bool contributesToProgress = true)
        {
            var task = new TaskItem
            {
                ProjectId = projects[projectCode].Id,
                SprintId = sprints[sprintKey].Id,
                Title = title,
                Description = description,
                Status = status,
                Priority = priority,
                AssigneeId = U(assigneeEmail).Id,
                ReporterId = U(reporterEmail).Id,
                StartDate = now.AddDays(startOffset),
                DueDate = now.AddDays(dueOffset),
                EstimatedHours = estimate,
                ActualHours = actual,
                SortOrder = sortOrder,
                IsPinned = pinned,
                ContributesToProgress = contributesToProgress,
                UpvoteCount = priority is "High" or "Critical" ? 3 : 1,
                CreatedAt = now.AddDays(startOffset)
            };
            tasks[key] = task;
            return task;
        }

        Add("workos-kpi-dashboard", "qaly-workos-demo", "workos-current",
            "Chuẩn hóa dashboard demo theo dữ liệu thật",
            "Sắp xếp lại các widget tiến độ, task rủi ro, tải team và hoạt động gần đây để khách hàng nhìn thấy giá trị trong 30 giây đầu.",
            "InReview", "High", "bao.ngoc@qaly.dev", "admin@qaly.dev", -7, 2, 18, 15, 10, pinned: true);
        Add("workos-evidence-flow", "qaly-workos-demo", "workos-current",
            "Hoàn thiện luồng upload evidence cho task",
            "Task demo phải có file bằng chứng, trạng thái duyệt và ghi chú reviewer để trình bày quy trình kiểm soát chất lượng.",
            "InProgress", "Medium", "tuan.kiet@qaly.dev", "minh.anh@qaly.dev", -6, 5, 12, 7, 20);
        Add("workos-role-matrix", "qaly-workos-demo", "workos-current",
            "Rà soát matrix phân quyền cho manager và viewer",
            "Kiểm tra các quyền xem timeline, rủi ro task, nudge assignee và tín hiệu chưa xem task.",
            "Done", "High", "admin@qaly.dev", "bao.ngoc@qaly.dev", -12, -2, 10, 9, 30);
        Add("workos-export-pack", "qaly-workos-demo", "workos-next",
            "Chuẩn bị gói export báo cáo sau demo",
            "Tạo dữ liệu mẫu cho báo cáo tổng hợp dự án, chi tiết task và phụ lục bình luận.",
            "Todo", "Medium", "yen.nhi@qaly.dev", "admin@qaly.dev", 7, 18, 14, null, 40);

        Add("erumi-intent-router", "erumi-local-analytics", "erumi-current",
            "Phân loại intent câu hỏi Erumi",
            "Nhận diện câu hỏi tổng quan, truy vấn task, người phụ trách, rủi ro, deadline và câu hỏi cần phản hồi ngắn.",
            "Done", "Critical", "linh.chi@qaly.dev", "admin@qaly.dev", -10, -1, 20, 22, 10, pinned: true);
        Add("erumi-db-snapshot", "erumi-local-analytics", "erumi-current",
            "Tạo snapshot realtime từ database cho phân tích local",
            "Đọc project, task, assignee, due date, comment và attention signal theo quyền người dùng để Erumi trả lời đúng dữ liệu.",
            "InProgress", "Critical", "quoc.huy@qaly.dev", "linh.chi@qaly.dev", -6, 1, 24, 16, 20, pinned: true);
        Add("erumi-answer-style", "erumi-local-analytics", "erumi-current",
            "Tinh chỉnh style phản hồi theo loại câu hỏi",
            "Câu hỏi chào hỏi trả lời ngắn; câu hỏi phân tích mới đưa số liệu; câu hỏi rủi ro trả lời kèm hành động đề xuất.",
            "InReview", "High", "thanh.tam@qaly.dev", "linh.chi@qaly.dev", -4, 3, 16, 11, 30);
        Add("erumi-cache-metrics", "erumi-local-analytics", "erumi-current",
            "Ghi nhận latency và cache hit cho Erumi local",
            "Lưu ledger chi phí/latency giả lập để màn AI governance có dữ liệu demo.",
            "Todo", "Medium", "viet.long@qaly.dev", "admin@qaly.dev", -1, 9, 10, null, 40);

        Add("nova-pos-mapping", "nova-retail-pilot", "nova-current",
            "Chốt mapping SKU cho 2 cửa hàng còn lại",
            "Cửa hàng Quận 3 và Thủ Đức còn lệch mã SKU giữa POS và master data, cần khách hàng xác nhận trước khi bật đồng bộ.",
            "InProgress", "Critical", "quoc.huy@qaly.dev", "minh.anh@qaly.dev", -8, -2, 14, 12, 10, pinned: true);
        Add("nova-store-training", "nova-retail-pilot", "nova-current",
            "Đào tạo ca trưởng sử dụng checklist Qaly",
            "Hoàn tất đào tạo cho 8 cửa hàng pilot, ghi nhận câu hỏi thường gặp và ảnh minh chứng từng ca.",
            "InReview", "High", "thu.ha@qaly.dev", "bao.ngoc@qaly.dev", -5, 1, 18, 14, 20);
        Add("nova-risk-dashboard", "nova-retail-pilot", "nova-current",
            "Cấu hình dashboard rủi ro vận hành Nova",
            "Hiển thị task quá hạn, task chưa có người xem trong 48h và task thiếu bằng chứng sau khi đánh dấu Done.",
            "Todo", "High", "gia.khang@qaly.dev", "minh.anh@qaly.dev", -2, 4, 16, null, 30);
        Add("nova-customer-signoff", "nova-retail-pilot", "nova-current",
            "Chuẩn bị biên bản nghiệm thu Wave 2",
            "Tổng hợp kết quả pilot, số cửa hàng hoạt động ổn định, blocker còn lại và quyết định go/no-go.",
            "Todo", "Medium", "mai.phuong@qaly.dev", "bao.ngoc@qaly.dev", 1, 6, 8, null, 40);

        Add("mobile-offline-queue", "field-ops-mobile", "mobile-current",
            "Xử lý hàng đợi đồng bộ offline",
            "Ứng dụng lưu checklist và evidence khi mất mạng, tự retry khi kết nối quay lại và hiển thị trạng thái đồng bộ rõ ràng.",
            "InProgress", "High", "tuan.kiet@qaly.dev", "linh.chi@qaly.dev", -5, 4, 22, 13, 10, pinned: true);
        Add("mobile-photo-compression", "field-ops-mobile", "mobile-current",
            "Nén ảnh evidence trước khi upload",
            "Giảm dung lượng ảnh nhưng vẫn đủ rõ để reviewer đọc được nhãn kệ và mã cửa hàng.",
            "Done", "Medium", "gia.khang@qaly.dev", "tuan.kiet@qaly.dev", -8, -1, 8, 7, 20);
        Add("mobile-qa-checklist", "field-ops-mobile", "mobile-current",
            "Test checklist hiện trường trên Android/iOS",
            "Kiểm thử nhập liệu dài, upload ảnh, mất mạng giữa chừng và khôi phục phiên làm việc.",
            "Todo", "Medium", "mai.phuong@qaly.dev", "tuan.kiet@qaly.dev", 0, 7, 12, null, 30);

        Add("ops-audit-log", "ops-compliance-readiness", "ops-current",
            "Bổ sung audit log cho thao tác nhạy cảm",
            "Ghi nhận tạo API key, đổi quyền thành viên, import meeting và xác nhận draft AI.",
            "InProgress", "High", "thanh.tam@qaly.dev", "admin@qaly.dev", -4, 3, 12, 8, 10);
        Add("ops-webhook-demo", "ops-compliance-readiness", "ops-current",
            "Tạo webhook demo cho sự kiện task.updated",
            "Webhook cần có log thành công/thất bại để khách hàng enterprise thấy khả năng tích hợp.",
            "Done", "Medium", "viet.long@qaly.dev", "thanh.tam@qaly.dev", -6, -1, 6, 5, 20);
        Add("ops-privacy-pack", "ops-compliance-readiness", "ops-current",
            "Chuẩn bị privacy consent cho dữ liệu meeting",
            "Tạo dữ liệu consent và data subject request để minh họa kiểm soát quyền riêng tư.",
            "Todo", "High", "bao.ngoc@qaly.dev", "thanh.tam@qaly.dev", -1, 8, 10, null, 30);

        Add("legacy-archive-export", "legacy-crm-cleanup", "legacy-closed",
            "Xuất archive dữ liệu CRM cũ",
            "Hoàn tất export danh bạ, lịch sử tương tác và mapping khách hàng sang cấu trúc Qaly.",
            "Done", "Low", "thu.ha@qaly.dev", "bao.ngoc@qaly.dev", -50, -28, 12, 10, 10);

        await _context.TaskItems.AddRangeAsync(tasks.Values);
        await _context.SaveChangesAsync();

        var assignments = new List<TaskAssignment>();
        void Assign(string taskKey, string assignedBy, params string[] assigneeEmails)
        {
            var task = tasks[taskKey];
            var primaryAssignee = users.Values.Single(user => task.AssigneeId.HasValue && user.Id == task.AssigneeId.Value).Email;
            foreach (var email in assigneeEmails.Prepend(primaryAssignee).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                assignments.Add(new TaskAssignment
                {
                    TaskItemId = task.Id,
                    UserId = U(email).Id,
                    AssignedByUserId = U(assignedBy).Id,
                    AssignedAt = task.CreatedAt.AddHours(2),
                    CreatedAt = task.CreatedAt.AddHours(2)
                });
            }
        }

        Assign("workos-kpi-dashboard", "admin@qaly.dev", "minh.anh@qaly.dev");
        Assign("workos-evidence-flow", "minh.anh@qaly.dev", "gia.khang@qaly.dev");
        Assign("erumi-db-snapshot", "linh.chi@qaly.dev", "viet.long@qaly.dev");
        Assign("nova-pos-mapping", "minh.anh@qaly.dev", "bao.ngoc@qaly.dev");
        Assign("nova-risk-dashboard", "minh.anh@qaly.dev", "thanh.tam@qaly.dev");
        Assign("mobile-offline-queue", "linh.chi@qaly.dev", "gia.khang@qaly.dev");
        Assign("ops-audit-log", "admin@qaly.dev", "viet.long@qaly.dev");

        await _context.TaskAssignments.AddRangeAsync(assignments);
        await _context.SaveChangesAsync();

        return tasks;
    }

    private async Task SeedTaskCollaborationAsync(
        Dictionary<string, TaskItem> tasks,
        Dictionary<string, ProjectLabel> labels,
        Dictionary<string, User> users,
        DateTimeOffset now)
    {
        User U(string email) => users[email];
        ProjectLabel L(string projectCode, string labelName) => labels[$"{projectCode}:{labelName}"];

        var taskLabels = new List<TaskLabel>
        {
            new() { TaskItemId = tasks["workos-kpi-dashboard"].Id, ProjectLabelId = L("qaly-workos-demo", "Customer Demo").Id, CreatedAt = now.AddDays(-6) },
            new() { TaskItemId = tasks["workos-evidence-flow"].Id, ProjectLabelId = L("qaly-workos-demo", "Operations").Id, CreatedAt = now.AddDays(-5) },
            new() { TaskItemId = tasks["erumi-intent-router"].Id, ProjectLabelId = L("erumi-local-analytics", "Data").Id, CreatedAt = now.AddDays(-9) },
            new() { TaskItemId = tasks["erumi-db-snapshot"].Id, ProjectLabelId = L("erumi-local-analytics", "Backend").Id, CreatedAt = now.AddDays(-5) },
            new() { TaskItemId = tasks["nova-pos-mapping"].Id, ProjectLabelId = L("nova-retail-pilot", "High Risk").Id, CreatedAt = now.AddDays(-6) },
            new() { TaskItemId = tasks["nova-risk-dashboard"].Id, ProjectLabelId = L("nova-retail-pilot", "Customer Demo").Id, CreatedAt = now.AddDays(-2) },
            new() { TaskItemId = tasks["mobile-offline-queue"].Id, ProjectLabelId = L("field-ops-mobile", "Frontend").Id, CreatedAt = now.AddDays(-4) },
            new() { TaskItemId = tasks["ops-webhook-demo"].Id, ProjectLabelId = L("ops-compliance-readiness", "Backend").Id, CreatedAt = now.AddDays(-5) }
        };
        await _context.TaskLabels.AddRangeAsync(taskLabels);

        await _context.TaskDependencies.AddRangeAsync(
            new TaskDependency { PredecessorId = tasks["erumi-intent-router"].Id, SuccessorId = tasks["erumi-db-snapshot"].Id, DependencyType = "FinishToStart", CreatedAt = now.AddDays(-6) },
            new TaskDependency { PredecessorId = tasks["workos-role-matrix"].Id, SuccessorId = tasks["workos-kpi-dashboard"].Id, DependencyType = "FinishToStart", CreatedAt = now.AddDays(-7) },
            new TaskDependency { PredecessorId = tasks["nova-pos-mapping"].Id, SuccessorId = tasks["nova-customer-signoff"].Id, DependencyType = "FinishToStart", CreatedAt = now.AddDays(-4) });

        var comments = new List<TaskComment>
        {
            new() { TaskItemId = tasks["erumi-db-snapshot"].Id, AuthorId = U("linh.chi@qaly.dev").Id, Content = "Ưu tiên câu trả lời ngắn cho câu hỏi chào hỏi, chỉ sinh bảng khi intent thực sự là phân tích.", UpvoteCount = 4, CreatedAt = now.AddDays(-3).AddHours(4) },
            new() { TaskItemId = tasks["erumi-db-snapshot"].Id, AuthorId = U("quoc.huy@qaly.dev").Id, Content = "Đã thêm lookup theo assignee, status, priority và due date. Cần kiểm tra thêm case project archived.", UpvoteCount = 2, CreatedAt = now.AddDays(-2).AddHours(6) },
            new() { TaskItemId = tasks["nova-pos-mapping"].Id, AuthorId = U("bao.ngoc@qaly.dev").Id, Content = "Khách hàng xác nhận sẽ gửi file SKU mới trước 16:00. Nếu trễ thì demo chuyển qua dữ liệu wave 1.", UpvoteCount = 3, CreatedAt = now.AddDays(-1).AddHours(2) },
            new() { TaskItemId = tasks["workos-kpi-dashboard"].Id, AuthorId = U("minh.anh@qaly.dev").Id, Content = "Dashboard hiện đã đủ số liệu để demo: tiến độ, workload, overdue và attention signal.", UpvoteCount = 5, CreatedAt = now.AddHours(-20) },
            new() { TaskItemId = tasks["ops-audit-log"].Id, AuthorId = U("thanh.tam@qaly.dev").Id, Content = "Nhớ demo phần audit khi tạo API key, khách hàng enterprise rất quan tâm đoạn này.", UpvoteCount = 2, CreatedAt = now.AddHours(-15) }
        };
        await _context.TaskComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        await _context.TaskComments.AddAsync(new TaskComment
        {
            TaskItemId = tasks["erumi-db-snapshot"].Id,
            AuthorId = U("admin@qaly.dev").Id,
            ParentCommentId = comments[0].Id,
            Content = "Đúng hướng. Khi người dùng hỏi 'hôm nay cần xem gì', ưu tiên task overdue và critical trước.",
            UpvoteCount = 3,
            CreatedAt = now.AddDays(-3).AddHours(5)
        });

        var file1 = new PhysicalFile
        {
            ContentHash = "demo_hash_storyline",
            FilePath = "/uploads/demo/projects/qaly-demo-storyline.pdf",
            FileSize = 1_204_912,
            ReferenceCount = 1
        };
        var file2 = new PhysicalFile
        {
            ContentHash = "demo_hash_evidence_flow",
            FilePath = "/uploads/demo/tasks/evidence-review-flow.png",
            FileSize = 428_640,
            ReferenceCount = 1
        };
        var file3 = new PhysicalFile
        {
            ContentHash = "demo_hash_checklist",
            FilePath = "/uploads/demo/tasks/store-training-checklist.xlsx",
            FileSize = 316_004,
            ReferenceCount = 1
        };
        var file4 = new PhysicalFile
        {
            ContentHash = "demo_hash_sku",
            FilePath = "/uploads/demo/comments/sku-mismatch-sample.csv",
            FileSize = 18_228,
            ReferenceCount = 1
        };

        await _context.TaskAttachments.AddRangeAsync(
            new TaskAttachment
            {
                ProjectId = tasks["workos-kpi-dashboard"].ProjectId,
                UploadedById = U("admin@qaly.dev").Id,
                FileName = "qaly-demo-storyline.pdf",
                PhysicalFile = file1,
                ContentType = "application/pdf",
                Scope = "Project",
                UploadedAt = now.AddDays(-4),
                CreatedAt = now.AddDays(-4)
            },
            new TaskAttachment
            {
                TaskItemId = tasks["workos-evidence-flow"].Id,
                UploadedById = U("tuan.kiet@qaly.dev").Id,
                FileName = "evidence-review-flow.png",
                PhysicalFile = file2,
                ContentType = "image/png",
                Scope = "Task",
                IsEvidence = true,
                EvidenceApprovalStatus = "Approved",
                EvidenceReviewedById = U("bao.ngoc@qaly.dev").Id,
                EvidenceReviewedAt = now.AddDays(-1),
                EvidenceReviewNote = "Ảnh đủ rõ, dùng tốt cho demo duyệt evidence.",
                UploadedAt = now.AddDays(-2),
                CreatedAt = now.AddDays(-2)
            },
            new TaskAttachment
            {
                TaskItemId = tasks["nova-store-training"].Id,
                UploadedById = U("thu.ha@qaly.dev").Id,
                FileName = "store-training-checklist.xlsx",
                PhysicalFile = file3,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Scope = "Task",
                IsEvidence = true,
                EvidenceApprovalStatus = "Pending",
                UploadedAt = now.AddHours(-18),
                CreatedAt = now.AddHours(-18)
            },
            new TaskAttachment
            {
                CommentId = comments[2].Id,
                UploadedById = U("bao.ngoc@qaly.dev").Id,
                FileName = "sku-mismatch-sample.csv",
                PhysicalFile = file4,
                ContentType = "text/csv",
                Scope = "Comment",
                UploadedAt = now.AddDays(-1).AddHours(3),
                CreatedAt = now.AddDays(-1).AddHours(3)
            });

        await _context.Votes.AddRangeAsync(
            new Vote { TargetType = "Task", TargetId = tasks["erumi-db-snapshot"].Id, UserId = U("admin@qaly.dev").Id, Value = 1, CreatedAt = now.AddDays(-2) },
            new Vote { TargetType = "Task", TargetId = tasks["nova-pos-mapping"].Id, UserId = U("bao.ngoc@qaly.dev").Id, Value = 1, CreatedAt = now.AddDays(-1) },
            new Vote { TargetType = "Comment", TargetId = comments[0].Id, UserId = U("quoc.huy@qaly.dev").Id, Value = 1, CreatedAt = now.AddDays(-2) });

        await _context.TaskViewEvents.AddRangeAsync(
            new TaskViewEvent { TaskItemId = tasks["nova-pos-mapping"].Id, UserId = U("minh.anh@qaly.dev").Id, ViewedAt = now.AddHours(-3), ViewCount = 7, CreatedAt = now.AddHours(-3) },
            new TaskViewEvent { TaskItemId = tasks["erumi-db-snapshot"].Id, UserId = U("admin@qaly.dev").Id, ViewedAt = now.AddHours(-2), ViewCount = 5, CreatedAt = now.AddHours(-2) },
            new TaskViewEvent { TaskItemId = tasks["workos-kpi-dashboard"].Id, UserId = U("yen.nhi@qaly.dev").Id, ViewedAt = now.AddHours(-8), ViewCount = 2, CreatedAt = now.AddHours(-8) });

        await _context.TaskAttentionSignals.AddRangeAsync(
            new TaskAttentionSignal { TaskItemId = tasks["nova-pos-mapping"].Id, UserId = U("quoc.huy@qaly.dev").Id, SignalType = "OverdueCritical", FirstDetectedAt = now.AddDays(-2), LastSentAt = now.AddHours(-4), CooldownHours = 12, CreatedAt = now.AddDays(-2) },
            new TaskAttentionSignal { TaskItemId = tasks["nova-risk-dashboard"].Id, UserId = U("gia.khang@qaly.dev").Id, SignalType = "UnseenByAssignee", FirstDetectedAt = now.AddDays(-1), LastSentAt = now.AddHours(-5), CooldownHours = 24, CreatedAt = now.AddDays(-1) },
            new TaskAttentionSignal { TaskItemId = tasks["workos-role-matrix"].Id, UserId = U("admin@qaly.dev").Id, SignalType = "Resolved", FirstDetectedAt = now.AddDays(-6), LastSentAt = now.AddDays(-5), ResolvedAt = now.AddDays(-2), CreatedAt = now.AddDays(-6) });

        await _context.TimeEntries.AddRangeAsync(
            new TimeEntry { TaskId = tasks["erumi-db-snapshot"].Id, UserId = U("quoc.huy@qaly.dev").Id, StartedAt = now.AddDays(-3).AddHours(1), EndedAt = now.AddDays(-3).AddHours(5), Note = "Thiết kế query snapshot cho Erumi.", CreatedAt = now.AddDays(-3).AddHours(1) },
            new TimeEntry { TaskId = tasks["workos-kpi-dashboard"].Id, UserId = U("bao.ngoc@qaly.dev").Id, StartedAt = now.AddDays(-2).AddHours(2), ManualMinutes = 210, Note = "Polish dashboard và kiểm tra số liệu demo.", CreatedAt = now.AddDays(-2).AddHours(2) },
            new TimeEntry { TaskId = tasks["nova-pos-mapping"].Id, UserId = U("quoc.huy@qaly.dev").Id, StartedAt = now.AddDays(-1).AddHours(3), EndedAt = now.AddDays(-1).AddHours(6), Note = "Đối soát SKU cửa hàng Quận 3.", CreatedAt = now.AddDays(-1).AddHours(3) });

        await _context.SaveChangesAsync();
    }

    private async Task SeedKnowledgeAndOperationsAsync(
        Dictionary<string, Project> projects,
        Dictionary<string, TaskItem> tasks,
        Organization organization,
        Dictionary<string, User> users,
        DateTimeOffset now)
    {
        User U(string email) => users[email];

        await _context.WikiPages.AddRangeAsync(
            new WikiPage
            {
                ProjectId = projects["qaly-workos-demo"].Id,
                AuthorId = U("admin@qaly.dev").Id,
                Title = DemoSeedMarkerTitle,
                Content = """
                    Bộ dữ liệu demo Qaly 2026.

                    Tài khoản admin: admin@qaly.dev
                    Tài khoản user mẫu: minh.anh@qaly.dev, linh.chi@qaly.dev, quoc.huy@qaly.dev

                    Các luồng nên demo:
                    1. Portfolio nhiều dự án với task quá hạn và task critical.
                    2. Erumi local trả lời theo dữ liệu realtime.
                    3. Evidence approval, comment, time log và attention signal.
                    4. Work group chat, poll, meeting import và webhook log.
                    """,
                IsPublic = false,
                Visibility = "internal",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new WikiPage
            {
                ProjectId = projects["erumi-local-analytics"].Id,
                AuthorId = U("linh.chi@qaly.dev").Id,
                Title = "Erumi Local Response Playbook",
                Content = "Erumi chỉ đưa bảng khi người dùng hỏi phân tích số liệu. Với câu hỏi chào hỏi hoặc hỏi nhanh, phản hồi ngắn và nêu đúng dữ liệu nổi bật.",
                IsPublic = true,
                Visibility = "customer_safe",
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddHours(-12)
            },
            new WikiPage
            {
                ProjectId = projects["nova-retail-pilot"].Id,
                AuthorId = U("bao.ngoc@qaly.dev").Id,
                Title = "Nova Retail Pilot Notes",
                Content = "Wave 2 tập trung 12 cửa hàng. Blocker chính là mapping SKU và lịch đào tạo ca trưởng. Demo nên mở task critical quá hạn trước.",
                IsPublic = true,
                Visibility = "customer_safe",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddHours(-18)
            });

        var importSession = new ImportSession
        {
            ProjectId = projects["nova-retail-pilot"].Id,
            UserId = U("bao.ngoc@qaly.dev").Id,
            FileName = "nova-wave-2-action-items.xlsx",
            TotalRows = 18,
            ImportedCount = 15,
            SkippedCount = 3,
            IsUndone = false,
            CreatedAt = now.AddDays(-4)
        };
        await _context.ImportSessions.AddAsync(importSession);
        await _context.SaveChangesAsync();

        tasks["nova-store-training"].ImportSessionId = importSession.Id;
        tasks["nova-customer-signoff"].ImportSessionId = importSession.Id;
        await _context.SaveChangesAsync();

        var completedJobCreatedAt = now.AddDays(-1).AddHours(-2);
        const string completedJobRequest = """{"meeting_id":"nova-pilot-weekly-202606","purpose":"meeting_action_extraction"}""";
        const string completedJobResult = """{"schema_id":"qaly.task_draft.v1","confidence":0.91,"items":[{"title":"Resolve remaining SKU mappings","priority":"Critical"},{"title":"Prepare Wave 2 acceptance record","priority":"Medium"}]}""";
        var aiJob = new AiJob
        {
            TenantId = organization.Id,
            ProjectId = projects["nova-retail-pilot"].Id,
            RequestedById = U("minh.anh@qaly.dev").Id,
            JobType = "meeting_action_extract",
            SourceType = "meeting",
            SourceId = "meetily:nova-pilot-weekly-202606",
            SchemaId = "meeting_action_extract.v4",
            SchemaVersion = "4.0",
            RequestJson = completedJobRequest,
            RequestHash = ComputeDemoHash(completedJobRequest),
            IdempotencyKey = "demo:meeting-action-extract:nova-pilot-weekly-202606",
            ProviderHint = "local",
            Sensitive = true,
            CloudEligible = false,
            PolicyDecisionJson = """{"allowed":true,"provider_class":"local","reason":"demo_policy"}""",
            PolicyCheckedAt = completedJobCreatedAt,
            Status = AiJobStatuses.Succeeded,
            ProgressPercent = 100,
            AvailableAt = completedJobCreatedAt,
            StartedAt = completedJobCreatedAt.AddSeconds(2),
            FinishedAt = completedJobCreatedAt.AddSeconds(6),
            AttemptCount = 1,
            MaxAttempts = 3,
            ResultJson = completedJobResult,
            ResultHash = ComputeDemoHash(completedJobResult),
            SelectedProvider = "LocalRules",
            SelectedModel = "erumi-local-intent-v1",
            EstimatedCostUsd = 0.000000m,
            ActualCostUsd = 0.000000m,
            PricingVersion = "demo-local-v1",
            CacheKey = "demo:meeting:nova-pilot-weekly-202606",
            CreatedAt = completedJobCreatedAt,
            UpdatedAt = completedJobCreatedAt.AddSeconds(6)
        };
        await _context.AiJobs.AddAsync(aiJob);
        await _context.SaveChangesAsync();

        await _context.AiJobDispatches.AddAsync(new AiJobDispatch
        {
            AiJobId = aiJob.Id,
            Priority = 50,
            AvailableAt = completedJobCreatedAt,
            DeliveryCount = 1,
            CompletedAt = aiJob.FinishedAt,
            CreatedAt = completedJobCreatedAt
        });
        await _context.AiJobSources.AddAsync(new AiJobSource
        {
            AiJobId = aiJob.Id,
            SourceType = "meeting",
            LegacySourceKey = aiJob.SourceId,
            SourceVersion = "demo-v1",
            SourceHash = "8f14e45fceea167a5a36dedd4bea2543",
            SourceTimestamp = completedJobCreatedAt,
            SortOrder = 0,
            CreatedAt = completedJobCreatedAt
        });
        await _context.SaveChangesAsync();

        var draft = new AiGeneratedDraft
        {
            AiJobId = aiJob.Id,
            ProjectId = projects["nova-retail-pilot"].Id,
            DraftType = "TaskDraft",
            PayloadJson = """{"schema_id":"qaly.task_draft.v1","confidence":0.91,"items":[{"title":"Chốt mapping SKU cho 2 cửa hàng còn lại","priority":"Critical"},{"title":"Chuẩn bị biên bản nghiệm thu Wave 2","priority":"Medium"}]}""",
            Status = AiDraftStatuses.Confirmed,
            ConfirmedById = U("minh.anh@qaly.dev").Id,
            ConfirmedAt = now.AddDays(-1).AddHours(-1),
            ConfirmAction = "CreateTasks",
            ConfirmationNote = "Dùng 2 action item làm task demo cho Nova Retail.",
            SchemaId = "qaly.task_draft.v1",
            Confidence = 0.91m,
            CreatedAt = now.AddDays(-1).AddHours(-2)
        };
        draft.PayloadJson = completedJobResult;
        draft.OriginalPayloadJson = completedJobResult;
        draft.WorkingPayloadJson = completedJobResult;
        await _context.AiGeneratedDrafts.AddAsync(draft);
        await _context.SaveChangesAsync();

        var meetingImport = new MeetingImport
        {
            ProjectId = projects["nova-retail-pilot"].Id,
            ImportedById = U("minh.anh@qaly.dev").Id,
            SourceProvider = "meetily",
            SourceId = "nova-pilot-weekly-202606",
            SourceHash = "8f14e45fceea167a5a36dedd4bea2543",
            Title = "Nova Retail Pilot Weekly",
            MeetingStartedAt = now.AddDays(-1).AddHours(-2),
            Summary = "Cuộc họp chốt blocker SKU, dashboard rủi ro và biên bản nghiệm thu Wave 2.",
            TranscriptText = "Minh Anh: SKU mapping còn lệch ở Quận 3 và Thủ Đức. Bảo Ngọc: cần biên bản nghiệm thu Wave 2 trước thứ Sáu.",
            ParticipantsJson = """["Nguyễn Minh Anh","Trần Bảo Ngọc","Lê Quốc Huy","Phạm Thu Hà"]""",
            RawPayloadJson = """{"source":"demo","duration_minutes":52}""",
            AiJobId = aiJob.Id,
            AiDraftId = draft.Id,
            CreatedAt = now.AddDays(-1).AddHours(-2)
        };
        await _context.MeetingImports.AddAsync(meetingImport);
        await _context.SaveChangesAsync();

        await _context.MeetingActionItemMappings.AddRangeAsync(
            new MeetingActionItemMapping
            {
                MeetingImportId = meetingImport.Id,
                ActionItemIndex = 0,
                TaskId = tasks["nova-pos-mapping"].Id,
                Status = "Created",
                SourceTitle = "Chốt mapping SKU cho 2 cửa hàng còn lại",
                SourcePriority = "Critical",
                SourceDueDate = tasks["nova-pos-mapping"].DueDate,
                SourceQuote = "SKU mapping còn lệch ở Quận 3 và Thủ Đức.",
                CreatedById = U("minh.anh@qaly.dev").Id,
                CreatedAt = now.AddDays(-1).AddHours(-1)
            },
            new MeetingActionItemMapping
            {
                MeetingImportId = meetingImport.Id,
                ActionItemIndex = 1,
                TaskId = tasks["nova-customer-signoff"].Id,
                Status = "Created",
                SourceTitle = "Chuẩn bị biên bản nghiệm thu Wave 2",
                SourcePriority = "Medium",
                SourceDueDate = tasks["nova-customer-signoff"].DueDate,
                SourceQuote = "Cần biên bản nghiệm thu Wave 2 trước thứ Sáu.",
                CreatedById = U("minh.anh@qaly.dev").Id,
                CreatedAt = now.AddDays(-1).AddHours(-1)
            });

        await _context.Notifications.AddRangeAsync(
            new Notification { UserId = U("quoc.huy@qaly.dev").Id, Message = "Task critical 'Chốt mapping SKU' đã quá hạn 2 ngày.", Type = "DueDateReminder", Tone = "warning", RelatedEntityType = "Task", RelatedEntityId = tasks["nova-pos-mapping"].Id, IsRead = false, IdempotencyKey = "demo:nova-pos-overdue", CreatedAt = now.AddHours(-4) },
            new Notification { UserId = U("gia.khang@qaly.dev").Id, Message = "Bạn chưa xem task 'Cấu hình dashboard rủi ro Nova'.", Type = "TaskAttentionNudge", Tone = "info", RelatedEntityType = "Task", RelatedEntityId = tasks["nova-risk-dashboard"].Id, IsRead = false, IdempotencyKey = "demo:nova-risk-unseen", CreatedAt = now.AddHours(-5) },
            new Notification { UserId = U("bao.ngoc@qaly.dev").Id, Message = "Evidence mới đang chờ duyệt trong task đào tạo cửa hàng.", Type = "ReviewCompleted", Tone = "info", RelatedEntityType = "Task", RelatedEntityId = tasks["nova-store-training"].Id, IsRead = true, IdempotencyKey = "demo:nova-training-evidence", CreatedAt = now.AddHours(-18) });

        await _context.AiProviderConfigs.AddRangeAsync(
            new AiProviderConfig { TenantId = organization.Id, ProviderName = "LocalRules", ModelName = "erumi-local-intent-v1", Purpose = "analytics_chat", IsEnabled = true, PriorityOrder = 1, MaxInputTokens = 4000, MaxOutputTokens = 900, CostInputPer1MUsd = 0m, CostOutputPer1MUsd = 0m, DataPolicy = "mock_only", CreatedAt = now.AddDays(-2) },
            new AiProviderConfig { TenantId = organization.Id, ProviderName = "Ollama", ModelName = "llama3.2:1b", Purpose = "draft_generation", IsEnabled = false, PriorityOrder = 20, MaxInputTokens = 8000, MaxOutputTokens = 1200, CostInputPer1MUsd = 0m, CostOutputPer1MUsd = 0m, DataPolicy = "no_cloud_sensitive", CreatedAt = now.AddDays(-12) });

        await _context.AiBudgetPolicies.AddAsync(new AiBudgetPolicy
        {
            TenantId = organization.Id,
            ProjectId = projects["erumi-local-analytics"].Id,
            DailyBudgetUsd = 1.50m,
            MonthlyBudgetUsd = 25.00m,
            WarnAtPercent = 75,
            HardStopEnabled = true,
            AllowCloudForSensitive = false,
            CreatedBy = U("admin@qaly.dev").Id,
            CreatedAt = now.AddDays(-12)
        });

        await _context.AiUsageLedger.AddRangeAsync(
            new AiUsageLedger { TenantId = organization.Id, ProjectId = projects["erumi-local-analytics"].Id, UserId = U("linh.chi@qaly.dev").Id, JobType = "analytics_chat", ProviderName = "LocalRules", ModelName = "erumi-local-intent-v1", InputTokens = 0, OutputTokens = 0, EstimatedCostUsd = 0m, LatencyMs = 46, Status = "succeeded", CacheHit = false, PromptHash = "demo-local-question-1", ResponseHash = "demo-local-answer-1", CreatedAt = now.AddHours(-3) },
            new AiUsageLedger { TenantId = organization.Id, ProjectId = projects["nova-retail-pilot"].Id, UserId = U("minh.anh@qaly.dev").Id, AiJobId = aiJob.Id, JobType = "meeting_action_extract", ProviderName = "LocalRules", ModelName = "erumi-local-intent-v1", InputTokens = 0, OutputTokens = 0, EstimatedCostUsd = 0m, ActualCostUsd = 0m, PricingVersion = "demo-local-v1", LatencyMs = 88, Status = "succeeded", CacheHit = true, PromptHash = aiJob.RequestHash, ResponseHash = aiJob.ResultHash!, CreatedAt = now.AddDays(-1) });

        await _context.AiPromptCache.AddAsync(new AiPromptCache
        {
            TenantId = organization.Id,
            ProjectId = projects["erumi-local-analytics"].Id,
            CacheKey = "demo:erumi:summary:nova-retail",
            JobType = "analytics_chat",
            SchemaId = "qaly.erumi.answer.v1",
            ProviderName = "LocalRules",
            ModelName = "erumi-local-intent-v1",
            RequestHash = "demo-request-hash",
            ResponseJson = """{"answer":"Nova Retail có 1 task critical quá hạn, 2 task cần theo dõi và 1 blocker từ khách hàng."}""",
            HitCount = 7,
            ExpiresAt = now.AddDays(5),
            CreatedAt = now.AddHours(-8)
        });

        const string queuedJobRequest = """{"scope":"demo","project":"ops-compliance-readiness"}""";
        var queuedJob = new AiJob
        {
            TenantId = organization.Id,
            ProjectId = projects["ops-compliance-readiness"].Id,
            RequestedById = U("thanh.tam@qaly.dev").Id,
            JobType = "progress_summary",
            SourceType = "project",
            SourceId = projects["ops-compliance-readiness"].Id.ToString(),
            SchemaId = "progress_summary.v4",
            SchemaVersion = "4.0",
            RequestJson = queuedJobRequest,
            RequestHash = ComputeDemoHash(queuedJobRequest),
            IdempotencyKey = "demo:progress-summary:ops-compliance-readiness",
            Sensitive = false,
            ProviderHint = "local",
            CloudEligible = false,
            PolicyDecisionJson = """{"allowed":true,"provider_class":"local","reason":"demo_policy"}""",
            PolicyCheckedAt = now.AddMinutes(-20),
            Status = AiJobStatuses.Queued,
            AvailableAt = now.AddMinutes(-20),
            MaxAttempts = 3,
            EstimatedCostUsd = 0m,
            MaximumCostUsd = 0m,
            CacheKey = "demo:progress-summary:ops-compliance-readiness",
            CreatedAt = now.AddMinutes(-20)
        };
        await _context.AiJobs.AddAsync(queuedJob);
        await _context.SaveChangesAsync();
        await _context.AiJobDispatches.AddAsync(new AiJobDispatch
        {
            AiJobId = queuedJob.Id,
            Priority = 50,
            AvailableAt = queuedJob.AvailableAt,
            CreatedAt = queuedJob.CreatedAt
        });
        await _context.AiJobSources.AddAsync(new AiJobSource
        {
            AiJobId = queuedJob.Id,
            SourceType = "project",
            SourceEntityId = projects["ops-compliance-readiness"].Id,
            SourceVersion = "demo-v1",
            SourceHash = ComputeDemoHash(queuedJobRequest),
            SourceTimestamp = queuedJob.CreatedAt,
            SortOrder = 0,
            CreatedAt = queuedJob.CreatedAt
        });

        await _context.AiAuditEvents.AddRangeAsync(
            new AiAuditEvent { TenantId = organization.Id, ProjectId = projects["erumi-local-analytics"].Id, ActorUserId = U("admin@qaly.dev").Id, EventType = "provider_config.updated", EntityType = "AiProviderConfig", BeforeJson = """{"enabled":false}""", AfterJson = """{"enabled":true}""", IpAddress = "127.0.0.1", UserAgent = "Qaly Demo Seeder", CreatedAt = now.AddDays(-2) },
            new AiAuditEvent { TenantId = organization.Id, ProjectId = projects["nova-retail-pilot"].Id, ActorUserId = U("minh.anh@qaly.dev").Id, EventType = "meeting_import.confirmed", EntityType = "MeetingImport", AfterJson = """{"created_tasks":2}""", IpAddress = "127.0.0.1", UserAgent = "Qaly Demo Seeder", CreatedAt = now.AddDays(-1) });

        var webhook = new WebhookSubscription
        {
            ProjectId = projects["ops-compliance-readiness"].Id,
            PayloadUrl = "https://hooks.customer.example/qaly/task-events",
            Secret = "demo-webhook-secret",
            Events = """["task.created","task.updated","task.completed"]""",
            IsActive = true,
            FailureCount = 1,
            CreatedAt = now.AddDays(-7)
        };
        await _context.WebhookSubscriptions.AddAsync(webhook);
        await _context.SaveChangesAsync();

        await _context.WebhookDeliveryLogs.AddRangeAsync(
            new WebhookDeliveryLog { WebhookId = webhook.Id, EventType = "task.updated", IdempotencyKey = "demo-wh-001", RequestPayload = """{"task":"ops-webhook-demo","status":"Done"}""", ResponseStatusCode = 200, ResponseBody = "ok", DurationMs = 148, AttemptCount = 1, IsSuccess = true, CreatedAt = now.AddDays(-1) },
            new WebhookDeliveryLog { WebhookId = webhook.Id, EventType = "task.updated", IdempotencyKey = "demo-wh-002", RequestPayload = """{"task":"nova-pos-mapping","priority":"Critical"}""", ResponseStatusCode = 503, ResponseBody = "temporary unavailable", DurationMs = 420, AttemptCount = 2, IsSuccess = false, CreatedAt = now.AddHours(-10) });

        await _context.ApiKeys.AddAsync(new ApiKey
        {
            UserId = U("admin@qaly.dev").Id,
            Name = "Demo Integration Key",
            KeyHash = HashPassword("qaly_demo_integration_key"),
            Prefix = "qaly_dem",
            Scopes = """["tasks:read","tasks:write","projects:read","webhooks:read"]""",
            ExpiresAt = now.AddMonths(3),
            LastUsedAt = now.AddHours(-6),
            IsRevoked = false,
            CreatedAt = now.AddDays(-6)
        });

        await _context.PrivacyConsents.AddRangeAsync(
            new PrivacyConsent { TenantId = organization.Id, ProjectId = projects["nova-retail-pilot"].Id, UserId = U("minh.anh@qaly.dev").Id, ConsentType = "meeting_import", Purpose = "Tạo action item từ transcript meeting demo.", ScopeJson = """{"provider":"meetily","retention_days":30}""", Status = "granted", GrantedAt = now.AddDays(-2), IpAddress = "127.0.0.1", UserAgent = "Qaly Demo Seeder", CreatedAt = now.AddDays(-2) },
            new PrivacyConsent { TenantId = organization.Id, ProjectId = projects["erumi-local-analytics"].Id, UserId = U("linh.chi@qaly.dev").Id, ConsentType = "ai_cloud_processing", Purpose = "Không dùng cloud cho dữ liệu nhạy cảm trong demo Erumi local.", ScopeJson = """{"cloud_allowed":false}""", Status = "revoked", GrantedAt = now.AddDays(-10), RevokedAt = now.AddDays(-2), IpAddress = "127.0.0.1", UserAgent = "Qaly Demo Seeder", CreatedAt = now.AddDays(-10) });

        await _context.DataSubjectRequests.AddAsync(new DataSubjectRequest
        {
            TenantId = organization.Id,
            ProjectId = projects["ops-compliance-readiness"].Id,
            RequesterUserId = U("yen.nhi@qaly.dev").Id,
            RequestType = "export",
            ScopeJson = """{"entities":["tasks","comments","time_entries"],"reason":"demo_customer_request"}""",
            Status = "approved",
            RequestedAt = now.AddDays(-3),
            ApprovedBy = U("thanh.tam@qaly.dev").Id,
            CompletedAt = now.AddDays(-1),
            EvidenceUri = "/uploads/demo/privacy/export-request-demo.zip",
            CreatedAt = now.AddDays(-3)
        });

        await _context.PushSubscriptions.AddAsync(new PushSubscription
        {
            UserId = U("quoc.huy@qaly.dev").Id,
            Endpoint = "https://push.demo.qaly.local/subscriptions/quoc-huy",
            P256dh = "demo-p256dh",
            Auth = "demo-auth",
            Device = "Chrome on Windows",
            LastUsedAt = now.AddHours(-4),
            CreatedAt = now.AddDays(-5)
        });

        await _context.VectorSyncOutbox.AddRangeAsync(
            new VectorSyncOutbox { EventType = "TaskUpdated", Payload = $$"""{"id":"{{tasks["erumi-db-snapshot"].Id}}","projectCode":"erumi-local-analytics"}""", RetryCount = 0, ProcessedAt = now.AddHours(-2), CreatedAt = now.AddHours(-3) },
            new VectorSyncOutbox { EventType = "WikiPageCreated", Payload = $$"""{"title":"{{DemoSeedMarkerTitle}}","projectCode":"qaly-workos-demo"}""", RetryCount = 1, ErrorMessage = "Vector sync disabled in local demo", CreatedAt = now.AddHours(-1) });

        await _context.AuditLogs.AddRangeAsync(
            new AuditLog { Action = "Create", EntityType = "ApiKey", EntityId = "Demo Integration Key", UserId = U("admin@qaly.dev").Id, ChangesJson = """{"scopes":["tasks:read","tasks:write"]}""", IpAddress = "127.0.0.1", Timestamp = now.AddDays(-6) },
            new AuditLog { Action = "StatusChange", EntityType = "TaskItem", EntityId = tasks["nova-pos-mapping"].Id.ToString(), UserId = U("minh.anh@qaly.dev").Id, ChangesJson = """{"from":"Todo","to":"InProgress","priority":"Critical"}""", IpAddress = "127.0.0.1", Timestamp = now.AddDays(-4) },
            new AuditLog { Action = "Update", EntityType = "ProjectMember", EntityId = "nova-retail-pilot:yen.nhi", UserId = U("bao.ngoc@qaly.dev").Id, ChangesJson = """{"role":"Viewer"}""", IpAddress = "127.0.0.1", Timestamp = now.AddDays(-2) });

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Extends the canonical rich demo with relational source evidence used by the
    /// member-skill, assignee-recommendation and portfolio-capacity AI-native flows.
    /// The extension is deliberately idempotent so it can enrich an existing demo
    /// database without replacing data that a user has edited.
    /// </summary>
    private async Task<bool> EnsureAiNativeDemoEvidenceAsync()
    {
        var organization = await _context.Organizations
            .SingleOrDefaultAsync(item => item.Code == "qaly-demo-2026");
        if (organization == null)
        {
            return false;
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;
        var users = await _context.Users
            .Where(item => item.Email.EndsWith("@qaly.dev"))
            .ToDictionaryAsync(item => item.Email, StringComparer.OrdinalIgnoreCase);
        var projects = await _context.Projects
            .IgnoreQueryFilters()
            .Where(item => item.OrganizationId == organization.Id && item.Status == "Active" && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase);

        var hasEffectiveRulebook = await _context.OrganizationWorkRuleSets.AnyAsync(item =>
            item.OrganizationId == organization.Id && item.Status == "active" &&
            (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= now) &&
            (!item.EffectiveUntil.HasValue || item.EffectiveUntil > now));
        if (!hasEffectiveRulebook && users.TryGetValue("admin@qaly.dev", out var rulebookOwner))
        {
            await _context.OrganizationWorkRuleSets.AddAsync(new OrganizationWorkRuleSet
            {
                OrganizationId = organization.Id,
                Version = 1,
                Status = "active",
                EffectiveFrom = now.AddDays(-30),
                RulesJson = """
                    [
                      {"ruleKey":"active_membership_required","category":"governance","enforcement":"block","description":"Requester must be an active organization member.","enabled":true},
                      {"ruleKey":"max_active_projects","category":"portfolio_capacity","enforcement":"block","description":"Protect organization capacity by limiting concurrent active projects.","numericValue":20,"unit":"projects","enabled":true},
                      {"ruleKey":"capacity_evidence_required","category":"staffing","enforcement":"block","description":"Staffing requires current capacity and availability evidence.","enabled":true}
                    ]
                    """,
                CreatedByUserId = rulebookOwner.Id,
                ActivatedByUserId = rulebookOwner.Id,
                ActivatedAt = now.AddDays(-30),
                Revision = 2,
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-30)
            });
            changed = true;
            await SaveIfChangedAsync();
        }

        var requiredProjectCodes = new[]
        {
            "qaly-workos-demo",
            "erumi-local-analytics",
            "nova-retail-pilot",
            "field-ops-mobile",
            "ops-compliance-readiness"
        };
        if (requiredProjectCodes.Any(code => !projects.ContainsKey(code)) || users.Count < 5)
        {
            return false;
        }

        const string novaEvidenceTitle = "Xác nhận dữ liệu POS Wave 1";
        var novaProject = projects["nova-retail-pilot"];
        var novaEvidenceTask = await _context.TaskItems
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ProjectId == novaProject.Id && item.Title == novaEvidenceTitle);
        if (novaEvidenceTask == null &&
            users.TryGetValue("quoc.huy@qaly.dev", out var novaContributor) &&
            users.TryGetValue("minh.anh@qaly.dev", out var novaOwner))
        {
            var activeSprint = await _context.Set<Sprint>()
                .Where(item => item.ProjectId == novaProject.Id && item.Status == "Active")
                .OrderByDescending(item => item.StartDate)
                .FirstOrDefaultAsync();
            novaEvidenceTask = new TaskItem
            {
                ProjectId = novaProject.Id,
                SprintId = activeSprint?.Id,
                Title = novaEvidenceTitle,
                Description = "Đối soát giao dịch POS với master data của 8 cửa hàng Wave 1, xác nhận sai lệch SKU đã được đóng và lưu biên bản bàn giao làm nguồn cho quyết định mở rộng pilot.",
                Status = "Done",
                Priority = "High",
                AssigneeId = novaContributor.Id,
                ReporterId = novaOwner.Id,
                StartDate = now.AddDays(-16),
                DueDate = now.AddDays(-9),
                EstimatedHours = 14,
                ActualHours = 13,
                SortOrder = 50,
                UpvoteCount = 3,
                CreatedAt = now.AddDays(-16),
                UpdatedAt = now.AddDays(-9)
            };
            await _context.TaskItems.AddAsync(novaEvidenceTask);
            await _context.TaskAssignments.AddAsync(new TaskAssignment
            {
                TaskItemId = novaEvidenceTask.Id,
                UserId = novaContributor.Id,
                AssignedByUserId = novaOwner.Id,
                AssignedAt = now.AddDays(-16),
                CreatedAt = now.AddDays(-16)
            });
            changed = true;
            await _context.SaveChangesAsync();
        }

        var skillDefinitions = new (string NormalizedName, string Name, string Description)[]
        {
            ("frontend-vue", "Frontend / Vue 3", "Thiết kế component Vue 3, state, accessibility và hành vi UI có thể kiểm thử."),
            ("backend-dotnet", "Backend / .NET APIs", "Xây dựng API ASP.NET Core, service domain, authorization và xử lý lỗi có contract."),
            ("database-efcore-sql", "Database / EF Core & SQL Server", "Thiết kế truy vấn, mapping EF Core, migration và tính toàn vẹn dữ liệu SQL Server."),
            ("qa-playwright", "QA Automation / Playwright", "Thiết kế kiểm thử browser, regression và evidence lặp lại được bằng Playwright."),
            ("security-auth-privacy", "Security / Authorization & Privacy", "Authorization, tenant isolation, audit và xử lý dữ liệu riêng tư theo policy."),
            ("ai-structured-llm", "AI Integration / Structured LLM", "Thiết kế prompt, structured output, grounding, provider routing và failure handling cho LLM."),
            ("mobile-offline-sync", "Mobile / Offline Sync", "Đồng bộ mobile khi mất mạng, retry an toàn, conflict handling và evidence upload."),
            ("devops-observability", "DevOps / Observability", "Telemetry, webhook, latency, health signal và điều tra lỗi vận hành."),
            ("data-retail-integration", "Data / Retail Integration", "Đối soát dữ liệu POS, master data, mapping SKU và chất lượng dữ liệu bán lẻ.")
        };
        var skills = await _context.OrganizationSkills
            .Where(item => item.OrganizationId == organization.Id)
            .ToDictionaryAsync(item => item.NormalizedName, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in skillDefinitions)
        {
            if (skills.ContainsKey(definition.NormalizedName))
            {
                continue;
            }

            var skill = new OrganizationSkill
            {
                OrganizationId = organization.Id,
                Name = definition.Name,
                NormalizedName = definition.NormalizedName,
                Description = definition.Description,
                IsActive = true,
                CreatedAt = now.AddDays(-30)
            };
            skills[definition.NormalizedName] = skill;
            await _context.OrganizationSkills.AddAsync(skill);
            changed = true;
        }
        await SaveIfChangedAsync();

        var allProjectIds = projects.Values.Select(item => item.Id).ToHashSet();
        var projectTasks = await _context.TaskItems
            .IgnoreQueryFilters()
            .Where(item => allProjectIds.Contains(item.ProjectId) && !item.IsDeleted)
            .ToListAsync();
        TaskItem? FindTask(string projectCode, string title) => projectTasks.SingleOrDefault(item =>
            item.ProjectId == projects[projectCode].Id && string.Equals(item.Title, title, StringComparison.Ordinal));

        var requirementDefinitions = new (string ProjectCode, string TaskTitle, string Skill, string Level)[]
        {
            ("qaly-workos-demo", "Rà soát matrix phân quyền cho manager và viewer", "security-auth-privacy", "Advanced"),
            ("qaly-workos-demo", "Rà soát matrix phân quyền cho manager và viewer", "qa-playwright", "Working"),
            ("erumi-local-analytics", "Phân loại intent câu hỏi Erumi", "ai-structured-llm", "Advanced"),
            ("erumi-local-analytics", "Phân loại intent câu hỏi Erumi", "backend-dotnet", "Working"),
            ("nova-retail-pilot", novaEvidenceTitle, "data-retail-integration", "Advanced"),
            ("nova-retail-pilot", novaEvidenceTitle, "devops-observability", "Working"),
            ("field-ops-mobile", "Nén ảnh evidence trước khi upload", "mobile-offline-sync", "Advanced"),
            ("field-ops-mobile", "Nén ảnh evidence trước khi upload", "frontend-vue", "Working"),
            ("ops-compliance-readiness", "Tạo webhook demo cho sự kiện task.updated", "backend-dotnet", "Working"),
            ("ops-compliance-readiness", "Tạo webhook demo cho sự kiện task.updated", "devops-observability", "Advanced"),
            ("qaly-workos-demo", "Chuẩn hóa dashboard demo theo dữ liệu thật", "frontend-vue", "Advanced"),
            ("qaly-workos-demo", "Chuẩn hóa dashboard demo theo dữ liệu thật", "data-retail-integration", "Familiar"),
            ("erumi-local-analytics", "Tạo snapshot realtime từ database cho phân tích local", "backend-dotnet", "Advanced"),
            ("erumi-local-analytics", "Tạo snapshot realtime từ database cho phân tích local", "database-efcore-sql", "Advanced"),
            ("nova-retail-pilot", "Cấu hình dashboard rủi ro vận hành Nova", "frontend-vue", "Working"),
            ("nova-retail-pilot", "Cấu hình dashboard rủi ro vận hành Nova", "devops-observability", "Working"),
            ("field-ops-mobile", "Xử lý hàng đợi đồng bộ offline", "mobile-offline-sync", "Advanced"),
            ("field-ops-mobile", "Xử lý hàng đợi đồng bộ offline", "backend-dotnet", "Working"),
            ("ops-compliance-readiness", "Bổ sung audit log cho thao tác nhạy cảm", "security-auth-privacy", "Advanced"),
            ("ops-compliance-readiness", "Bổ sung audit log cho thao tác nhạy cảm", "backend-dotnet", "Working")
        };
        var requirementTaskIds = requirementDefinitions
            .Select(item => FindTask(item.ProjectCode, item.TaskTitle)?.Id)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
        var existingRequirements = await _context.TaskSkillRequirements
            .Where(item => requirementTaskIds.Contains(item.TaskItemId))
            .Select(item => new { item.TaskItemId, item.OrganizationSkillId })
            .ToListAsync();
        var requirementKeys = existingRequirements
            .Select(item => (item.TaskItemId, item.OrganizationSkillId))
            .ToHashSet();
        foreach (var definition in requirementDefinitions)
        {
            var task = FindTask(definition.ProjectCode, definition.TaskTitle);
            if (task == null || !skills.TryGetValue(definition.Skill, out var skill))
            {
                continue;
            }

            var key = (task.Id, skill.Id);
            if (requirementKeys.Contains(key))
            {
                continue;
            }

            await _context.TaskSkillRequirements.AddAsync(new TaskSkillRequirement
            {
                TaskItemId = task.Id,
                OrganizationSkillId = skill.Id,
                RequiredLevel = definition.Level,
                Provenance = "MANUAL",
                ConfirmedByUserId = projects[definition.ProjectCode].OwnerId,
                ConfirmedAt = task.CreatedAt.AddHours(2),
                CreatedAt = task.CreatedAt.AddHours(2)
            });
            requirementKeys.Add(key);
            changed = true;
        }

        var evidenceDefinitions = new (string ProjectCode, string TaskTitle, string ContributorEmail)[]
        {
            ("qaly-workos-demo", "Rà soát matrix phân quyền cho manager và viewer", "admin@qaly.dev"),
            ("erumi-local-analytics", "Phân loại intent câu hỏi Erumi", "linh.chi@qaly.dev"),
            ("nova-retail-pilot", novaEvidenceTitle, "quoc.huy@qaly.dev"),
            ("field-ops-mobile", "Nén ảnh evidence trước khi upload", "gia.khang@qaly.dev"),
            ("ops-compliance-readiness", "Tạo webhook demo cho sự kiện task.updated", "viet.long@qaly.dev")
        };
        var evidenceTaskIds = evidenceDefinitions
            .Select(item => FindTask(item.ProjectCode, item.TaskTitle)?.Id)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
        var attributionKeys = (await _context.TaskCompletionAttributions
                .Where(item => evidenceTaskIds.Contains(item.TaskItemId))
                .Select(item => new { item.TaskItemId, item.ContributorUserId })
                .ToListAsync())
            .Select(item => (item.TaskItemId, item.ContributorUserId))
            .ToHashSet();
        foreach (var definition in evidenceDefinitions)
        {
            var task = FindTask(definition.ProjectCode, definition.TaskTitle);
            if (task == null || !users.TryGetValue(definition.ContributorEmail, out var contributor))
            {
                continue;
            }

            var key = (task.Id, contributor.Id);
            if (attributionKeys.Contains(key))
            {
                continue;
            }

            var completedAt = task.UpdatedAt ?? task.DueDate ?? task.CreatedAt;
            await _context.TaskCompletionAttributions.AddAsync(new TaskCompletionAttribution
            {
                TaskItemId = task.Id,
                ContributorUserId = contributor.Id,
                ConfirmedByUserId = projects[definition.ProjectCode].OwnerId,
                CompletedAt = completedAt,
                ConfirmedAt = completedAt.AddHours(4),
                Status = TaskCompletionAttribution.Confirmed,
                AttributionPolicyVersion = "completion-contributor.v1",
                CreatedAt = completedAt.AddHours(4)
            });
            attributionKeys.Add(key);
            changed = true;
        }
        await SaveIfChangedAsync();

        var weeklyCapacityByEmail = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin@qaly.dev"] = 36m,
            ["minh.anh@qaly.dev"] = 38m,
            ["bao.ngoc@qaly.dev"] = 36m,
            ["quoc.huy@qaly.dev"] = 40m,
            ["thu.ha@qaly.dev"] = 36m,
            ["gia.khang@qaly.dev"] = 40m,
            ["linh.chi@qaly.dev"] = 36m,
            ["tuan.kiet@qaly.dev"] = 40m,
            ["mai.phuong@qaly.dev"] = 36m,
            ["thanh.tam@qaly.dev"] = 38m,
            ["viet.long@qaly.dev"] = 40m,
            ["yen.nhi@qaly.dev"] = 32m
        };
        var capacityProfiles = await _context.OrganizationMemberCapacityProfiles
            .Include(item => item.AvailabilityWindows)
            .Where(item => item.OrganizationId == organization.Id)
            .ToDictionaryAsync(item => item.UserId);
        foreach (var (email, weeklyHours) in weeklyCapacityByEmail)
        {
            if (!users.TryGetValue(email, out var user) || capacityProfiles.ContainsKey(user.Id))
            {
                continue;
            }

            var profile = new OrganizationMemberCapacityProfile
            {
                OrganizationId = organization.Id,
                UserId = user.Id,
                WeeklyCapacityHours = weeklyHours,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                CreatedAt = now.AddDays(-21)
            };
            capacityProfiles[user.Id] = profile;
            await _context.OrganizationMemberCapacityProfiles.AddAsync(profile);
            changed = true;
        }
        await SaveIfChangedAsync();

        var nextMonday = now.Date.AddDays(((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7);
        if (nextMonday <= now.Date)
        {
            nextMonday = nextMonday.AddDays(7);
        }
        var availabilityDefinitions = new (string Email, int StartDay, int EndDay, string Kind, decimal? Hours)[]
        {
            ("admin@qaly.dev", 0, 5, MemberAvailabilityWindow.ReducedCapacity, 32m),
            ("linh.chi@qaly.dev", 7, 12, MemberAvailabilityWindow.ReducedCapacity, 28m),
            ("quoc.huy@qaly.dev", 2, 4, MemberAvailabilityWindow.Unavailable, null),
            ("gia.khang@qaly.dev", 9, 10, MemberAvailabilityWindow.Unavailable, null),
            ("viet.long@qaly.dev", 14, 19, MemberAvailabilityWindow.ReducedCapacity, 30m)
        };
        foreach (var definition in availabilityDefinitions)
        {
            if (!users.TryGetValue(definition.Email, out var user) ||
                !capacityProfiles.TryGetValue(user.Id, out var profile) ||
                profile.AvailabilityWindows.Count > 0)
            {
                continue;
            }

            var window = new MemberAvailabilityWindow
            {
                OrganizationMemberCapacityProfileId = profile.Id,
                StartsAt = nextMonday.AddDays(definition.StartDay),
                EndsAt = nextMonday.AddDays(definition.EndDay),
                Kind = definition.Kind,
                AvailableHours = definition.Hours,
                CreatedAt = now.AddDays(-2)
            };
            profile.AvailabilityWindows.Add(window);
            await _context.MemberAvailabilityWindows.AddAsync(window);
            changed = true;
        }
        await SaveIfChangedAsync();

        return changed;
    }

    private static string ComputeDemoHash(string value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
