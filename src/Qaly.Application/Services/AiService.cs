using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text;
using System.Text.Json;

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
    private readonly AiTools _aiTools;
    private const string CollectionName = "qaly_context";
    private static readonly JsonSerializerOptions CategorizationPromptJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };
    private static readonly JsonSerializerOptions CategorizationResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly char[] KeywordSplitSeparators = new[] { ' ', ',', '.', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', '\n', '\r', '\t' };

    public AiService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStorageService vectorStorage,
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        ICurrentUserService currentUserService,
        ILogger<AiService> logger,
        AiTools aiTools)
    {
        _chatClient = chatClient;

        _embeddingGenerator = embeddingGenerator;
        _vectorStorage = vectorStorage;
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _currentUserService = currentUserService;
        _logger = logger;
        _aiTools = aiTools;
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

        var members = await _memberRepo.GetQueryable()
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .ToListAsync();

        var activeTasks = await _taskRepo.GetQueryable()
            .Where(t => t.ProjectId == projectId && t.Status != "Done" && t.AssigneeId != null)
            .ToListAsync();

        var workload = members.Select(m => new
        {
            m.User.FullName,
            m.Role,
            ActiveCount = activeTasks.Count(t => t.AssigneeId == m.UserId)
        }).ToList();

        var membersContext = string.Join("\n", workload.Select(w => $"- {w.FullName} (Vai trò: {w.Role}): Đang có {w.ActiveCount} task(s) chưa hoàn thành."));

        var prompt = $@"Bạn là trợ lý quản lý dự án xuất sắc. Hãy phân tích và đề xuất thành viên phù hợp nhất để thực hiện công việc sau:

Công việc: {task.Title}
Mô tả: {task.Description}

Danh sách thành viên hiện tại trong dự án và khối lượng công việc:
{membersContext}

Yêu cầu:
1. Đề xuất 1-2 người phù hợp nhất (ưu tiên người đang rảnh hoặc có vai trò phù hợp).
2. Giải thích lý do chọn họ dựa trên thông tin trên.
3. Trả lời ngắn gọn, chuyên nghiệp bằng Tiếng Việt.";
        var response = await _chatClient.CompleteAsync(prompt);
        return response.Message.Text ?? "Không thể đưa ra đề xuất.";
    }

    public async Task<Result<TaskAssignmentInsightDto>> GetTaskAssignmentInsightAsync(Guid taskId, Guid projectId, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return Result.Forbidden<TaskAssignmentInsightDto>("Báº¡n khÃ´ng cÃ³ quyá»n truy cáº­p dá»± Ã¡n nÃ y.");
        }

        var task = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Project)
            .Include(item => item.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(item => item.Labels)
                .ThenInclude(label => label.ProjectLabel)
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId, ct);

        if (task == null)
        {
            return Result.NotFound<TaskAssignmentInsightDto>("KhÃ´ng tÃ¬m tháº¥y cÃ´ng viá»‡c.");
        }

        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .Include(member => member.User)
            .ToListAsync(ct);

        var projectTasks = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Where(taskItem => taskItem.ProjectId == projectId)
            .Include(taskItem => taskItem.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(taskItem => taskItem.Labels)
                .ThenInclude(label => label.ProjectLabel)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var taskKeywords = ExtractKeywords(task.Title, task.Description);
        var taskLabels = task.Labels
            .Select(label => label.ProjectLabel?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var candidates = members.Select(member =>
        {
            var activeAssignments = projectTasks
                .Where(taskItem => IsAssignedTo(taskItem, member.UserId) && taskItem.Status is not ("Done" or "Cancelled"))
                .ToList();

            var overdueTaskCount = activeAssignments.Count(item => item.DueDate.HasValue && item.DueDate.Value < now);
            var recentCompletionCount = projectTasks.Count(taskItem =>
                IsAssignedTo(taskItem, member.UserId) &&
                taskItem.Status == "Done" &&
                taskItem.UpdatedAt.HasValue &&
                taskItem.UpdatedAt.Value >= now.AddDays(-90));

            var completedTasks = projectTasks
                .Where(taskItem => IsAssignedTo(taskItem, member.UserId) && taskItem.Status == "Done")
                .ToList();

            var skillSignals = completedTasks
                .SelectMany(item => item.Labels.Select(label => label.ProjectLabel?.Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => group.Key)
                .ToList();

            var matchingLabelCount = skillSignals.Intersect(taskLabels, StringComparer.OrdinalIgnoreCase).Count();
            var matchingKeywordCount = taskKeywords.Count(keyword =>
                completedTasks.Any(item =>
                    item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (item.Description != null && item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))));

            var activeTaskCount = activeAssignments.Count;
            var workloadScore = Math.Max(0, 60 - activeTaskCount * 12 - overdueTaskCount * 8);
            var skillMatchScore = matchingLabelCount * 20 + Math.Min(20, matchingKeywordCount * 4);
            var historyScore = Math.Min(20, recentCompletionCount * 3 + projectTasks.Count(taskItem =>
                IsAssignedTo(taskItem, member.UserId) &&
                taskItem.ReporterId == task.ReporterId));
            var totalScore = workloadScore + skillMatchScore + historyScore;

            var recentSignals = new List<string>();
            if (activeTaskCount == 0)
            {
                recentSignals.Add("DangTrong");
            }
            if (overdueTaskCount > 0)
            {
                recentSignals.Add($"{overdueTaskCount} task qua han");
            }
            if (recentCompletionCount > 0)
            {
                recentSignals.Add($"{recentCompletionCount} task hoan thanh gan day");
            }

            return new TaskAssignmentCandidateDto(
                member.UserId,
                member.User.FullName,
                member.Role,
                activeTaskCount,
                overdueTaskCount,
                recentCompletionCount,
                skillMatchScore,
                historyScore,
                workloadScore,
                totalScore,
                skillSignals,
                recentSignals);
        })
        .OrderByDescending(candidate => candidate.TotalScore)
        .ThenBy(candidate => candidate.ActiveTaskCount)
        .ThenByDescending(candidate => candidate.SkillMatchScore)
        .ToList();

        var recommended = candidates.FirstOrDefault();
        var summary = recommended == null
            ? "Khong co du lieu de de xuat."
            : $"Uu tien {recommended.FullName} vi workload thap, co {recommended.ActiveTaskCount} task dang mo va phu hop voi {string.Join(", ", recommended.SkillSignals.Take(3))}.";

        return Result.Success(new TaskAssignmentInsightDto(
            task.Id,
            projectId,
            task.Title,
            task.Description,
            task.Priority,
            task.Status,
            task.DueDate,
            recommended?.UserId,
            recommended?.FullName ?? string.Empty,
            summary,
            now,
            candidates));
    }

    public async Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null)
    {
        if (!projectId.HasValue)
        {
            return new List<string> { "Please select a project before using AI search." };
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            return new List<string> { "Bạn không có quyền tìm kiếm trong dự án này." };
        }

        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = queryEmbedding[0].Vector.ToArray();

        var filter = new VectorFilter
        {
            ProjectId = projectId.Value,
            OwnerId = _currentUserService.UserId,
            IsPrivate = false // By default, smart search only shows non-private items
        };

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 5);
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

    public async Task<string> ChatAsync(string userMessage, Guid? projectId = null, string mode = "erumi", IList<AiChatMessageDto>? history = null)
    {
        if (!projectId.HasValue)
        {
            return "Please select a project before using Erumi with project data.";
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            return "Bạn không có quyền truy cập vào dữ liệu của dự án này.";
        }

        // Smart RAG: Refine query
        var searchQueries = await RefineSearchQueriesAsync(userMessage);
        
        var contextBuilder = new StringBuilder();
        foreach (var query in searchQueries)
        {
            var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { query });
            var vector = queryEmbedding[0].Vector.ToArray();

            var filter = new VectorFilter
            {
                ProjectId = projectId.Value,
                OwnerId = _currentUserService.UserId
            };

            var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 3);
            foreach (var res in results)
            {
                contextBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"- [Dữ liệu]: {res.Payload.GetValueOrDefault("Content")}");
                contextBuilder.AppendLine();
            }
        }

        var exportLink = projectId != null ? $"/api/ai/export/{projectId}?format=excel" : "#";
        var systemPrompt = $@"Bạn là Erumi (Erumi-chan), một Trợ lý ảo AI thông minh, tận tâm và chuyên nghiệp của hệ thống quản lý dự án Qaly.

