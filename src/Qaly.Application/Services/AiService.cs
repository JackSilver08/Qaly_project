using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text;

namespace Qaly.Application.Services;

public partial class AiService : IAiService
{
    private readonly IChatClient _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStorageService _vectorStorage;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AiService> _logger;
    private const string CollectionName = "qaly_context";

    public AiService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStorageService vectorStorage,
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        ICurrentUserService currentUserService,
        ILogger<AiService> logger)
    {
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        _vectorStorage = vectorStorage;
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<string> SuggestTaskPriorityAsync(string taskTitle, string taskDescription, string projectContext)
    {
        var prompt = $@"Dựa trên thông tin công việc sau, hãy đề xuất độ ưu tiên (Low, Medium, High, Critical) và giải thích lý do ngắn gọn.
Dự án: {projectContext}
Công việc: {taskTitle}
Mô tả: {taskDescription}

Trả lời theo định dạng: [Priority] - [Lý do]";

        var response = await _chatClient.CompleteAsync(prompt);
        return response.Message.Text ?? "Medium - Không thể xác định";
    }

    public async Task<string> GenerateProjectSummaryAsync(Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            LogUnauthorizedSummaryRequest(_logger, projectId, _currentUserService.UserId ?? Guid.Empty);
            return "Bạn không có quyền truy cập thông tin dự án này.";
        }

        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project == null) return "Không tìm thấy dự án.";

        var prompt = $"Hãy tóm tắt tình trạng hiện tại của dự án '{project.Name}'. Mô tả: {project.Description}";
        var response = await _chatClient.CompleteAsync(prompt);
        return response.Message.Text ?? "Không thể tạo tóm tắt.";
    }

    public async Task<string> AnalyzeProjectRisksAsync(Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            LogUnauthorizedRiskAnalysisRequest(_logger, projectId, _currentUserService.UserId ?? Guid.Empty);
            return "Bạn không có quyền truy cập dữ liệu dự án này để phân tích rủi ro.";
        }

        var project = await GetProjectWithTasksAsync(projectId);
        if (project == null) return "Không tìm thấy dự án.";

        var overdueCount = project.Tasks.Count(IsTaskOverdue);
        var prompt = $"Phân tích rủi ro cho dự án '{project.Name}'. Hiện có {overdueCount} task quá hạn.";
        var response = await _chatClient.CompleteAsync(prompt);
        return response.Message.Text ?? "Không thể phân tích rủi ro.";
    }

    public async Task<string> SuggestTaskAssignmentAsync(Guid taskId, Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return "Bạn không có quyền truy cập dự án này.";
        }

        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task == null) return "Không tìm thấy công việc.";

        var prompt = $"Đề xuất thành viên phù hợp để thực hiện task: {task.Title}";
        var response = await _chatClient.CompleteAsync(prompt);
        return response.Message.Text ?? "Không thể đưa ra đề xuất.";
    }

    public async Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null)
    {
        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            return new List<string> { "Bạn không có quyền tìm kiếm trong dự án này." };
        }

        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = queryEmbedding[0].Vector.ToArray();

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, limit: 5);
        return results.Select(r => r.Payload.GetValueOrDefault("Content")?.ToString() ?? "").ToList();
    }

    public async Task<IReadOnlyList<string>> GenerateSubtasksAsync(string taskTitle, string taskDescription)
    {
        var prompt = $@"Hãy chia nhỏ công việc sau thành các sub-tasks thực tế (tối đa 5 task).
Công việc chính: {taskTitle}
Mô tả: {taskDescription}

Trả lời dưới dạng danh sách gạch đầu dòng.";

        var response = await _chatClient.CompleteAsync(prompt);
        var text = response.Message.Text ?? "";
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.TrimStart('-', ' ', '1', '2', '3', '.', '*'))
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .ToList();
    }

    public async Task<string> ChatAsync(string userMessage, Guid? projectId = null)
    {
        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            return "Bạn không có quyền truy cập vào dữ liệu của dự án này.";
        }

        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { userMessage });
        var vector = queryEmbedding[0].Vector.ToArray();

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, limit: 5);
        
        var contextBuilder = new StringBuilder();
        foreach (var res in results)
        {
            contextBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"- [Loại: {res.Payload.GetValueOrDefault("Type")}]: {res.Payload.GetValueOrDefault("Content")}");
            contextBuilder.AppendLine();
        }

        var exportLink = projectId != null ? $"/api/ai/export/{projectId}?format=excel" : "#";
        var systemPrompt = $@"Bạn là Erumi (Erumi-chan), một Trợ lý ảo AI thông minh, tận tâm và chuyên nghiệp của hệ thống quản lý dự án Qaly.

PHONG CÁCH LÀM VIỆC:
1. Luôn lịch sự, sử dụng ngôn ngữ Tiếng Việt chuẩn mực, chuyên nghiệp nhưng vẫn thân thiện.
2. Trả lời súc tích, đi thẳng vào vấn đề.
3. Sử dụng định dạng Markdown (gạch đầu dòng, in đậm, bảng).

