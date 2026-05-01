using Qaly.Application.Common.Interfaces;

namespace Qaly.Application.Services;

public class AiService : IAiService
{
    public Task<string> SuggestTaskPriorityAsync(string taskTitle, string taskDescription, string projectContext)
    {
        return Task.FromResult("Trung bình");
    }

    public Task<string> GenerateProjectSummaryAsync(Guid projectId)
    {
        return Task.FromResult("Tóm tắt AI: Dự án đang ở giai đoạn nền tảng ban đầu.");
    }

    public Task<string> AnalyzeProjectRisksAsync(Guid projectId)
    {
        return Task.FromResult("Phân tích AI: Dự án đang ổn định. Chưa phát hiện rủi ro khẩn cấp.");
    }

    public Task<string> SuggestTaskAssignmentAsync(Guid taskId, Guid projectId)
    {
        return Task.FromResult("Đề xuất giao cho: Quản trị viên (mặc định)");
    }

    public Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Kết quả phù hợp 1", "Kết quả phù hợp 2" });
    }

    public Task<IReadOnlyList<string>> GenerateSubtasksAsync(string taskTitle, string taskDescription)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Thiết lập môi trường", "Xác định yêu cầu", "Triển khai logic lõi" });
    }

    public Task<string> ChatAsync(string userMessage, Guid? projectId = null)
    {
        return Task.FromResult("AI: Xin chào! Tôi có thể hỗ trợ gì cho dự án của bạn?");
    }
}
