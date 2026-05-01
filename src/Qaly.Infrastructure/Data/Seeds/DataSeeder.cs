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
        LogDatabaseMigrated(_logger);

        if (!await _context.Users.AnyAsync())
        {
            await SeedUsersAsync();
            await SeedProjectsAsync();
            await _context.SaveChangesAsync();
            LogSeedDataCreated(_logger);
        }
        else
        {
            LogSeedSkipped(_logger);
        }
    }

    private async Task SeedUsersAsync()
    {
        var users = new List<User>
        {
            new()
            {
                FullName = "Quản trị viên",
                Email = "admin@qaly.dev",
                PasswordHash = HashPassword("Admin@123"),
                Role = "Admin",
                IsActive = true
            },
            new()
            {
                FullName = "Nguyễn Văn A",
                Email = "nguyenvana@qaly.dev",
                PasswordHash = HashPassword("User@123"),
                Role = "Member"
            },
            new()
            {
                FullName = "Trần Thị B",
                Email = "tranthib@qaly.dev",
                PasswordHash = HashPassword("User@123"),
                Role = "Member"
            }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        LogSeededUsers(_logger, users.Count);
    }

    private async Task SeedProjectsAsync()
    {
        var admin = await _context.Users.FirstAsync(u => u.Role == "Admin");

        var project = new Project
        {
            Name = "Qaly MVP",
            Description = "Dự án quản lý công việc nội bộ - sản phẩm khả dụng tối thiểu",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddMonths(3)
        };

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Thêm thành viên
        var members = await _context.Users.Where(u => u.Role != "Admin").ToListAsync();
        foreach (var member in members)
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = member.Id,
                Role = "Member"
            });
        }

        // Thêm công việc mẫu
        var tasks = new List<TaskItem>
        {
            new() { Title = "Thiết kế lược đồ cơ sở dữ liệu", Status = "Done", Priority = "High", ProjectId = project.Id, ReporterId = admin.Id, AssigneeId = admin.Id },
            new() { Title = "Triển khai xác thực", Status = "InProgress", Priority = "High", ProjectId = project.Id, ReporterId = admin.Id },
            new() { Title = "Tạo giao diện bảng điều khiển", Status = "Todo", Priority = "Medium", ProjectId = project.Id, ReporterId = admin.Id },
            new() { Title = "Tích hợp trợ lý AI", Status = "Todo", Priority = "High", ProjectId = project.Id, ReporterId = admin.Id, DueDate = DateTimeOffset.UtcNow.AddDays(30) },
            new() { Title = "Viết kiểm thử đơn vị", Status = "Todo", Priority = "Medium", ProjectId = project.Id, ReporterId = admin.Id },
        };

        await _context.TaskItems.AddRangeAsync(tasks);
        LogSeededProject(_logger, project.Name, tasks.Count);
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
}
