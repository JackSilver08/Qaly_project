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

        // XÃ³a dá»¯ liá»‡u cÅ© Ä‘á»ƒ Ä‘áº£m báº£o seed láº¡i báº£n Tiáº¿ng Viá»‡t chuáº©n nháº¥t
        if (await _context.Users.AnyAsync(u => u.Email == "admin@qaly.dev"))
        {
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@qaly.dev");
            if (admin != null && admin.FullName != "Quáº£n trá»‹ viÃªn há»‡ thá»‘ng")
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
            await SeedKnowledgeBaseAsync(); // ThÃªm dá»¯ liá»‡u tri thá»©c má»Ÿ rá»™ng
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
                Title = "Quy trÃ¬nh lÃ m viá»‡c (Workflow) cá»§a Qaly", 
                Status = "Done", 
                Priority = "Low", 
                ProjectId = project.Id, 
                ReporterId = admin.Id,
                Description = "Quy trÃ¬nh chuáº©n bao gá»“m 4 bÆ°á»›c: Cáº§n lÃ m (Todo) -> Äang thá»±c hiá»‡n (InProgress) -> Chá» duyá»‡t (InReview) -> HoÃ n thÃ nh (Done). Táº¥t cáº£ cÃ¡c cÃ´ng viá»‡c má»›i táº¡o máº·c Ä‘á»‹nh á»Ÿ tráº¡ng thÃ¡i Cáº§n lÃ m. Khi má»™t task chuyá»ƒn sang 'HoÃ n thÃ nh', há»‡ thá»‘ng sáº½ tá»± Ä‘á»™ng gá»­i thÃ´ng bÃ¡o cho ngÆ°á»i bÃ¡o cÃ¡o."
            },
            new() { 
                Title = "HÆ°á»›ng dáº«n sá»­ dá»¥ng Erumi Chatbot", 
                Status = "Done", 
                Priority = "Low", 
                ProjectId = project.Id, 
                ReporterId = admin.Id,
                Description = "Erumi há»— trá»£ cÃ¡c lá»‡nh: 'TÃ³m táº¯t dá»± Ã¡n', 'PhÃ¢n tÃ­ch rá»§i ro', 'Äá» xuáº¥t phÃ¢n cÃ´ng'. Báº¡n cÃ³ thá»ƒ há»i trá»±c tiáº¿p vá» báº¥t ká»³ task nÃ o trong há»‡ thá»‘ng, Erumi sáº½ tÃ¬m kiáº¿m ngá»¯ cáº£nh vÃ  tráº£ lá»i. Erumi cÅ©ng cÃ³ kháº£ nÄƒng Ä‘á» xuáº¥t Ä‘á»™ Æ°u tiÃªn dá»±a trÃªn má»©c Ä‘á»™ quan trá»ng cá»§a cÃ´ng viá»‡c."
            },
            new() { 
                Title = "ChÃ­nh sÃ¡ch báº£o máº­t dá»¯ liá»‡u", 
                Status = "Done", 
                Priority = "High", 
                ProjectId = project.Id, 
                ReporterId = admin.Id,
                Description = "ToÃ n bá»™ dá»¯ liá»‡u cá»§a Qaly Ä‘Æ°á»£c lÆ°u trá»¯ local trÃªn há»‡ thá»‘ng cá»§a khÃ¡ch hÃ ng. ChÃºng tÃ´i sá»­ dá»¥ng Ollama Ä‘á»ƒ cháº¡y AI Offline, Ä‘áº£m báº£o khÃ´ng cÃ³ dá»¯ liá»‡u nÃ o bá»‹ gá»­i ra bÃªn ngoÃ i internet. Dá»¯ liá»‡u vector Ä‘Æ°á»£c lÆ°u trá»¯ mÃ£ hÃ³a trong Qdrant."
            },
            new() {
                Title = "SÆ¡ Ä‘á»“ tá»• chá»©c dá»± Ã¡n",
                Status = "Done",
                Priority = "Medium",
                ProjectId = project.Id,
                ReporterId = admin.Id,
                Description = "Dá»± Ã¡n hiá»‡n táº¡i cÃ³ 3 vai trÃ² chÃ­nh: Admin (ToÃ n quyá»n), Manager (Quáº£n lÃ½ dá»± Ã¡n), Member (ThÃ nh viÃªn thá»±c hiá»‡n). Admin cÃ³ thá»ƒ táº¡o dá»± Ã¡n vÃ  má»i thÃ nh viÃªn. Manager cÃ³ thá»ƒ quáº£n lÃ½ tasks. Member chá»‰ cÃ³ thá»ƒ cáº­p nháº­t task Ä‘Æ°á»£c giao."
            }
        };

        await _context.TaskItems.AddRangeAsync(kbTasks);
    }

    private async Task SeedUsersAsync()
    {
        var users = new List<User>
        {
            new() { FullName = "Quáº£n trá»‹ viÃªn há»‡ thá»‘ng", Email = "admin@qaly.dev", PasswordHash = HashPassword("Admin@123"), Role = "Admin", IsActive = true },
            new() { FullName = "Nguyá»…n VÄƒn An", Email = "nguyenvana@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Tráº§n Thá»‹ BÃ¬nh", Email = "tranthib@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "LÃª VÄƒn CÆ°á»ng", Email = "levancuong@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Pháº¡m Minh Äá»©c", Email = "phamminhduc@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "HoÃ ng Thu HÃ ", Email = "hoangthuha@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "Äáº·ng Há»“ng LiÃªn", Email = "danghonglien@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "VÅ© Quang Huy", Email = "vuquanghuy@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "BÃ¹i Tuyáº¿t Mai", Email = "buituyetmai@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true },
            new() { FullName = "NgÃ´ Gia Báº£o", Email = "ngogiabao@qaly.dev", PasswordHash = HashPassword("User@123"), Role = "Member", IsActive = true }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        LogSeededUsers(_logger, users.Count);
    }

    private async Task SeedProjectsAsync()
    {
        var admin = await _context.Users.FirstAsync(u => u.Role == "Admin");
        var allUsers = await _context.Users.ToListAsync();

        // Dá»± Ã¡n 1: Qaly MVP
        var qalyMvp = new Project
        {
            Name = "Há»‡ thá»‘ng Quáº£n lÃ½ Qaly MVP",
            Code = "qaly-mvp",
            Description = "Dá»± Ã¡n phÃ¡t triá»ƒn ná»n táº£ng quáº£n lÃ½ cÃ´ng viá»‡c táº­p trung, tÃ­ch há»£p trÃ­ tuá»‡ nhÃ¢n táº¡o Ä‘á»ƒ tá»‘i Æ°u hÃ³a hiá»‡u suáº¥t lÃ m viá»‡c nhÃ³m. Ná»n táº£ng nÃ y há»— trá»£ Ä‘a dá»± Ã¡n, realtime notifications vÃ  phÃ¢n tÃ­ch dá»¯ liá»‡u thÃ´ng minh.",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-10),
            EndDate = DateTimeOffset.UtcNow.AddMonths(3)
        };

        // Dá»± Ã¡n 2: AI Lab
        var aiLab = new Project
        {
            Name = "PhÃ²ng Lab NghiÃªn cá»©u AI Qaly",
            Code = "ai-lab",
            Description = "NghiÃªn cá»©u cÃ¡c kiáº¿n trÃºc LLM má»›i vÃ  tá»‘i Æ°u hÃ³a bá»™ nhá»› cho há»‡ thá»‘ng cháº¡y local. Táº­p trung vÃ o cÃ¡c mÃ´ hÃ¬nh nhá» (Small Language Models) nhÆ° Phi-3, Gemma.",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            EndDate = DateTimeOffset.UtcNow.AddMonths(6)
        };

        // Dá»± Ã¡n 3: Fintech Security
        var fintech = new Project
        {
            Name = "Kiá»ƒm Ä‘á»‹nh Báº£o máº­t Fintech 2026",
            Code = "fintech-security-2026",
            Description = "Dá»± Ã¡n Ä‘Ã¡nh giÃ¡ an ninh máº¡ng cho há»‡ thá»‘ng thanh toÃ¡n ngÃ¢n hÃ ng. Bao gá»“m Pentest, rÃ  soÃ¡t lá»— há»•ng vÃ  tÆ° váº¥n kiáº¿n trÃºc Zero Trust.",
            Status = "Planned",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(15),
            EndDate = DateTimeOffset.UtcNow.AddMonths(4)
        };

        // Dá»± Ã¡n 4: Smart City
        var smartCity = new Project
        {
            Name = "Háº¡ táº§ng Smart City Qaly",
            Code = "smart-city-qaly",
            Description = "XÃ¢y dá»±ng há»‡ thá»‘ng IoT giÃ¡m sÃ¡t giao thÃ´ng vÃ  mÃ´i trÆ°á»ng Ä‘Ã´ thá»‹. TÃ­ch há»£p AI Ä‘á»ƒ dá»± bÃ¡o táº¯c ngháº½n giao thÃ´ng.",
            Status = "Active",
            OwnerId = admin.Id,
            StartDate = DateTimeOffset.UtcNow.AddDays(-30),
            EndDate = DateTimeOffset.UtcNow.AddYears(1)
        };

        await _context.Projects.AddRangeAsync(qalyMvp, aiLab, fintech, smartCity);
        await _context.SaveChangesAsync();

        // ThÃªm thÃ nh viÃªn cho Qaly MVP
        foreach (var user in allUsers.Skip(1).Take(4))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = qalyMvp.Id, UserId = user.Id, Role = user.Email.Contains("nguyenvana") ? "Manager" : "Member" });
        }

        // ThÃªm thÃ nh viÃªn cho AI Lab
        foreach (var user in allUsers.Skip(5).Take(3))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = aiLab.Id, UserId = user.Id, Role = "Member" });
        }

        // ThÃªm thÃ nh viÃªn cho Smart City
        foreach (var user in allUsers.Skip(2).Take(6))
        {
            await _context.ProjectMembers.AddAsync(new ProjectMember { ProjectId = smartCity.Id, UserId = user.Id, Role = "Member" });
        }

        // ThÃªm cÃ´ng viá»‡c máº«u cho Qaly MVP
        var qalyTasks = new List<TaskItem>
        {
            new() { Title = "Thiáº¿t káº¿ cÆ¡ sá»Ÿ dá»¯ liá»‡u chi tiáº¿t", Status = "Done", Priority = "High", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = admin.Id, Description = "XÃ¢y dá»±ng cáº¥u trÃºc SQL Server cho cÃ¡c báº£ng core. ÄÃ£ hoÃ n thÃ nh migration." },
            new() { Title = "Triá»ƒn khai Auth & Role", Status = "Done", Priority = "High", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = allUsers[1].Id, Description = "Sá»­ dá»¥ng Cookie Auth. Há»— trá»£ Admin vÃ  Member roles." },
            new() { Title = "TÃ­ch há»£p AI Erumi", Status = "InProgress", Priority = "High", ProjectId = qalyMvp.Id, ReporterId = admin.Id, AssigneeId = allUsers[2].Id, Description = "Triá»ƒn khai RAG vá»›i Qdrant vÃ  Ollama. Hiá»‡n Ä‘ang tinh chá»‰nh prompt." },
            new() { Title = "Xuáº¥t bÃ¡o cÃ¡o Excel/Word", Status = "Todo", Priority = "Medium", ProjectId = qalyMvp.Id, ReporterId = admin.Id, Description = "Sá»­ dá»¥ng ClosedXML Ä‘á»ƒ táº¡o file bÃ¡o cÃ¡o dá»± Ã¡n tá»± Ä‘á»™ng." }
        };

        // ThÃªm cÃ´ng viá»‡c máº«u cho AI Lab
        var aiTasks = new List<TaskItem>
        {
            new() { Title = "Benchmark Llama 3.2 1B", Status = "Done", Priority = "High", ProjectId = aiLab.Id, ReporterId = admin.Id, AssigneeId = allUsers[5].Id, Description = "Äo lÆ°á»ng throughput trÃªn RTX 4090." },
            new() { Title = "Tá»‘i Æ°u Embedding Pipeline", Status = "InProgress", Priority = "Medium", ProjectId = aiLab.Id, ReporterId = admin.Id, AssigneeId = allUsers[6].Id, Description = "Sá»­ dá»¥ng Batching Ä‘á»ƒ tÄƒng tá»‘c Ä‘á»™ náº¡p dá»¯ liá»‡u vÃ o Qdrant." }
        };

        // ThÃªm cÃ´ng viá»‡c máº«u cho Smart City (QuÃ¡ háº¡n)
        var smartTasks = new List<TaskItem>
        {
            new() { Title = "CÃ i Ä‘áº·t Sensor táº¡i Quáº­n 1", Status = "InProgress", Priority = "High", ProjectId = smartCity.Id, ReporterId = admin.Id, AssigneeId = allUsers[8].Id, DueDate = DateTimeOffset.UtcNow.AddDays(-5), Description = "Láº¯p Ä‘áº·t 50 cáº£m biáº¿n khÃ´ng khÃ­. Äang bá»‹ cháº­m do thiáº¿u linh kiá»‡n." },
            new() { Title = "Viáº¿t API thu tháº­p dá»¯ liá»‡u", Status = "Todo", Priority = "High", ProjectId = smartCity.Id, ReporterId = admin.Id, AssigneeId = allUsers[9].Id, DueDate = DateTimeOffset.UtcNow.AddDays(-2), Description = "PhÃ¡t triá»ƒn endpoint nháº­n dá»¯ liá»‡u tá»« gateway qua MQTT." }
        };

        await _context.TaskItems.AddRangeAsync(qalyTasks);
        await _context.TaskItems.AddRangeAsync(aiTasks);
        await _context.TaskItems.AddRangeAsync(smartTasks);
        await _context.SaveChangesAsync();

        // ThÃªm comment cho task AI
        var aiTask = qalyTasks.First(t => t.Title.Contains("AI"));
        await _context.TaskComments.AddRangeAsync(new List<TaskComment>
        {
            new() { TaskItemId = aiTask.Id, AuthorId = allUsers[1].Id, Content = "Erumi tráº£ lá»i ráº¥t nhanh vá»›i streaming API má»›i." },
            new() { TaskItemId = aiTask.Id, AuthorId = admin.Id, Content = "Cáº§n bá»• sung thÃªm kháº£ nÄƒng táº¡o file bÃ¡o cÃ¡o." }
        });

        LogSeededProject(_logger, "Multi-Projects", 4);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "CÆ¡ sá»Ÿ dá»¯ liá»‡u Ä‘Ã£ migrate thÃ nh cÃ´ng.")]
    private static partial void LogDatabaseMigrated(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Dá»¯ liá»‡u máº«u Ä‘Ã£ Ä‘Æ°á»£c táº¡o thÃ nh cÃ´ng.")]
    private static partial void LogSeedDataCreated(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "CÆ¡ sá»Ÿ dá»¯ liá»‡u Ä‘Ã£ cÃ³ dá»¯ liá»‡u. Bá» qua bÆ°á»›c seed.")]
    private static partial void LogSeedSkipped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "ÄÃ£ seed {UserCount} ngÆ°á»i dÃ¹ng.")]
    private static partial void LogSeededUsers(ILogger logger, int userCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "ÄÃ£ seed dá»± Ã¡n '{ProjectName}' vá»›i {TaskCount} cÃ´ng viá»‡c.")]
    private static partial void LogSeededProject(ILogger logger, string projectName, int taskCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "PhÃ¡t hiá»‡n dá»¯ liá»‡u cÅ©, tiáº¿n hÃ nh xÃ³a Ä‘á»ƒ re-seed báº£n Tiáº¿ng Viá»‡t má»›i...")]
    private static partial void LogStaleDataDetected(ILogger logger);
}
