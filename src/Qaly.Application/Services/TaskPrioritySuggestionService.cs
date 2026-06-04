using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class TaskPrioritySuggestionService : ITaskPrioritySuggestionService
{
    private readonly IAiGateway _aiGateway;
    private readonly ICurrentUserService _currentUserService;

    public TaskPrioritySuggestionService(IAiGateway aiGateway, ICurrentUserService currentUserService)
    {
        _aiGateway = aiGateway;
        _currentUserService = currentUserService;
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

            var response = await _aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "TaskPrioritySuggestion",
                Prompt = prompt,
                UserId = _currentUserService.UserId,
                UseCache = true
            });
            return response.Content ?? "Medium - Không thể xác định";
        }
        catch
        {
            return "Medium - (AI suggestion unavailable)";
        }
    }
}
