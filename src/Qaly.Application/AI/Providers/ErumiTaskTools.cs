using System.ComponentModel;
using Microsoft.Extensions.AI;
using Qaly.Application.Services;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.AI.Providers;

public class ErumiTaskTools
{
    private readonly ITaskService _taskService;

    public ErumiTaskTools(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [Description("Tạo một nhiệm vụ mới trong một dự án cụ thể.")]
    public async Task<string> CreateTask(
        [Description("Tiêu đề của nhiệm vụ")] string title,
        [Description("ID của dự án")] Guid projectId,
        [Description("Mô tả chi tiết nhiệm vụ")] string? description = null,
        [Description("ID của người được giao")] Guid? assigneeId = null,
        [Description("Độ ưu tiên: Low, Medium, High, Critical")] string priority = "Medium",
        [Description("Ngày hết hạn (ISO 8601)")] DateTimeOffset? dueDate = null)
    {
        var dto = new CreateTaskDto(title, description, priority, dueDate, null, projectId, assigneeId, false);

        var result = await _taskService.CreateAsync(dto);
        if (result.IsSuccess)
        {
            return $"Đã tạo thành công nhiệm vụ: {result.Data?.Title} (ID: {result.Data?.Id})";
        }

        return $"Lỗi khi tạo nhiệm vụ: {result.Error}";
    }

    [Description("Cập nhật trạng thái của một nhiệm vụ.")]
    public async Task<string> UpdateTaskStatus(
        [Description("ID của nhiệm vụ")] Guid taskId,
        [Description("Trạng thái mới: Todo, InProgress, InReview, Done, Cancelled")] string status)
    {
        var result = await _taskService.UpdateStatusAsync(taskId, status);
        if (result.IsSuccess)
        {
            return $"Đã cập nhật trạng thái nhiệm vụ sang {status}.";
        }

        return $"Lỗi khi cập nhật trạng thái: {result.Error}";
    }
}
