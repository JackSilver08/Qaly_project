using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    private readonly QalyDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(QalyDbContext context, IConfiguration configuration, ILogger<DataSeeder> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();
        await EnsureProjectSchemaCompatibilityAsync();
        await EnsureImportSchemaCompatibilityAsync();
        await EnsureTimelineSchemaCompatibilityAsync();
        LogDatabaseMigrated(_logger);

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@qaly.dev");
        var qalyProject = await _context.Projects.FirstOrDefaultAsync(p => p.Code == "qaly-mvp");
        var needsReseed =
            (admin != null && admin.FullName != "Quản trị viên hệ thống") ||
            (qalyProject != null && qalyProject.Name != "Hệ thống Quản lý Qaly MVP");

        if (needsReseed)
        {
            LogStaleDataDetected(_logger);
            _context.TaskComments.RemoveRange(_context.TaskComments);
            _context.TaskItems.RemoveRange(_context.TaskItems);
            _context.ProjectMembers.RemoveRange(_context.ProjectMembers);
            _context.Projects.RemoveRange(_context.Projects);
            _context.Users.RemoveRange(_context.Users);
            await _context.SaveChangesAsync();
        }

        var seededAnyData = false;

        if (!await _context.Users.AnyAsync())
        {
            await SeedUsersAsync();
            seededAnyData = true;
        }

        if (!await _context.Projects.AnyAsync())
        {
            await SeedProjectsAsync();
            await SeedKnowledgeBaseAsync(); // ThÃªm dá»¯ liá»‡u tri thá»©c má»Ÿ rá»™ng
            await _context.SaveChangesAsync();
            seededAnyData = true;
        }

        if (seededAnyData)
        {
            LogSeedDataCreated(_logger);
        }
        else
        {
            LogSeedSkipped(_logger);
        }
    }

    private Task<int> EnsureImportSchemaCompatibilityAsync()
    {
        const string sql = """
            IF OBJECT_ID(N'[ImportSessions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ImportSessions] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ImportSessions] PRIMARY KEY DEFAULT NEWID(),
                    [ProjectId] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [FileName] nvarchar(256) NOT NULL,
                    [TotalRows] int NOT NULL CONSTRAINT [DF_ImportSessions_TotalRows] DEFAULT 0,
                    [ImportedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_ImportedCount] DEFAULT 0,
                    [SkippedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_SkippedCount] DEFAULT 0,
                    [IsUndone] bit NOT NULL CONSTRAINT [DF_ImportSessions_IsUndone] DEFAULT 0,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ImportSessions_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
            END;

            IF OBJECT_ID(N'[ProjectLabels]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProjectLabels] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ProjectLabels] PRIMARY KEY DEFAULT NEWID(),
                    [Color] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectLabels_Color] DEFAULT N'#64748B',
                    [Name] nvarchar(80) NOT NULL,
                    [ProjectId] uniqueidentifier NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ProjectLabels_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
                CREATE UNIQUE INDEX [IX_ProjectLabels_ProjectId_Name] ON [ProjectLabels]([ProjectId], [Name]);
                ALTER TABLE [ProjectLabels] ADD CONSTRAINT [FK_ProjectLabels_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
            END;

            IF OBJECT_ID(N'[TaskAssignments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TaskAssignments] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskAssignments] PRIMARY KEY DEFAULT NEWID(),
                    [TaskItemId] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
                CREATE INDEX [IX_TaskAssignments_UserId] ON [TaskAssignments]([UserId]);
                CREATE UNIQUE INDEX [IX_TaskAssignments_TaskItemId_UserId] ON [TaskAssignments]([TaskItemId], [UserId]);
                ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
                ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
            END;

            IF OBJECT_ID(N'[TaskLabels]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TaskLabels] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskLabels] PRIMARY KEY DEFAULT NEWID(),
                    [ProjectLabelId] uniqueidentifier NOT NULL,
                    [TaskItemId] uniqueidentifier NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskLabels_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
                CREATE INDEX [IX_TaskLabels_ProjectLabelId] ON [TaskLabels]([ProjectLabelId]);
                CREATE UNIQUE INDEX [IX_TaskLabels_TaskItemId_ProjectLabelId] ON [TaskLabels]([TaskItemId], [ProjectLabelId]);
                ALTER TABLE [TaskLabels] ADD CONSTRAINT [FK_TaskLabels_ProjectLabels_ProjectLabelId] FOREIGN KEY ([ProjectLabelId]) REFERENCES [ProjectLabels]([Id]) ON DELETE NO ACTION;
                ALTER TABLE [TaskLabels] ADD CONSTRAINT [FK_TaskLabels_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
            END;

            IF OBJECT_ID(N'[Votes]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Votes] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Votes] PRIMARY KEY DEFAULT NEWID(),
                    [TargetId] uniqueidentifier NOT NULL,
                    [TargetType] nvarchar(20) NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [Value] int NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Votes_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
                CREATE INDEX [IX_Votes_UserId] ON [Votes]([UserId]);
                CREATE INDEX [IX_Votes_TargetType_TargetId] ON [Votes]([TargetType], [TargetId]);
                CREATE UNIQUE INDEX [IX_Votes_TargetType_TargetId_UserId] ON [Votes]([TargetType], [TargetId], [UserId]);
                ALTER TABLE [Votes] ADD CONSTRAINT [FK_Votes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
            END;

            IF COL_LENGTH(N'[TaskItems]', N'ImportSessionId') IS NULL
            BEGIN
                ALTER TABLE [TaskItems] ADD [ImportSessionId] uniqueidentifier NULL;
            END;

            IF COL_LENGTH(N'[Projects]', N'Code') IS NULL
            BEGIN
                ALTER TABLE [Projects] ADD [Code] nvarchar(80) NOT NULL CONSTRAINT [DF_Projects_Code] DEFAULT N'';
            END;

            IF COL_LENGTH(N'[Projects]', N'LogoUrl') IS NULL
            BEGIN
                ALTER TABLE [Projects] ADD [LogoUrl] nvarchar(1000) NULL;
            END;

            IF COL_LENGTH(N'[TaskItems]', N'IsPinned') IS NULL
            BEGIN
                ALTER TABLE [TaskItems] ADD [IsPinned] bit NOT NULL CONSTRAINT [DF_TaskItems_IsPinned] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[TaskItems]', N'ContributesToProgress') IS NULL
            BEGIN
                ALTER TABLE [TaskItems] ADD [ContributesToProgress] bit NOT NULL CONSTRAINT [DF_TaskItems_ContributesToProgress] DEFAULT 1;
            END;

            IF COL_LENGTH(N'[TaskItems]', N'UpvoteCount') IS NULL
            BEGIN
                ALTER TABLE [TaskItems] ADD [UpvoteCount] int NOT NULL CONSTRAINT [DF_TaskItems_UpvoteCount] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[TaskItems]', N'DownvoteCount') IS NULL
            BEGIN
                ALTER TABLE [TaskItems] ADD [DownvoteCount] int NOT NULL CONSTRAINT [DF_TaskItems_DownvoteCount] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[TaskComments]', N'ParentCommentId') IS NULL
            BEGIN
                ALTER TABLE [TaskComments] ADD [ParentCommentId] uniqueidentifier NULL;
            END;

            IF COL_LENGTH(N'[TaskComments]', N'UpvoteCount') IS NULL
            BEGIN
                ALTER TABLE [TaskComments] ADD [UpvoteCount] int NOT NULL CONSTRAINT [DF_TaskComments_UpvoteCount] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[TaskComments]', N'DownvoteCount') IS NULL
            BEGIN
                ALTER TABLE [TaskComments] ADD [DownvoteCount] int NOT NULL CONSTRAINT [DF_TaskComments_DownvoteCount] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[TaskAttachments]', N'ProjectId') IS NULL
            BEGIN
                ALTER TABLE [TaskAttachments] ADD [ProjectId] uniqueidentifier NULL;
            END;

            IF COL_LENGTH(N'[TaskAttachments]', N'CommentId') IS NULL
            BEGIN
                ALTER TABLE [TaskAttachments] ADD [CommentId] uniqueidentifier NULL;
            END;

            IF COL_LENGTH(N'[TaskAttachments]', N'Scope') IS NULL
            BEGIN
                ALTER TABLE [TaskAttachments] ADD [Scope] nvarchar(20) NOT NULL CONSTRAINT [DF_TaskAttachments_Scope] DEFAULT N'Task';
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE name = N'FK_ImportSessions_Projects_ProjectId'
            )
            BEGIN
                ALTER TABLE [ImportSessions]
                    ADD CONSTRAINT [FK_ImportSessions_Projects_ProjectId]
                    FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE name = N'FK_ImportSessions_Users_UserId'
            )
            BEGIN
                ALTER TABLE [ImportSessions]
                    ADD CONSTRAINT [FK_ImportSessions_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys
                WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
            )
            BEGIN
                ALTER TABLE [TaskItems]
                    ADD CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId]
                    FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions]([Id]) ON DELETE NO ACTION;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_ImportSessions_ProjectId'
                  AND object_id = OBJECT_ID(N'[ImportSessions]')
            )
            BEGIN
                CREATE INDEX [IX_ImportSessions_ProjectId] ON [ImportSessions]([ProjectId]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_ImportSessions_UserId'
                  AND object_id = OBJECT_ID(N'[ImportSessions]')
            )
            BEGIN
                CREATE INDEX [IX_ImportSessions_UserId] ON [ImportSessions]([UserId]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_TaskItems_ImportSessionId'
                  AND object_id = OBJECT_ID(N'[TaskItems]')
            )
            BEGIN
                CREATE INDEX [IX_TaskItems_ImportSessionId] ON [TaskItems]([ImportSessionId]);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_Projects_Code'
                  AND object_id = OBJECT_ID(N'[Projects]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects]([Code]);
            END;
            """;

        return _context.Database.ExecuteSqlRawAsync(sql);
    }

    private Task<int> EnsureProjectSchemaCompatibilityAsync()
    {
        const string sql = """
            IF COL_LENGTH(N'[Projects]', N'Code') IS NULL
            BEGIN
                ALTER TABLE [Projects] ADD [Code] nvarchar(80) NULL;
            END;

            IF COL_LENGTH(N'[Projects]', N'LogoUrl') IS NULL
            BEGIN
                ALTER TABLE [Projects] ADD [LogoUrl] nvarchar(1000) NULL;
            END;

            IF COL_LENGTH(N'[Projects]', N'OrganizationId') IS NULL
            BEGIN
                ALTER TABLE [Projects] ADD [OrganizationId] uniqueidentifier NULL;
            END;

            UPDATE [Projects]
            SET [Code] = CONCAT(N'project-', REPLACE(CONVERT(nvarchar(36), [Id]), N'-', N''))
            WHERE [Code] IS NULL OR LTRIM(RTRIM([Code])) = N'';

            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[Projects]')
                  AND name = N'Code'
                  AND is_nullable = 1
            )
            BEGIN
                ALTER TABLE [Projects] ALTER COLUMN [Code] nvarchar(80) NOT NULL;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_Projects_Code'
                  AND object_id = OBJECT_ID(N'[Projects]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects]([Code]);
            END;

            IF OBJECT_ID(N'[Organizations]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Projects_Organizations_OrganizationId'
               )
            BEGIN
                ALTER TABLE [Projects]
                    ADD CONSTRAINT [FK_Projects_Organizations_OrganizationId]
                    FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations]([Id]) ON DELETE NO ACTION;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_Projects_OrganizationId'
                  AND object_id = OBJECT_ID(N'[Projects]')
            )
            BEGIN
                CREATE INDEX [IX_Projects_OrganizationId] ON [Projects]([OrganizationId]);
            END;
            """;

        return _context.Database.ExecuteSqlRawAsync(sql);
    }

    private Task<int> EnsureTimelineSchemaCompatibilityAsync()
    {
        const string sql = """
            IF COL_LENGTH(N'[ProjectMembers]', N'CanViewProjectTimeline') IS NULL
            BEGIN
                ALTER TABLE [ProjectMembers] ADD [CanViewProjectTimeline] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewProjectTimeline] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[ProjectMembers]', N'CanViewTaskRisk') IS NULL
            BEGIN
                ALTER TABLE [ProjectMembers] ADD [CanViewTaskRisk] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewTaskRisk] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[ProjectMembers]', N'CanNudgeAssignee') IS NULL
            BEGIN
                ALTER TABLE [ProjectMembers] ADD [CanNudgeAssignee] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanNudgeAssignee] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[ProjectMembers]', N'CanViewUnseenTaskSignal') IS NULL
            BEGIN
                ALTER TABLE [ProjectMembers] ADD [CanViewUnseenTaskSignal] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewUnseenTaskSignal] DEFAULT 0;
            END;

            IF COL_LENGTH(N'[TaskAssignments]', N'AssignedAt') IS NULL
            BEGIN
                ALTER TABLE [TaskAssignments] ADD [AssignedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_AssignedAt] DEFAULT SYSDATETIMEOFFSET();
            END;

            IF COL_LENGTH(N'[TaskAssignments]', N'AssignedByUserId') IS NULL
            BEGIN
                ALTER TABLE [TaskAssignments] ADD [AssignedByUserId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[TaskViewEvents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TaskViewEvents] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskViewEvents] PRIMARY KEY DEFAULT NEWID(),
                    [TaskItemId] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [ViewedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskViewEvents_ViewedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [ViewCount] int NOT NULL CONSTRAINT [DF_TaskViewEvents_ViewCount] DEFAULT 1,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskViewEvents_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskViewEvents_TaskItemId_UserId' AND object_id = OBJECT_ID(N'[TaskViewEvents]'))
            BEGIN
                CREATE UNIQUE INDEX [IX_TaskViewEvents_TaskItemId_UserId] ON [TaskViewEvents]([TaskItemId], [UserId]);
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskViewEvents_ViewedAt' AND object_id = OBJECT_ID(N'[TaskViewEvents]'))
            BEGIN
                CREATE INDEX [IX_TaskViewEvents_ViewedAt] ON [TaskViewEvents]([ViewedAt]);
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskViewEvents_TaskItems_TaskItemId')
            BEGIN
                ALTER TABLE [TaskViewEvents]
                    ADD CONSTRAINT [FK_TaskViewEvents_TaskItems_TaskItemId]
                    FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskViewEvents_Users_UserId')
            BEGIN
                ALTER TABLE [TaskViewEvents]
                    ADD CONSTRAINT [FK_TaskViewEvents_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
            END;

            IF OBJECT_ID(N'[TaskAttentionSignals]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TaskAttentionSignals] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskAttentionSignals] PRIMARY KEY DEFAULT NEWID(),
                    [TaskItemId] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [SignalType] nvarchar(50) NOT NULL,
                    [FirstDetectedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAttentionSignals_FirstDetectedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [LastSentAt] datetimeoffset NULL,
                    [CooldownHours] int NOT NULL CONSTRAINT [DF_TaskAttentionSignals_CooldownHours] DEFAULT 24,
                    [ResolvedAt] datetimeoffset NULL,
                    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAttentionSignals_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                    [UpdatedAt] datetimeoffset NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskAttentionSignals_TaskItemId_UserId_SignalType' AND object_id = OBJECT_ID(N'[TaskAttentionSignals]'))
            BEGIN
                CREATE UNIQUE INDEX [IX_TaskAttentionSignals_TaskItemId_UserId_SignalType]
                ON [TaskAttentionSignals]([TaskItemId], [UserId], [SignalType]);
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskAttentionSignals_UserId_ResolvedAt_LastSentAt' AND object_id = OBJECT_ID(N'[TaskAttentionSignals]'))
            BEGIN
                CREATE INDEX [IX_TaskAttentionSignals_UserId_ResolvedAt_LastSentAt]
                ON [TaskAttentionSignals]([UserId], [ResolvedAt], [LastSentAt]);
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskAttentionSignals_TaskItems_TaskItemId')
            BEGIN
                ALTER TABLE [TaskAttentionSignals]
                    ADD CONSTRAINT [FK_TaskAttentionSignals_TaskItems_TaskItemId]
                    FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskAttentionSignals_Users_UserId')
            BEGIN
                ALTER TABLE [TaskAttentionSignals]
                    ADD CONSTRAINT [FK_TaskAttentionSignals_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
            END;
            """;

        return _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task SeedKnowledgeBaseAsync()
    {
        var admin = await _context.Users.FirstAsync(u => u.Role == "Admin");
        var project = await _context.Projects.FirstAsync();

        var kbTasks = new List<TaskItem>
        {
            new()
            {
                Title = "Quy trình làm việc (Workflow) của Qaly",
                Status = "Done",
                Priority = "Low",
                ProjectId = project.Id,
                ReporterId = admin.Id,
                Description = "Quy trình chuẩn gồm 4 bước: Todo -> InProgress -> InReview -> Done. Công việc mới sẽ mặc định ở trạng thái Todo."
            },
            new()
            {
                Title = "Hướng dẫn sử dụng Erumi Chatbot",
                Status = "Done",
                Priority = "Low",
                ProjectId = project.Id,
                ReporterId = admin.Id,
                Description = "Erumi hỗ trợ tóm tắt dự án, phân tích rủi ro và đề xuất phân công công việc."
            },
            new()
            {
                Title = "Chính sách bảo mật dữ liệu",
                Status = "Done",
                Priority = "High",
                ProjectId = project.Id,
                ReporterId = admin.Id,
                Description = "Dữ liệu Qaly được lưu trữ nội bộ, AI chạy offline và vector store được quản lý riêng."
            },
            new()
            {
                Title = "Sơ đồ tổ chức dự án",
                Status = "Done",
                Priority = "Medium",
                ProjectId = project.Id,
                ReporterId = admin.Id,
                Description = "Dự án có 3 vai trò chính: Admin, Manager và Member. Mỗi vai trò có phạm vi thao tác riêng."
            }
        };

        await _context.TaskItems.AddRangeAsync(kbTasks);
    }

    private async Task SeedUsersAsync()
    {
        var adminPassword = GetRequiredSeedSecret("Seed:AdminPassword", "QALY_SEED_ADMIN_PASSWORD");
        var userPassword = GetRequiredSeedSecret("Seed:DefaultUserPassword", "QALY_SEED_DEFAULT_USER_PASSWORD");

        var users = new List<User>
        {
            new() { FullName = "Quản trị viên hệ thống", Email = "admin@qaly.dev", PasswordHash = HashPassword(adminPassword), Role = "Admin", IsActive = true },
            new() { FullName = "Nguyễn Văn An", Email = "nguyenvana@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Trần Thị Bình", Email = "tranthib@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Lê Văn Cường", Email = "levancuong@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Phạm Minh Đức", Email = "phamminhduc@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Hoàng Thu Hà", Email = "hoangthuha@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Đặng Hồng Liên", Email = "danghonglien@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Vũ Quang Huy", Email = "vuquanghuy@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Bùi Tuyết Mai", Email = "buituyetmai@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true },
            new() { FullName = "Ngô Gia Bảo", Email = "ngogiabao@qaly.dev", PasswordHash = HashPassword(userPassword), Role = "Member", IsActive = true }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        LogSeededUsers(_logger, users.Count);
    }

    private string GetRequiredSeedSecret(string configKey, string environmentVariable)
    {
        var value = _configuration[configKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(environmentVariable);
        }

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Missing seed secret '{configKey}'. Set it in user-secrets, a local .env file, or the {environmentVariable} environment variable.");
    }

    private async Task SeedProjectsAsync()
    {
        var admin = await _context.Users.FirstAsync(u => u.Role == "Admin");
        var allUsers = await _context.Users.ToListAsync();

        var qalyMvp = new Project
        {
            Name = "Hệ thống Quản lý Qaly MVP",
            Code = "qaly-mvp",
            Description = "Nền tảng quản lý công việc tập trung, tích hợp AI để tối ưu hóa hiệu suất làm việc nhóm.",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-10),
            EndDate = DateTimeOffset.UtcNow.AddMonths(3)
        };

        var aiLab = new Project
        {
            Name = "Phòng Lab Nghiên cứu AI Qaly",
            Code = "ai-lab",
            Description = "Nghiên cứu mô hình ngôn ngữ nhỏ và tối ưu hóa bộ nhớ cho hệ thống chạy local.",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            EndDate = DateTimeOffset.UtcNow.AddMonths(6)
        };

        var fintech = new Project
        {
            Name = "Kiểm định Bảo mật Fintech 2026",
            Code = "fintech-security-2026",
            Description = "Dự án đánh giá an ninh mạng cho hệ thống thanh toán ngân hàng.",
            Status = "Planned",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(15),
            EndDate = DateTimeOffset.UtcNow.AddMonths(4)
        };

        var smartCity = new Project
        {
            Name = "Hạ tầng Smart City Qaly",
            Code = "smart-city-qaly",
            Description = "Xây dựng hệ thống IoT giám sát giao thông và môi trường đô thị.",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-30),
            EndDate = DateTimeOffset.UtcNow.AddYears(1)
        };

        await _context.Projects.AddRangeAsync(qalyMvp, aiLab, fintech, smartCity);
        await _context.SaveChangesAsync();

        foreach (var user in allUsers.Skip(1).Take(4))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = qalyMvp.Id, UserId = user.Id, Role = user.Email.Contains("nguyenvana") ? "Manager" : "Member" });
        }

        foreach (var user in allUsers.Skip(5).Take(3))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = aiLab.Id, UserId = user.Id, Role = "Member" });
        }

        foreach (var user in allUsers.Skip(2).Take(6))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = smartCity.Id, UserId = user.Id, Role = "Member" });
        }

        var qalyTasks = new List<TaskItem>
        {
            new() { Title = "Thiết kế cơ sở dữ liệu chi tiết", Status = "Done", Priority = "High", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = admin.Id, Description = "Xây dựng cấu trúc SQL Server cho các bảng lõi." },
            new() { Title = "Triển khai Auth & Role", Status = "Done", Priority = "High", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = allUsers[1].Id, Description = "Sử dụng Cookie Auth cho Admin và Member." },
            new() { Title = "Tích hợp AI Erumi", Status = "InProgress", Priority = "High", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = allUsers[2].Id, Description = "Triển khai RAG với Qdrant và Ollama." },
            new() { Title = "Xuất báo cáo Excel/Word", Status = "Todo", Priority = "Medium", ProjectId = qalyMvp.Id, ReporterId = admin.Id, Description = "Dùng ClosedXML để tạo file báo cáo tự động." }
        };

        var aiTasks = new List<TaskItem>
        {
            new() { Title = "Benchmark Llama 3.2 1B", Status = "Done", Priority = "High", ProjectId = aiLab.Id, ReporterId = admin.Id, AssigneeId = allUsers[5].Id, Description = "Đo lường throughput trên RTX 4090." },
            new() { Title = "Tối ưu Embedding Pipeline", Status = "InProgress", Priority = "Medium", ProjectId = aiLab.Id, ReporterId = admin.Id, AssigneeId = allUsers[6].Id, Description = "Sử dụng batching để tăng tốc độ nạp dữ liệu." }
        };

        var smartTasks = new List<TaskItem>
        {
            new() { Title = "Cài đặt Sensor tại Quận 1", Status = "InProgress", Priority = "High", ProjectId = smartCity.Id, ReporterId = admin.Id, AssigneeId = allUsers[8].Id, DueDate = DateTimeOffset.UtcNow.AddDays(-5), Description = "Lắp đặt 50 cảm biến không khí." },
            new() { Title = "Viết API thu thập dữ liệu", Status = "Todo", Priority = "High", ProjectId = smartCity.Id, ReporterId = admin.Id, AssigneeId = allUsers[9].Id, DueDate = DateTimeOffset.UtcNow.AddDays(-2), Description = "Phát triển endpoint nhận dữ liệu từ gateway." }
        };

        await _context.TaskItems.AddRangeAsync(qalyTasks);
        await _context.TaskItems.AddRangeAsync(aiTasks);
        await _context.TaskItems.AddRangeAsync(smartTasks);
        await _context.SaveChangesAsync();

        var aiTask = qalyTasks.First(t => t.Title.Contains("AI"));
        await _context.TaskComments.AddRangeAsync(new List<TaskComment>
        {
            new() { TaskItemId = aiTask.Id, AuthorId = allUsers[1].Id, Content = "Erumi trả lời rất nhanh với streaming API mới." },
            new() { TaskItemId = aiTask.Id, AuthorId = admin.Id, Content = "Cần bổ sung thêm khả năng tạo file báo cáo." }
        });

        LogSeededProject(_logger, "Multi-Projects", 4);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Cơ sở dữ liệu đã migrate thành công.")]
    private static partial void LogDatabaseMigrated(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Dữ liệu mẫu đã được tạo thành công.")]
    private static partial void LogSeedDataCreated(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Cơ sở dữ liệu đã có dữ liệu. Bỏ qua bước seed.")]
    private static partial void LogSeedSkipped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Đã seed {UserCount} người dùng.")]
    private static partial void LogSeededUsers(ILogger logger, int userCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Đã seed dự án '{ProjectName}' với {TaskCount} công việc.")]
    private static partial void LogSeededProject(ILogger logger, string projectName, int taskCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Phát hiện dữ liệu cũ, tiến hành xóa để re-seed bản Tiếng Việt mới...")]
    private static partial void LogStaleDataDetected(ILogger logger);
}
