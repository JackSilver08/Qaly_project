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

        // Xóa dữ liệu cũ để đảm bảo seed lại bản Tiếng Việt chuẩn nhất
        if (await _context.Users.AnyAsync(u => u.Email == "admin@qaly.dev"))
        {
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@qaly.dev");
            if (admin != null && admin.FullName != "Quản trị viên hệ thống")
            {
                LogStaleDataDetected(_logger);
                _context.TaskComments.RemoveRange(_context.TaskComments);
                _context.TaskItems.RemoveRange(_context.TaskItems);
                _context.ProjectMembers.RemoveRange(_context.ProjectMembers);
                _context.Projects.RemoveRange(_context.Projects);
                _context.Users.RemoveRange(_context.Users);
                await _context.SaveChangesAsync();
            }
        }

        if (!await _context.Users.AnyAsync())
        {
            await SeedUsersAsync();
            await SeedProjectsAsync();
            await SeedKnowledgeBaseAsync(); // Thêm dữ liệu tri thức mở rộng
            await _context.SaveChangesAsync();
            LogSeedDataCreated(_logger);
        }
        else
        {
            LogSeedSkipped(_logger);
        }
    }

    private async Task SeedKnowledgeBaseAsync()
    {
        var admin = await _context.Users.FirstAsync(u => u.Role == "Admin");
        var project = await _context.Projects.FirstAsync();

        var kbTasks = new List<TaskItem>
        {
            new() { 
                Title = "Quy trình làm việc (Workflow) của Qaly", 
                Status = "Hoàn thành", 
                Priority = "Thấp", 
                ProjectId = project.Id, 
                ReporterId = admin.Id,
                Description = "Quy trình chuẩn bao gồm 4 bước: Cần làm (Todo) -> Đang thực hiện (InProgress) -> Chờ duyệt (InReview) -> Hoàn thành (Done). Tất cả các công việc mới tạo mặc định ở trạng thái Cần làm. Khi một task chuyển sang 'Hoàn thành', hệ thống sẽ tự động gửi thông báo cho người báo cáo."
            },
            new() { 
                Title = "Hướng dẫn sử dụng Erumi Chatbot", 
                Status = "Hoàn thành", 
                Priority = "Thấp", 
                ProjectId = project.Id, 
                ReporterId = admin.Id,
                Description = "Erumi hỗ trợ các lệnh: 'Tóm tắt dự án', 'Phân tích rủi ro', 'Đề xuất phân công'. Bạn có thể hỏi trực tiếp về bất kỳ task nào trong hệ thống, Erumi sẽ tìm kiếm ngữ cảnh và trả lời. Erumi cũng có khả năng đề xuất độ ưu tiên dựa trên mức độ quan trọng của công việc."
            },
            new() { 
                Title = "Chính sách bảo mật dữ liệu", 
                Status = "Hoàn thành", 
                Priority = "Cao", 
                ProjectId = project.Id, 
                ReporterId = admin.Id,
                Description = "Toàn bộ dữ liệu của Qaly được lưu trữ local trên hệ thống của khách hàng. Chúng tôi sử dụng Ollama để chạy AI Offline, đảm bảo không có dữ liệu nào bị gửi ra bên ngoài internet. Dữ liệu vector được lưu trữ mã hóa trong Qdrant."
            },
            new() {
                Title = "Sơ đồ tổ chức dự án",
                Status = "Hoàn thành",
                Priority = "Trung bình",
                ProjectId = project.Id,
                ReporterId = admin.Id,
                Description = "Dự án hiện tại có 3 vai trò chính: Admin (Toàn quyền), Manager (Quản lý dự án), Member (Thành viên thực hiện). Admin có thể tạo dự án và mời thành viên. Manager có thể quản lý tasks. Member chỉ có thể cập nhật task được giao."
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

        // Dự án 1: Qaly MVP
        var qalyMvp = new Project
        {
            Name = "Hệ thống Quản lý Qaly MVP",
            Description = "Dự án phát triển nền tảng quản lý công việc tập trung, tích hợp trí tuệ nhân tạo để tối ưu hóa hiệu suất làm việc nhóm. Nền tảng này hỗ trợ đa dự án, realtime notifications và phân tích dữ liệu thông minh.",
            Status = "Đang hoạt động",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-10),
            EndDate = DateTimeOffset.UtcNow.AddMonths(3)
        };

        // Dự án 2: AI Lab
        var aiLab = new Project
        {
            Name = "Phòng Lab Nghiên cứu AI Qaly",
            Description = "Nghiên cứu các kiến trúc LLM mới và tối ưu hóa bộ nhớ cho hệ thống chạy local. Tập trung vào các mô hình nhỏ (Small Language Models) như Phi-3, Gemma.",
            Status = "Đang hoạt động",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            EndDate = DateTimeOffset.UtcNow.AddMonths(6)
        };

        // Dự án 3: Fintech Security
        var fintech = new Project
        {
            Name = "Kiểm định Bảo mật Fintech 2026",
            Description = "Dự án đánh giá an ninh mạng cho hệ thống thanh toán ngân hàng. Bao gồm Pentest, rà soát lỗ hổng và tư vấn kiến trúc Zero Trust.",
            Status = "Lập kế hoạch",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(15),
            EndDate = DateTimeOffset.UtcNow.AddMonths(4)
        };

        // Dự án 4: Smart City
        var smartCity = new Project
        {
            Name = "Hạ tầng Smart City Qaly",
            Description = "Xây dựng hệ thống IoT giám sát giao thông và môi trường đô thị. Tích hợp AI để dự báo tắc nghẽn giao thông.",
            Status = "Đang hoạt động",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-30),
            EndDate = DateTimeOffset.UtcNow.AddYears(1)
        };

        await _context.Projects.AddRangeAsync(qalyMvp, aiLab, fintech, smartCity);
        await _context.SaveChangesAsync();

        // Thêm thành viên cho Qaly MVP
        foreach (var user in allUsers.Skip(1).Take(4))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = qalyMvp.Id, UserId = user.Id, Role = user.Email.Contains("nguyenvana") ? "Manager" : "Thành viên" });
        }

        // Thêm thành viên cho AI Lab
        foreach (var user in allUsers.Skip(5).Take(3))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = aiLab.Id, UserId = user.Id, Role = "Researcher" });
        }

        // Thêm thành viên cho Smart City
        foreach (var user in allUsers.Skip(2).Take(6))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = smartCity.Id, UserId = user.Id, Role = "Engineer" });
        }

        // Thêm công việc mẫu cho Qaly MVP
        var qalyTasks = new List<TaskItem>
        {
            new() { Title = "Thiết kế cơ sở dữ liệu chi tiết", Status = "Hoàn thành", Priority = "Cao", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = admin.Id, Description = "Xây dựng cấu trúc SQL Server cho các bảng core. Đã hoàn thành migration." },
            new() { Title = "Triển khai Auth & Role", Status = "Hoàn thành", Priority = "Cao", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = allUsers[1].Id, Description = "Sử dụng Cookie Auth. Hỗ trợ Admin và Member roles." },
            new() { Title = "Tích hợp AI Erumi", Status = "InProgress", Priority = "Cao", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = allUsers[2].Id, Description = "Triển khai RAG với Qdrant và Ollama. Hiện đang tinh chỉnh prompt." },
            new() { Title = "Xuất báo cáo Excel/Word", Status = "Todo", Priority = "Trung bình", ProjectId = qalyMvp.Id, ReporterId = admin.Id, Description = "Sử dụng ClosedXML để tạo file báo cáo dự án tự động." }
        };

        // Thêm công việc mẫu cho AI Lab
        var aiTasks = new List<TaskItem>
        {
            new() { Title = "Benchmark Llama 3.2 1B", Status = "Hoàn thành", Priority = "Cao", ProjectId = aiLab.Id, ReporterId = admin.Id, AssigneeId = allUsers[5].Id, Description = "Đo lường throughput trên RTX 4090." },
            new() { Title = "Tối ưu Embedding Pipeline", Status = "InProgress", Priority = "Trung bình", ProjectId = aiLab.Id, ReporterId = admin.Id, AssigneeId = allUsers[6].Id, Description = "Sử dụng Batching để tăng tốc độ nạp dữ liệu vào Qdrant." }
        };

        // Thêm công việc mẫu cho Smart City (Quá hạn)
        var smartTasks = new List<TaskItem>
        {
            new() { Title = "Cài đặt Sensor tại Quận 1", Status = "InProgress", Priority = "Cao", ProjectId = smartCity.Id, ReporterId = admin.Id, AssigneeId = allUsers[8].Id, DueDate = DateTimeOffset.UtcNow.AddDays(-5), Description = "Lắp đặt 50 cảm biến không khí. Đang bị chậm do thiếu linh kiện." },
            new() { Title = "Viết API thu thập dữ liệu", Status = "Todo", Priority = "Cao", ProjectId = smartCity.Id, ReporterId = admin.Id, AssigneeId = allUsers[9].Id, DueDate = DateTimeOffset.UtcNow.AddDays(-2), Description = "Phát triển endpoint nhận dữ liệu từ gateway qua MQTT." }
        };

        await _context.TaskItems.AddRangeAsync(qalyTasks);
        await _context.TaskItems.AddRangeAsync(aiTasks);
        await _context.TaskItems.AddRangeAsync(smartTasks);
        await _context.SaveChangesAsync();

        // Thêm comment cho task AI
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