PHONG CÁCH & TÍNH CÁCH:
1. Nhiệt tình, chu đáo: Luôn sẵn sàng hỗ trợ người dùng với thái độ tích cực.
2. Chính xác, chuyên nghiệp: Sử dụng ngôn ngữ Tiếng Việt chuẩn mực. Không 'chém gió' nếu không có dữ liệu.
3. Súc tích: Đi thẳng vào vấn đề, sử dụng định dạng Markdown (gạch đầu dòng, bảng, in đậm) để thông tin dễ đọc.

QUY TRÌNH SUY NGHĨ (Chain of Thought):
- Khi nhận được yêu cầu, hãy phân tích xem bạn có cần thêm thông tin từ hệ thống không.
- Nếu cần, hãy sử dụng các công cụ (Tools) được cung cấp (ví dụ: Tạo task, đổi trạng thái, phân công, tính giờ làm việc).
- Sau khi có kết quả từ tool, hãy kết hợp với ngữ cảnh dữ liệu (RAG Context) bên dưới để đưa ra câu trả lời cuối cùng.
- Luôn ưu tiên dữ liệu thực tế từ hệ thống hơn là kiến thức chung của bạn.

KIẾN THỨC VỀ HỆ THỐNG:
- Bạn có quyền truy cập vào Projects, Tasks, và Time Tracking thông qua công cụ.
- Bạn có thể hỗ trợ xuất báo cáo. Nếu người dùng yêu cầu, hãy cung cấp link tải.
  [📥 Tải báo cáo Excel dự án]({exportLink})

