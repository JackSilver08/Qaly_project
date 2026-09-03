using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class TaskPrioritySuggestionService : ITaskPrioritySuggestionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserService _currentUserService;

    public TaskPrioritySuggestionService(IServiceProvider serviceProvider, ICurrentUserService currentUserService)
    {
        _serviceProvider = serviceProvider;
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

            var aiGateway = _serviceProvider.GetRequiredService<IAiGateway>();
            var response = await aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "TaskPrioritySuggestion",
                Prompt = prompt,
                UserId = _currentUserService.UserId,
                UseCache = true
            });
            return response.IsSuccess && !string.IsNullOrWhiteSpace(response.Content)
                ? response.Content
                : "Medium - Model chưa phản hồi; dùng mức trung bình để bạn tiếp tục chỉnh.";
        }
        catch
        {
            return "Medium - Model chưa phản hồi; dùng mức trung bình để bạn tiếp tục chỉnh.";
        }
    }
}
