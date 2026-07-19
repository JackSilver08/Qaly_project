using Qaly.Domain.Entities;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Common.Interfaces;

/// <summary>
/// AI Service interface - điểm tích hợp AI vào hệ thống.
/// Sẽ implement với OpenAI, Gemini, hoặc local model.
/// </summary>
public interface IAiService
{
    /// <summary>Phân tích và đề xuất ưu tiên task dựa trên context dự án</summary>
    Task<string> SuggestTaskPriorityAsync(string taskTitle, string taskDescription, string projectContext);

    /// <summary>Tự động tạo summary cho project dựa trên các tasks</summary>
    Task<string> GenerateProjectSummaryAsync(Guid projectId);

    /// <summary>Phân tích rủi ro dựa trên task overdue, workload distribution</summary>
    Task<string> AnalyzeProjectRisksAsync(Guid projectId);

    /// <summary>Đề xuất phân công task dựa trên skill và workload của members</summary>
    Task<string> SuggestTaskAssignmentAsync(Guid taskId, Guid projectId);

    /// <summary>Trả về dữ liệu chuẩn hóa để AI/UX gợi ý assignee.</summary>
    Task<Result<TaskAssignmentInsightDto>> GetTaskAssignmentInsightAsync(Guid taskId, Guid projectId, CancellationToken ct = default);

    /// <summary>Smart search - tìm kiếm ngữ nghĩa trong tasks/comments</summary>
    Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null);

    /// <summary>Tự động tạo subtasks từ mô tả task lớn</summary>
    Task<IReadOnlyList<string>> GenerateSubtasksAsync(string taskTitle, string taskDescription);

    /// <summary>Chat assistant - hỏi đáp về dự án (Streaming version)</summary>
    Task<string> ChatAsync(string userMessage, Guid? projectId = null, string mode = "erumi", IList<AiChatMessageDto>? history = null);

    /// <summary>Chat assistant - hỏi đáp về dự án với phản hồi trực tiếp (Streaming)</summary>
    IAsyncEnumerable<string> ChatStreamingAsync(string userMessage, Guid? projectId = null, string mode = "erumi", IList<AiChatMessageDto>? history = null);

    /// <summary>Lấy thông tin project kèm tasks để phục vụ export</summary>
    Task<Project?> GetProjectWithTasksAsync(Guid projectId);

    /// <summary>Tạo nhận xét AI dựa trên dữ liệu phân tích (Analytics)</summary>
    Task<string> GenerateAnalyticsInsightsAsync(Guid projectId, string analyticsData);

    /// <summary>Tự động phân loại hàng loạt task (Status, Priority, Labels) dựa trên Title và Description</summary>
    Task<List<Qaly.Application.DTOs.Import.AiCategorizationResult>> CategorizeTasksBatchAsync(List<Qaly.Application.DTOs.Import.AiCategorizationRequest> tasks);

    /// <summary>Lập kế hoạch dự án và công việc bằng AI từ ngôn ngữ tự nhiên</summary>
    Task<Result<GeneratedPlanDto>> GeneratePlanAsync(string userPrompt, Guid? projectId, CancellationToken ct = default);
}

