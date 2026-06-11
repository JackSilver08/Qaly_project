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

    [Description("Lấy tóm tắt thống kê của một dự án.")]
    public async Task<string> GetProjectSummary(
        [Description("ID của dự án")] Guid projectId)
    {
        var result = await _projectService.GetByIdAsync(projectId);
        if (!result.IsSuccess || result.Data == null) return "Không tìm thấy dự án hoặc bạn không có quyền truy cập.";

        var tasksResult = await _taskService.GetByProjectAsync(projectId, pageSize: 1000);
        if (!tasksResult.IsSuccess) return $"Dự án: {result.Data.Name}. Không thể lấy danh sách task.";

        var tasks = tasksResult.Data!.Items;
        var total = tasks.Count;
        var done = tasks.Count(t => t.Status == "Done");
        var inProgress = tasks.Count(t => t.Status == "InProgress");
        var overdue = tasks.Count(t => t.DueDate < DateTimeOffset.UtcNow && t.Status != "Done");

        return $"Dự án: {result.Data.Name}. Tổng số công việc: {total}. Hoàn thành: {done}. Đang làm: {inProgress}. Quá hạn: {overdue}. Mô tả: {result.Data.Description}";
    }

    [Description("Lấy danh sách các công việc quá hạn của một dự án.")]
    public async Task<string> GetOverdueTasks(
        [Description("ID của dự án")] Guid projectId)
    {
        var tasksResult = await _taskService.GetByProjectAsync(projectId, pageSize: 1000);
        if (!tasksResult.IsSuccess) return "Không thể lấy danh sách công việc.";

        var overdueTasks = tasksResult.Data!.Items
            .Where(t => t.DueDate < DateTimeOffset.UtcNow && t.Status != "Done")
            .Select(t => $"- {t.Title} (Hạn: {t.DueDate:dd/MM/yyyy}, Người làm: {t.AssigneeName ?? "Chưa phân công"})")
            .ToList();

        if (overdueTasks.Count == 0) return "Hiện tại không có công việc nào quá hạn.";
        return "Các công việc quá hạn:\n" + string.Join("\n", overdueTasks);
    }

    [Description("Tạo một công việc mới trong dự án.")]
    public async Task<string> CreateTask(
        [Description("ID của dự án")] Guid projectId,
        [Description("Tiêu đề công việc")] string title,
        [Description("Mô tả chi tiết")] string? description = null,
        [Description("Độ ưu tiên (Low, Medium, High, Critical)")] string priority = "Medium",
        [Description("ID người thực hiện")] Guid? assigneeId = null,
        [Description("Hạn chót (định dạng ISO 8601)")] DateTimeOffset? dueDate = null)
    {
        var dto = new CreateTaskDto(title, description, priority, dueDate, null, projectId, assigneeId);
        var result = await _taskService.CreateAsync(dto);

        if (result.IsSuccess)
        {
            return $"Đã tạo công việc thành công: {title} (ID: {result.Data!.Id})";
        }

        return $"Lỗi khi tạo công việc: {result.Error}";
    }

    [Description("Cập nhật trạng thái của một công việc.")]
    public async Task<string> UpdateTaskStatus(
        [Description("ID của công việc")] Guid taskId,
        [Description("Trạng thái mới (Todo, InProgress, InReview, Done, Cancelled)")] string status)
    {
        var result = await _taskService.UpdateStatusAsync(taskId, status);
        if (result.IsSuccess)
        {
            return $"Đã cập nhật trạng thái công việc sang: {status}";
        }
        return $"Lỗi khi cập nhật trạng thái: {result.Error}";
    }

    [Description("Phân công công việc cho một thành viên.")]
    public async Task<string> AssignTask(
        [Description("ID của công việc")] Guid taskId,
        [Description("ID của người thực hiện")] Guid assigneeId)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "Không tìm thấy công việc.";

        var task = taskResult.Data!;
        var dto = new UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, assigneeId, task.IsPrivate);
        
        var result = await _taskService.UpdateAsync(taskId, dto);
        if (result.IsSuccess)
        {
            return $"Đã phân công công việc cho thành viên (ID: {assigneeId})";
        }
        return $"Lỗi khi phân công: {result.Error}";
    }

    [Description("Đặt độ ưu tiên cho công việc.")]
    public async Task<string> SetTaskPriority(
        [Description("ID của công việc")] Guid taskId,
        [Description("Độ ưu tiên (Low, Medium, High, Critical)")] string priority)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "Không tìm thấy công việc.";

        var task = taskResult.Data!;
        var dto = new UpdateTaskDto(task.Title, task.Description, task.Status, priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);

        var result = await _taskService.UpdateAsync(taskId, dto);
        if (result.IsSuccess)
        {
            return $"Đã cập nhật độ ưu tiên thành: {priority}";
        }
        return $"Lỗi khi cập nhật độ ưu tiên: {result.Error}";
    }

    [Description("Đặt hạn chót cho công việc.")]
    public async Task<string> AddDueDate(
        [Description("ID của công việc")] Guid taskId,
        [Description("Hạn chót (ISO 8601)")] DateTimeOffset dueDate)
    {
        var taskResult = await _taskService.GetByIdAsync(taskId);
        if (!taskResult.IsSuccess) return "Không tìm thấy công việc.";

        var task = taskResult.Data!;
        var dto = new UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, dueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);

        var result = await _taskService.UpdateAsync(taskId, dto);
        if (result.IsSuccess)
        {
            return $"Đã cập nhật hạn chót thành: {dueDate:dd/MM/yyyy HH:mm}";
        }
        return $"Lỗi khi cập nhật hạn chót: {result.Error}";
    }

    [Description("Thêm bình luận vào một công việc.")]
    public async Task<string> AddComment(
        [Description("ID của công việc")] Guid taskId,
        [Description("Nội dung bình luận")] string content)
    {
        var dto = new CreateCommentDto(content, taskId);
        var result = await _commentService.CreateAsync(dto);
        if (result.IsSuccess)
        {
            return "Đã thêm bình luận thành công.";
        }
        return $"Lỗi khi thêm bình luận: {result.Error}";
    }

    [Description("Kiểm tra khối lượng công việc của các thành viên trong dự án.")]
    public async Task<string> GetMemberWorkload(
        [Description("ID của dự án")] Guid projectId)
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
            return $"- {m.User.FullName}: {count} công việc đang thực hiện.";
        });

        return "Khối lượng công việc hiện tại:\n" + string.Join("\n", report);
    }

    [Description("Lấy danh sách công việc của một thành viên cụ thể.")]
    public async Task<string> ListTasksByAssignee(
        [Description("ID của thành viên")] Guid assigneeId)
    {
        var result = await _taskService.GetByAssigneeAsync(assigneeId);
        if (!result.IsSuccess) return "Không thể lấy danh sách công việc.";

        var tasks = result.Data!.Items.Select(t => $"- {t.Title} (Trạng thái: {t.Status}, Dự án: {t.ProjectName})");
        return $"Danh sách công việc của thành viên:\n" + string.Join("\n", tasks);
    }

    [Description("Tìm kiếm thông tin, kiến thức trong dự án (Wiki, Tasks, Comments).")]
    public async Task<string> SearchKnowledge(
        [Description("Câu truy vấn tìm kiếm")] string query,
        [Description("ID của dự án (tùy chọn)")] Guid? projectId = null)
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
        if (results.Count == 0) return "Không tìm thấy thông tin liên quan.";

        var response = results.Select(r => $"- [{r.Payload.GetValueOrDefault("ContentType")}]: {r.Payload.GetValueOrDefault("Content")}");
        return "Kết quả tìm kiếm:\n" + string.Join("\n", response);
    }

    [Description("Xuất báo cáo dự án ra file Excel.")]
    public async Task<string> GenerateExcelReport(
        [Description("ID của dự án")] Guid projectId)
    {
        var project = await _projectRepo.GetQueryable()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project != null)
        {
            await _exportService.ExportProjectToExcelAsync(project);
        }

        return $"Báo cáo Excel đã sẵn sàng. Bạn có thể tải tại đây: [📥 Tải báo cáo Excel](/api/ai/export/{projectId}?format=excel)";
    }

    [Description("Xuất báo cáo dự án ra file Word.")]
    public async Task<string> GenerateWordReport(
        [Description("ID của dự án")] Guid projectId)
    {
        var project = await _projectRepo.GetQueryable()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project != null)
        {
            await _exportService.ExportProjectToWordAsync(project);
        }

        return $"Báo cáo Word đã sẵn sàng. Bạn có thể tải tại đây: [📥 Tải báo cáo Word](/api/ai/export/{projectId}?format=word)";
    }

    [Description("Bắt đầu tính giờ làm việc cho một công việc.")]
    public async Task<string> StartTimeTracking(
        [Description("ID của công việc")] Guid taskId)
    {
        var result = await _timeTrackingService.StartTimerAsync(taskId);
        if (result.IsSuccess)
        {
            return "Đã bắt đầu tính giờ làm việc.";
        }
        return $"Lỗi khi bắt đầu tính giờ: {result.Error}";
    }

    [Description("Dừng tính giờ làm việc hiện tại.")]
    public async Task<string> StopTimeTracking(
        [Description("ID của bản ghi tính giờ (entry Id)")] Guid entryId)
    {
        var result = await _timeTrackingService.StopTimerAsync(entryId);
        if (result.IsSuccess)
        {
            return $"Đã dừng tính giờ. Tổng thời gian: {result.Data!.TotalMinutes} phút.";
        }
        return $"Lỗi khi dừng tính giờ: {result.Error}";
    }

    [Description("Lấy lịch sử ghi nhận thời gian của bản thân trong một dự án.")]
    public async Task<string> GetMyTimeLogs(
        [Description("ID của dự án")] Guid projectId)
    {
        var result = await _timeTrackingService.GetByProjectAsync(projectId);
        if (result.IsSuccess)
        {
            var myLogs = result.Data!.Where(l => l.UserId == _currentUserService.UserId).ToList();
            if (myLogs.Count == 0) return "Bạn chưa có ghi nhận thời gian nào trong dự án này.";

            var total = myLogs.Sum(l => l.TotalMinutes);
            var logs = myLogs.Take(10).Select(l => $"- {l.TaskTitle}: {l.TotalMinutes} phút ({l.StartedAt:dd/MM/yyyy})");
            return $"Tổng thời gian ghi nhận: {total} phút.\nChi tiết (10 bản ghi gần nhất):\n" + string.Join("\n", logs);
        }
        return $"Lỗi khi lấy lịch sử thời gian: {result.Error}";
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

    public List<AITool> GetAvailableTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(GetProjectSummary),
            AIFunctionFactory.Create(GetOverdueTasks),
            AIFunctionFactory.Create(CreateTask),
            AIFunctionFactory.Create(UpdateTaskStatus),
            AIFunctionFactory.Create(AssignTask),
            AIFunctionFactory.Create(SuggestTaskAssignment),
            AIFunctionFactory.Create(SetTaskPriority),
            AIFunctionFactory.Create(AddDueDate),
            AIFunctionFactory.Create(AddComment),
            AIFunctionFactory.Create(GetMemberWorkload),
            AIFunctionFactory.Create(SearchKnowledge),
            AIFunctionFactory.Create(StartTimeTracking),
            AIFunctionFactory.Create(StopTimeTracking),
            AIFunctionFactory.Create(GetMyTimeLogs)
        };
    }
}