NGỮ CẢNH DỮ LIỆU HIỆN TẠI (RAG Context):
{contextBuilder}

HƯỚNG DẪN TRẢ LỜI:
- Dựa TRỰC TIẾP vào ngữ cảnh và kết quả trả về từ công cụ.
- Nếu không tìm thấy thông tin, hãy nói: 'Erumi không tìm thấy dữ liệu này trong hệ thống, bạn có thể cung cấp thêm chi tiết không?'
- Nếu người dùng muốn thực hiện hành động (tạo task, assign, stop timer...), hãy sử dụng tool tương ứng và thông báo kết quả.

ID dự án hiện tại (nếu có): {projectId}
Thời gian hiện tại: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";

        var chatHistory = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt)
        };

        if (history != null)
        {
            foreach (var msg in history)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                    ? ChatRole.Assistant : ChatRole.User;
                chatHistory.Add(new ChatMessage(role, msg.Content));
            }
        }

        chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var options = new ChatOptions
        {
            Tools = GetTools()
        };

        var response = await _chatClient.CompleteAsync(chatHistory, options);
        return response.Message.Text ?? "Xin lỗi, tôi gặp chút trục trặc khi kết nối với bộ não AI. Vui lòng thử lại sau giây lát.";
    }

    public async IAsyncEnumerable<string> ChatStreamingAsync(string userMessage, Guid? projectId = null, string mode = "erumi", IList<AiChatMessageDto>? history = null)
    {
        if (!projectId.HasValue)
        {
            yield return "Please select a project before using Erumi with project data.";
            yield break;
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            yield return "Bạn không có quyền truy cập vào dữ liệu của dự án này.";
            yield break;
        }

        // Smart RAG: Simplified for streaming (single query)
        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { userMessage });
        var vector = queryEmbedding[0].Vector.ToArray();

        var filter = new VectorFilter
        {
            ProjectId = projectId.Value,
            OwnerId = _currentUserService.UserId
        };

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 5);

        var contextBuilder = new StringBuilder();
        foreach (var res in results)
        {
            contextBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"- [Loại: {res.Payload.GetValueOrDefault("ContentType")}]: {res.Payload.GetValueOrDefault("Content")}");
            contextBuilder.AppendLine();
        }

        var exportLink = projectId != null ? $"/api/ai/export/{projectId}?format=excel" : "#";
        var systemPrompt = $@"Bạn là Erumi (Erumi-chan), một Trợ lý ảo AI thông minh, tận tâm và chuyên nghiệp của hệ thống quản lý dự án Qaly.

PHONG CÁCH LÀM VIỆC:
1. Luôn lịch sự, sử dụng ngôn ngữ Tiếng Việt chuẩn mực, chuyên nghiệp nhưng vẫn thân thiện.
2. Trả lời súc tích, đi thẳng vào vấn đề.
3. Sử dụng định dạng Markdown (gạch đầu dòng, in đậm, bảng).

