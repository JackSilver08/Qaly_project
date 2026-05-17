using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    private readonly QalyDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(QalyDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();
        await EnsureImportSchemaCompatibilityAsync();
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

        if (!await _context.Users.AnyAsync())
        {
            await SeedUsersAsync();
            await SeedProjectsAsync();
            await SeedKnowledgeBaseAsync(); // ThÃªm dá»¯ liá»‡u tri thá»©c má»Ÿ rá»™ng
            await _context.SaveChangesAsync();
            LogSeedDataCreated(_logger);
        }
        else
        {
            LogSeedSkipped(_logger);
        }
    }

    private Task EnsureImportSchemaCompatibilityAsync()
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

            IF COL_LENGTH(N'[TaskItems]', N'ImportSessionId') IS NULL
            BEGIN
                ALTER TABLE [TaskItems] ADD [ImportSessionId] uniqueidentifier NULL;
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
                    FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions]([Id]) ON DELETE SET NULL;
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
        var users = new List<User>
        {
            new() { FullName = "Quản trị viên hệ thống", Email = "admin@qaly.dev", PasswordHash = HashPassword("Admin@123"), Role = "Admin", IsActive = true },
            new() { FullName = "Nguyễn Văn An", Email = "nguyenvana@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Trần Thị Bình", Email = "tranthib@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Lê Văn Cường", Email = "levancuong@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Phạm Minh Đức", Email = "phamminhduc@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Hoàng Thu Hà", Email = "hoangthuha@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Đặng Hồng Liên", Email = "danghonglien@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Vũ Quang Huy", Email = "vuquanghuy@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Bùi Tuyết Mai", Email = "buituyetmai@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Ngô Gia Bảo", Email = "ngogiabao@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        LogSeededUsers(_logger, users.Count);
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