KIẾN THỨC VỀ HỆ THỐNG:
- Bạn có quyền truy cập vào thông tin về Projects, Tasks thông qua ngữ cảnh.
- Bạn có thể hỗ trợ xuất báo cáo. Nếu người dùng yêu cầu xuất file Excel hoặc báo cáo dự án, hãy cung cấp link theo định dạng Markdown sau:
  [📥 Tải báo cáo Excel dự án]({exportLink})
  (Lưu ý: Chỉ cung cấp link nếu có projectId hoặc người dùng đang hỏi về một dự án cụ thể).

NGỮ CẢNH DỮ LIỆU HIỆN TẠI (RAG Context):
{contextBuilder}

HƯỚNG DẪN TRẢ LỜI:
- Dựa TRỰC TIẾP vào ngữ cảnh để trả lời.
- Sử dụng bảng Markdown nếu cần so sánh dữ liệu.
- Nếu người dùng muốn xuất file, hãy đưa ra link tải như hướng dẫn trên.

Thời gian hiện tại: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";

        var chatHistory = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        };

        var response = await _chatClient.CompleteAsync(chatHistory);
        return response.Message.Text ?? "Xin lỗi, tôi gặp chút trục trặc khi kết nối với bộ não AI. Vui lòng thử lại sau giây lát.";
    }

    public async IAsyncEnumerable<string> ChatStreamingAsync(string userMessage, Guid? projectId = null)
    {
        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            yield return "Bạn không có quyền truy cập vào dữ liệu của dự án này.";
            yield break;
        }

        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { userMessage });
        var vector = queryEmbedding[0].Vector.ToArray();

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, limit: 5);

        var contextBuilder = new StringBuilder();
        foreach (var res in results)
        {
            contextBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"- [Loại: {res.Payload.GetValueOrDefault("Type")}]: {res.Payload.GetValueOrDefault("Content")}");
            contextBuilder.AppendLine();
        }

        var exportLink = projectId != null ? $"/api/ai/export/{projectId}?format=excel" : "#";
        var systemPrompt = $@"Bạn là Erumi (Erumi-chan), một Trợ lý ảo AI thông minh, tận tâm và chuyên nghiệp của hệ thống quản lý dự án Qaly.

PHONG CÁCH LÀM VIỆC:
1. Luôn lịch sự, sử dụng ngôn ngữ Tiếng Việt chuẩn mực, chuyên nghiệp nhưng vẫn thân thiện.
2. Trả lời súc tích, đi thẳng vào vấn đề.
3. Sử dụng định dạng Markdown (gạch đầu dòng, in đậm, bảng).

KIẾN THỨC VỀ HỆ THỐNG:
- Bạn có quyền truy cập vào thông tin về Projects, Tasks thông qua ngữ cảnh.
- Bạn có thể hỗ trợ xuất báo cáo. Nếu người dùng yêu cầu xuất file Excel hoặc báo cáo dự án, hãy cung cấp link theo định dạng Markdown sau:
  [📥 Tải báo cáo Excel dự án]({exportLink})
  (Lưu ý: Chỉ cung cấp link nếu có projectId hoặc người dùng đang hỏi về một dự án cụ thể).

NGỮ CẢNH DỮ LIỆU HIỆN TẠI:
{contextBuilder}

HƯỚNG DẪN:
- Dựa TRỰC TIẾP vào ngữ cảnh để trả lời.
- Sử dụng bảng Markdown nếu cần so sánh dữ liệu.
- Nếu người dùng muốn xuất file, hãy đưa ra link tải như hướng dẫn trên.

Thời gian: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";

        var chatHistory = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        };

        await foreach (var update in _chatClient.CompleteStreamingAsync(chatHistory))
        {
            if (update.Text != null)
            {
                yield return update.Text;
            }
        }
    }

    public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return null;
        }

        return await _projectRepo.GetQueryable()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId);
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        if (string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project == null) return false;

        if (project.OwnerId == currentUserId) return true;

        return await _memberRepo.GetQueryable()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == currentUserId);
    }

    private static bool IsDone(TaskItem task)
        => IsStatus(task, "Done");

    private static bool IsStatus(TaskItem task, string status)
        => string.Equals(task.Status, status, StringComparison.OrdinalIgnoreCase);

    private static bool IsTaskOverdue(TaskItem task)
        => task.DueDate.HasValue && task.DueDate.Value < DateTimeOffset.UtcNow && !IsDone(task);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Unauthorized AI summary request for project {ProjectId} by user {UserId}")]
    private static partial void LogUnauthorizedSummaryRequest(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unauthorized AI risk analysis request for project {ProjectId} by user {UserId}")]
    private static partial void LogUnauthorizedRiskAnalysisRequest(ILogger logger, Guid projectId, Guid userId);
}