KIẾN THỨC VỀ HỆ THỐNG:
- Bạn có quyền truy cập và thực thi các tác vụ: Quản lý Task, Phân công, Tính giờ làm việc, Tìm kiếm tri thức.
- Bạn có thể hỗ trợ xuất báo cáo. Link format:
  [📥 Tải báo cáo Excel dự án]({exportLink})

NGỮ CẢNH DỮ LIỆU HIỆN TẠI:
{contextBuilder}

HƯỚNG DẪN:
- Dựa TRỰC TIẾP vào ngữ cảnh để trả lời.
- Sử dụng các công cụ (tools) được cung cấp để thực hiện hành động nếu người dùng yêu cầu.
- Nếu người dùng muốn xuất file, hãy đưa ra link tải như hướng dẫn trên.

ID dự án hiện tại (nếu có): {projectId}
Thời gian: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";

        var chatHistory = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt)
        };

        if (history != null)
        {
            foreach (var msg in history)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                    ? ChatRole.Assistant : ChatRole.User;
                chatHistory.Add(new ChatMessage(role, msg.Content));
            }
        }

        chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var options = new ChatOptions
        {
            Tools = GetTools()
        };

        IAsyncEnumerable<StreamingChatCompletionUpdate>? streamingResponse = null;
        string? connectionError = null;
        try
        {
            streamingResponse = _chatClient.CompleteStreamingAsync(chatHistory, options);
        }
        catch (Exception ex)
        {
            LogAiStreamStartFailed(_logger, ex);
            connectionError = "Xin lỗi, hiện tại tôi không thể kết nối tới máy chủ AI. Bạn hãy thử lại sau nhé.";
        }

        if (connectionError != null)
        {
            yield return connectionError;
            yield break;
        }

        await using var enumerator = streamingResponse!.GetAsyncEnumerator();
        while (true)
        {
            StreamingChatCompletionUpdate? update = null;
            string? streamError = null;
            try
            {
                if (!await enumerator.MoveNextAsync()) break;
                update = enumerator.Current;
            }
            catch (Exception ex)
            {
                LogAiStreamFailed(_logger, ex);
                streamError = "\n\n*[Kết nối AI bị gián đoạn giữa chừng]*";
            }

            if (streamError != null)
            {
                yield return streamError;
                break;
            }

            if (update?.Text != null)
            {
                yield return update.Text;
            }
        }
    }

    private async Task<List<string>> RefineSearchQueriesAsync(string userMessage)
    {
        var prompt = $@"Dựa trên tin nhắn của người dùng sau, hãy tạo ra tối đa 2 câu truy vấn tìm kiếm ngắn gọn (bằng tiếng Việt) để tìm kiếm thông tin liên quan trong kho dữ liệu dự án.
Chỉ trả về danh sách các câu truy vấn, mỗi câu một dòng.

Tin nhắn: {userMessage}";

        var response = await _chatClient.CompleteAsync(prompt);
        var text = response.Message.Text ?? userMessage;
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim().TrimStart('-'))
                   .Take(2)
                   .ToList();
    }

    private List<AITool> GetTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(_aiTools.GetProjectSummary),
            AIFunctionFactory.Create(_aiTools.GetOverdueTasks),
            AIFunctionFactory.Create(_aiTools.CreateTask),
            AIFunctionFactory.Create(_aiTools.UpdateTaskStatus),
            AIFunctionFactory.Create(_aiTools.AssignTask),
            AIFunctionFactory.Create(_aiTools.SuggestTaskAssignment),
            AIFunctionFactory.Create(_aiTools.SetTaskPriority),
            AIFunctionFactory.Create(_aiTools.AddDueDate),
            AIFunctionFactory.Create(_aiTools.AddComment),
            AIFunctionFactory.Create(_aiTools.GetMemberWorkload),
            AIFunctionFactory.Create(_aiTools.SearchKnowledge),
            AIFunctionFactory.Create(_aiTools.StartTimeTracking),
            AIFunctionFactory.Create(_aiTools.StopTimeTracking),
            AIFunctionFactory.Create(_aiTools.GetMyTimeLogs)
        };
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

    public async Task<string> GenerateAnalyticsInsightsAsync(Guid projectId, string analyticsData)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return "Bạn không có quyền truy cập dữ liệu phân tích của dự án này.";
        }

        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project == null) return "Không tìm thấy dự án.";

        var prompt = $@"Bạn là chuyên gia phân tích dữ liệu dự án. Hãy xem xét dữ liệu phân tích sau đây của dự án '{project.Name}' và đưa ra các nhận xét thông minh, phát hiện xu hướng, rủi ro tiềm ẩn hoặc cơ hội cải thiện hiệu suất.

