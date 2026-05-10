using System.ComponentModel;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;
using Qaly.Application.DTOs.Comment;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace Qaly.Application.Services;

public class AiTools
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;
    private readonly ICommentService _commentService;
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly IAiExportService _exportService;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVectorStorageService _vectorStorage;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private const string CollectionName = "qaly_context";

    public AiTools(
        ITaskService taskService,
        IProjectService projectService,
        ICommentService commentService,
        ITimeTrackingService timeTrackingService,
        IAiExportService exportService,
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        ICurrentUserService currentUserService,
        IVectorStorageService vectorStorage,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _taskService = taskService;
        _projectService = projectService;
        _commentService = commentService;
        _timeTrackingService = timeTrackingService;
        _exportService = exportService;
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _currentUserService = currentUserService;
        _vectorStorage = vectorStorage;
        _embeddingGenerator = embeddingGenerator;
    }

    [Description("Láº¥y tÃ³m táº¯t thá»‘ng kÃª cá»§a má»™t dá»± Ã¡n.")]
    public async Task<string> GetProjectSummary(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId)
    {
        var result = await _projectService.GetByIdAsync(projectId);
        if (!result.IsSuccess || result.Data == null) return "KhÃ´ng tÃ¬m tháº¥y dá»± Ã¡n hoáº·c báº¡n khÃ´ng cÃ³ quyá»n truy cáº­p.";

        var tasksResult = await _taskService.GetByProjectAsync(projectId, pageSize: 1000);
        if (!tasksResult.IsSuccess) return $"Dá»± Ã¡n: {result.Data.Name}. KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch task.";

        var tasks = tasksResult.Data!.Items;
        var total = tasks.Count;
        var done = tasks.Count(t => t.Status == "Done");
        var inProgress = tasks.Count(t => t.Status == "InProgress");
        var overdue = tasks.Count(t => t.DueDate < DateTimeOffset.UtcNow && t.Status != "Done");

        return $"Dá»± Ã¡n: {result.Data.Name}. Tá»•ng sá»‘ cÃ´ng viá»‡c: {total}. HoÃ n thÃ nh: {done}. Äang lÃ m: {inProgress}. QuÃ¡ háº¡n: {overdue}. MÃ´ táº£: {result.Data.Description}";
    }

    [Description("Láº¥y danh sÃ¡ch cÃ¡c cÃ´ng viá»‡c quÃ¡ háº¡n cá»§a má»™t dá»± Ã¡n.")]
    public async Task<string> GetOverdueTasks(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId)
    {
        var tasksResult = await _taskService.GetByProjectAsync(projectId, pageSize: 1000);
        if (!tasksResult.IsSuccess) return "KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch cÃ´ng viá»‡c.";

        var overdueTasks = tasksResult.Data!.Items
            .Where(t => t.DueDate < DateTimeOffset.UtcNow && t.Status != "Done")
            .Select(t => $"- {t.Title} (Háº¡n: {t.DueDate:dd/MM/yyyy}, NgÆ°á»i lÃ m: {t.AssigneeName ?? "ChÆ°a phÃ¢n cÃ´ng"})")
            .ToList();

        if (overdueTasks.Count == 0) return "Hiá»‡n táº¡i khÃ´ng cÃ³ cÃ´ng viá»‡c nÃ o quÃ¡ háº¡n.";
        return "CÃ¡c cÃ´ng viá»‡c quÃ¡ háº¡n:\n" + string.Join("\n", overdueTasks);
    }

    [Description("Táº¡o má»™t cÃ´ng viá»‡c má»›i trong dá»± Ã¡n.")]
    public async Task<string> CreateTask(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId,
        [Description("TiÃªu Ä‘á» cÃ´ng viá»‡c")] string title,
        [Description("MÃ´ táº£ chi tiáº¿t")] string? description = null,
        [Description("Äá»™ Æ°u tiÃªn (Low, Medium, High, Critical)")] string priority = "Medium",
        [Description("ID ngÆ°á»i thá»±c hiá»‡n")] Guid? assigneeId = null,
        [Description("Háº¡n chÃ³t (Ä‘á»‹nh dáº¡ng ISO 8601)")] DateTimeOffset? dueDate = null)
    {
        var dto = new CreateTaskDto(title, description, priority, dueDate, null, projectId, assigneeId);
        var result = await _taskService.CreateAsync(dto);

        if (result.IsSuccess)
        {
            return $"ÄÃ£ táº¡o cÃ´ng viá»‡c thÃ nh cÃ´ng: {title} (ID: {result.Data!.Id})";
        }

        return $"Lá»—i khi táº¡o cÃ´ng viá»‡c: {result.Error}";
    }

    [Description("Cáº­p nháº­t tráº¡ng thÃ¡i cá»§a má»™t cÃ´ng viá»‡c.")]
    public async Task<string> UpdateTaskStatus(
        [Description("ID cá»§a cÃ´ng viá»‡c")] Guid taskId,
        [Description("Tráº¡ng thÃ¡i má»›i (Todo, InProgress, InReview, Done, Cancelled)")] string status)
    {
        var result = await _taskService.UpdateStatusAsync(taskId, status);
        if (result.IsSuccess)
        {
            return $"ÄÃ£ cáº­p nháº­t tráº¡ng thÃ¡i cÃ´ng viá»‡c sang: {status}";
        }
        return $"Lá»—i khi cáº­p nháº­t tráº¡ng thÃ¡i: {result.Error}";
    }

    [Description("PhÃ¢n cÃ´ng cÃ´ng viá»‡c cho má»™t thÃ nh viÃªn.")]
    public async Task<string> AssignTask(
        [Description("ID cá»§a cÃ´ng viá»‡c")] Guid taskId,
        [Description("ID cá»§a ngÆ°á»i thá»±c hiá»‡n")] Guid assigneeId)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "KhÃ´ng tÃ¬m tháº¥y cÃ´ng viá»‡c.";

        var task = taskResult.Data!;
        var dto = new UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, assigneeId, task.IsPrivate);
        
        var result = await _taskService.UpdateAsync(taskId, dto);
        if (result.IsSuccess)
        {
            return $"ÄÃ£ phÃ¢n cÃ´ng cÃ´ng viá»‡c cho thÃ nh viÃªn (ID: {assigneeId})";
        }
        return $"Lá»—i khi phÃ¢n cÃ´ng: {result.Error}";
    }

    [Description("Äáº·t Ä‘á»™ Æ°u tiÃªn cho cÃ´ng viá»‡c.")]
    public async Task<string> SetTaskPriority(
        [Description("ID cá»§a cÃ´ng viá»‡c")] Guid taskId,
        [Description("Äá»™ Æ°u tiÃªn (Low, Medium, High, Critical)")] string priority)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "KhÃ´ng tÃ¬m tháº¥y cÃ´ng viá»‡c.";

        var task = taskResult.Data!;
        var dto = new UpdateTaskDto(task.Title, task.Description, task.Status, priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);

        var result = await _taskService.UpdateAsync(taskId, dto);
        if (result.IsSuccess)
        {
            return $"ÄÃ£ cáº­p nháº­t Ä‘á»™ Æ°u tiÃªn thÃ nh: {priority}";
        }
        return $"Lá»—i khi cáº­p nháº­t Ä‘á»™ Æ°u tiÃªn: {result.Error}";
    }

    [Description("Äáº·t háº¡n chÃ³t cho cÃ´ng viá»‡c.")]
    public async Task<string> AddDueDate(
        [Description("ID cá»§a cÃ´ng viá»‡c")] Guid taskId,
        [Description("Háº¡n chÃ³t (ISO 8601)")] DateTimeOffset dueDate)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "KhÃ´ng tÃ¬m tháº¥y cÃ´ng viá»‡c.";

        var task = taskResult.Data!;
        var dto = new UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, dueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);

        var result = await _taskService.UpdateAsync(taskId, dto);
        if (result.IsSuccess)
        {
            return $"ÄÃ£ cáº­p nháº­t háº¡n chÃ³t thÃ nh: {dueDate:dd/MM/yyyy HH:mm}";
        }
        return $"Lá»—i khi cáº­p nháº­t háº¡n chÃ³t: {result.Error}";
    }

    [Description("ThÃªm bÃ¬nh luáº­n vÃ o má»™t cÃ´ng viá»‡c.")]
    public async Task<string> AddComment(
        [Description("ID cá»§a cÃ´ng viá»‡c")] Guid taskId,
        [Description("Ná»™i dung bÃ¬nh luáº­n")] string content)
    {
        var dto = new CreateCommentDto(content, taskId);
        var result = await _commentService.CreateAsync(dto);
        if (result.IsSuccess)
        {
            return "ÄÃ£ thÃªm bÃ¬nh luáº­n thÃ nh cÃ´ng.";
        }
        return $"Lá»—i khi thÃªm bÃ¬nh luáº­n: {result.Error}";
    }

    [Description("Kiá»ƒm tra khá»‘i lÆ°á»£ng cÃ´ng viá»‡c cá»§a cÃ¡c thÃ nh viÃªn trong dá»± Ã¡n.")]
    public async Task<string> GetMemberWorkload(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId)
    {
        var members = await _memberRepo.GetQueryable()
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .ToListAsync();

        var tasks = await _taskRepo.GetQueryable()
            .Where(t => t.ProjectId == projectId && t.Status != "Done" && t.Status != "Cancelled")
            .ToListAsync();

        var report = members.Select(m => {
            var count = tasks.Count(t => t.AssigneeId == m.UserId);
            return $"- {m.User.FullName}: {count} cÃ´ng viá»‡c Ä‘ang thá»±c hiá»‡n.";
        });

        return "Khá»‘i lÆ°á»£ng cÃ´ng viá»‡c hiá»‡n táº¡i:\n" + string.Join("\n", report);
    }

    [Description("Láº¥y danh sÃ¡ch cÃ´ng viá»‡c cá»§a má»™t thÃ nh viÃªn cá»¥ thá»ƒ.")]
    public async Task<string> ListTasksByAssignee(
        [Description("ID cá»§a thÃ nh viÃªn")] Guid assigneeId)
    {
        var result = await _taskService.GetByAssigneeAsync(assigneeId);
        if (!result.IsSuccess) return "KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch cÃ´ng viá»‡c.";

        var tasks = result.Data!.Items.Select(t => $"- {t.Title} (Tráº¡ng thÃ¡i: {t.Status}, Dá»± Ã¡n: {t.ProjectName})");
        return $"Danh sÃ¡ch cÃ´ng viá»‡c cá»§a thÃ nh viÃªn:\n" + string.Join("\n", tasks);
    }

    [Description("TÃ¬m kiáº¿m thÃ´ng tin, kiáº¿n thá»©c trong dá»± Ã¡n (Wiki, Tasks, Comments).")]
    public async Task<string> SearchKnowledge(
        [Description("CÃ¢u truy váº¥n tÃ¬m kiáº¿m")] string query,
        [Description("ID cá»§a dá»± Ã¡n (tÃ¹y chá»n)")] Guid? projectId = null)
    {
        if (!projectId.HasValue)
        {
            return "Please select a project before searching project knowledge.";
        }

        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = queryEmbedding[0].Vector.ToArray();

        var filter = new VectorFilter
        {
            ProjectId = projectId.Value,
            OwnerId = _currentUserService.UserId
        };

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 5);
        if (results.Count == 0) return "KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin liÃªn quan.";

        var response = results.Select(r => $"- [{r.Payload.GetValueOrDefault("ContentType")}]: {r.Payload.GetValueOrDefault("Content")}");
        return "Káº¿t quáº£ tÃ¬m kiáº¿m:\n" + string.Join("\n", response);
    }

    [Description("Xuáº¥t bÃ¡o cÃ¡o dá»± Ã¡n ra file Excel.")]
    public async Task<string> GenerateExcelReport(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId)
    {
        var project = await _projectRepo.GetQueryable()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project != null)
        {
            await _exportService.ExportProjectToExcelAsync(project);
        }

        return $"BÃ¡o cÃ¡o Excel Ä‘Ã£ sáºµn sÃ ng. Báº¡n cÃ³ thá»ƒ táº£i táº¡i Ä‘Ã¢y: [ðŸ“¥ Táº£i bÃ¡o cÃ¡o Excel](/api/ai/export/{projectId}?format=excel)";
    }

    [Description("Xuáº¥t bÃ¡o cÃ¡o dá»± Ã¡n ra file Word.")]
    public async Task<string> GenerateWordReport(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId)
    {
        var project = await _projectRepo.GetQueryable()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project != null)
        {
            await _exportService.ExportProjectToWordAsync(project);
        }

        return $"BÃ¡o cÃ¡o Word Ä‘Ã£ sáºµn sÃ ng. Báº¡n cÃ³ thá»ƒ táº£i táº¡i Ä‘Ã¢y: [ðŸ“¥ Táº£i bÃ¡o cÃ¡o Word](/api/ai/export/{projectId}?format=word)";
    }

    [Description("Báº¯t Ä‘áº§u tÃ­nh giá» lÃ m viá»‡c cho má»™t cÃ´ng viá»‡c.")]
    public async Task<string> StartTimeTracking(
        [Description("ID cá»§a cÃ´ng viá»‡c")] Guid taskId)
    {
        var result = await _timeTrackingService.StartTimerAsync(taskId);
        if (result.IsSuccess)
        {
            return "ÄÃ£ báº¯t Ä‘áº§u tÃ­nh giá» lÃ m viá»‡c.";
        }
        return $"Lá»—i khi báº¯t Ä‘áº§u tÃ­nh giá»: {result.Error}";
    }

    [Description("Dá»«ng tÃ­nh giá» lÃ m viá»‡c hiá»‡n táº¡i.")]
    public async Task<string> StopTimeTracking(
        [Description("ID cá»§a báº£n ghi tÃ­nh giá» (entry Id)")] Guid entryId)
    {
        var result = await _timeTrackingService.StopTimerAsync(entryId);
        if (result.IsSuccess)
        {
            return $"ÄÃ£ dá»«ng tÃ­nh giá». Tá»•ng thá»i gian: {result.Data!.TotalMinutes} phÃºt.";
        }
        return $"Lá»—i khi dá»«ng tÃ­nh giá»: {result.Error}";
    }

    [Description("Láº¥y lá»‹ch sá»­ ghi nháº­n thá»i gian cá»§a báº£n thÃ¢n trong má»™t dá»± Ã¡n.")]
    public async Task<string> GetMyTimeLogs(
        [Description("ID cá»§a dá»± Ã¡n")] Guid projectId)
    {
        var result = await _timeTrackingService.GetByProjectAsync(projectId);
        if (result.IsSuccess)
        {
            var myLogs = result.Data!.Where(l => l.UserId == _currentUserService.UserId).ToList();
            if (myLogs.Count == 0) return "Báº¡n chÆ°a cÃ³ ghi nháº­n thá»i gian nÃ o trong dá»± Ã¡n nÃ y.";

            var total = myLogs.Sum(l => l.TotalMinutes);
            var logs = myLogs.Take(10).Select(l => $"- {l.TaskTitle}: {l.TotalMinutes} phÃºt ({l.StartedAt:dd/MM/yyyy})");
            return $"Tá»•ng thá»i gian ghi nháº­n: {total} phÃºt.\nChi tiáº¿t (10 báº£n ghi gáº§n nháº¥t):\n" + string.Join("\n", logs);
        }
        return $"Lá»—i khi láº¥y lá»‹ch sá»­ thá»i gian: {result.Error}";
    }
    [Description("Đề xuất thành viên phù hợp nhất để làm một công việc cụ thể dựa vào khối lượng công việc hiện tại.")]
    public async Task<string> SuggestTaskAssignment(
        [Description("ID của công việc cần phân công")] Guid taskId,
        [Description("ID của dự án")] Guid projectId)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "Không tìm thấy công việc.";

        var members = await _memberRepo.GetQueryable()
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .ToListAsync();

        var activeTasks = await _taskRepo.GetQueryable()
            .Where(t => t.ProjectId == projectId && t.Status != "Done" && t.Status != "Cancelled" && t.AssigneeId != null)
            .ToListAsync();

        var workload = members.Select(m => new
        {
            m.User.FullName,
            m.Role,
            ActiveCount = activeTasks.Count(t => t.AssigneeId == m.UserId)
        }).ToList();

        var membersContext = string.Join("\n", workload.Select(w => $"- {w.FullName} (Vai trò: {w.Role}): Đang có {w.ActiveCount} task(s) chưa hoàn thành."));

        return $"Thông tin công việc: {taskResult.Data!.Title}. Danh sách thành viên và khối lượng công việc:\n{membersContext}\n\nHãy tự phân tích và đưa ra đề xuất người phù hợp nhất cho người dùng.";
    }
}

