using Microsoft.Extensions.AI;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Application.Services;

public sealed class TaskPrioritySuggestionService : ITaskPrioritySuggestionService
{
    private readonly IChatClient _chatClient;

    public TaskPrioritySuggestionService(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<string> SuggestAsync(string taskTitle, string taskDescription, string projectContext)
    {
        try
        {
            var prompt = $@"Dựa trên thông tin công việc sau, hãy đề xuất độ ưu tiên (Low, Medium, High, Critical) và giải thích lý do ngắn gọn.
Dự án: {projectContext}
Công việc: {taskTitle}
Mô tả: {taskDescription}

Trả lời theo định dạng: [Priority] - [Lý do]";

            var response = await _chatClient.CompleteAsync(prompt);
            return response.Message.Text ?? "Medium - Không thể xác định";
        }
        catch
        {
            return "Medium - (AI suggestion unavailable)";
        }
    }
}