Dữ liệu phân tích:
{analyticsData}

Yêu cầu:
1. Đưa ra 3-4 nhận xét quan trọng nhất.
2. Đề xuất hành động cụ thể để cải thiện dự án.
3. Trả lời bằng Tiếng Việt, súc tích và mang tính hành động cao.";

        var response = await _chatClient.CompleteAsync(prompt);
        return response.Message.Text ?? "Không thể tạo nhận xét phân tích.";
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

    private static List<string> ExtractKeywords(string? title, string? description)
        => string.Join(' ', new[] { title, description }.Where(value => !string.IsNullOrWhiteSpace(value)))
            .Split(KeywordSplitSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Select(token => token.ToLowerInvariant())
            .Distinct()
            .Take(12)
            .ToList();

    private static bool IsAssignedTo(TaskItem task, Guid userId)
        => task.AssigneeId == userId || task.Assignees.Any(assignment => assignment.UserId == userId);

    private static bool IsTaskOverdue(TaskItem task)
        => task.DueDate.HasValue && task.DueDate.Value < DateTimeOffset.UtcNow && !IsDone(task);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Unauthorized AI summary request for project {ProjectId} by user {UserId}")]
    private static partial void LogUnauthorizedSummaryRequest(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unauthorized AI risk analysis request for project {ProjectId} by user {UserId}")]
    private static partial void LogUnauthorizedRiskAnalysisRequest(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error while starting AI stream.")]
    private static partial void LogAiStreamStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error while streaming AI response.")]
    private static partial void LogAiStreamFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Error while executing AI categorization batch.")]
    private static partial void LogAiCategorizationBatchFailed(ILogger logger, Exception exception);

    public async Task<List<Qaly.Application.DTOs.Import.AiCategorizationResult>> CategorizeTasksBatchAsync(List<Qaly.Application.DTOs.Import.AiCategorizationRequest> tasks)
    {
        if (tasks.Count == 0) return new List<Qaly.Application.DTOs.Import.AiCategorizationResult>();

        var taskJson = JsonSerializer.Serialize(tasks, CategorizationPromptJsonOptions);
        var prompt = $@"Bạn là trợ lý AI chuyên môn về Agile/Kanban. Nhiệm vụ của bạn là đọc các task sau và phân loại chúng vào các cột (Status) phù hợp, độ ưu tiên (Priority) hợp lý, và tối đa 2 nhãn (Labels) cho mỗi task.

Dữ liệu đầu vào:
{taskJson}

Bạn PHẢI trả về KẾT QUẢ ĐẦU RA dưới dạng một JSON Array HỢP LỆ, định dạng CHÍNH XÁC như mẫu sau (KHÔNG ĐƯỢC chứa thêm bất kỳ text nào khác ngoài mảng JSON):
[
  {{ ""RowIndex"": 1, ""Status"": ""Todo"", ""Priority"": ""High"", ""Labels"": [""Bug"", ""Frontend""] }}
]
Chỉ được chọn Status từ: Todo, InProgress, InReview, OnHold, Done.
Chỉ được chọn Priority từ: Low, Medium, High, Critical.
Chỉ xuất ra đúng mảng JSON, tuyệt đối không giải thích.";

        try
        {
            var response = await _chatClient.CompleteAsync(prompt);
            var text = response.Message.Text ?? "[]";
            
            // Extract json array if the AI enclosed it in markdown block ```json ... ```
            var startIdx = text.IndexOf('[');
            var endIdx = text.LastIndexOf(']');
            if (startIdx >= 0 && endIdx >= startIdx)
            {
                var jsonStr = text.Substring(startIdx, endIdx - startIdx + 1);
                var results = JsonSerializer.Deserialize<List<Qaly.Application.DTOs.Import.AiCategorizationResult>>(
                    jsonStr, 
                    CategorizationResponseJsonOptions
                );
                return results ?? new List<Qaly.Application.DTOs.Import.AiCategorizationResult>();
            }
        }
        catch (Exception ex)
        {
            LogAiCategorizationBatchFailed(_logger, ex);
        }

        return new List<Qaly.Application.DTOs.Import.AiCategorizationResult>();
    }
}


