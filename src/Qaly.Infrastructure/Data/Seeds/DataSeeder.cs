using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Seeds;

public class DataSeeder
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
        _logger.LogInformation("Database migrated successfully.");

        if (!await _context.Users.AnyAsync())
        {
            await SeedUsersAsync();
            await SeedProjectsAsync();
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed data created successfully.");
        }
        else
        {
            _logger.LogInformation("Database already contains data. Skipping seed.");
        }
    }

    private async Task SeedUsersAsync()
    {
        var users = new List<User>
        {
            new()
            {
                FullName = "Admin User",
                Email = "admin@qaly.dev",
                PasswordHash = HashPassword("Admin@123"),
                Role = "Admin",
                IsActive = true
            },
            new()
            {
                FullName = "Nguyen Van A",
                Email = "nguyenvana@qaly.dev",
                PasswordHash = HashPassword("User@123"),
                Role = "Member"
            },
            new()
            {
                FullName = "Tran Thi B",
                Email = "tranthib@qaly.dev",
                PasswordHash = HashPassword("User@123"),
                Role = "Member"
            }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} users.", users.Count);
    }

    private async Task SeedProjectsAsync()
    {
        var admin = await _context.Users.FirstAsync(u => u.Role == "Admin");

        var project = new Project
        {
            Name = "Qaly MVP",
            Description = "Dự án quản lý công việc nội bộ - Minimum Viable Product",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddMonths(3)
        };

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Add members
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

        // Add sample tasks
        var tasks = new List<TaskItem>
        {
            new() { Title = "Thiết kế database schema", Status = "Done", Priority = "High", ProjectId = project.Id, ReporterId = admin.Id, AssigneeId = admin.Id },
            new() { Title = "Implement Authentication", Status = "InProgress", Priority = "High", ProjectId = project.Id, ReporterId = admin.Id },
            new() { Title = "Tạo Dashboard UI", Status = "Todo", Priority = "Medium", ProjectId = project.Id, ReporterId = admin.Id },
            new() { Title = "Tích hợp AI Assistant", Status = "Todo", Priority = "High", ProjectId = project.Id, ReporterId = admin.Id, DueDate = DateTimeOffset.UtcNow.AddDays(30) },
            new() { Title = "Viết Unit Tests", Status = "Todo", Priority = "Medium", ProjectId = project.Id, ReporterId = admin.Id },
        };

        await _context.TaskItems.AddRangeAsync(tasks);
        _logger.LogInformation("Seeded project '{Name}' with {Count} tasks.", project.Name, tasks.Count);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
