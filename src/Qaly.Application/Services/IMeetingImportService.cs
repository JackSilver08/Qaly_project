using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services;

public interface IMeetingImportService
{
    Task<Result<MeetilyImportResult>> ImportMeetilyAsync(MeetilyImportRequest request, CancellationToken ct = default);
    Task<Result<MeetingActionItemsResponseDto>> GetMeetingActionItemsAsync(Guid meetingImportId, CancellationToken ct = default);
    Task<Result<TaskItemDto>> CreateTaskFromMeetingActionItemAsync(Guid meetingImportId, int actionItemIndex, MeetingActionItemCreateRequest request, CancellationToken ct = default);
    Task<Result<MeetingActionItemTaskLinkDto>> LinkMeetingActionItemToTaskAsync(Guid meetingImportId, int actionItemIndex, LinkMeetingActionItemTaskRequest request, CancellationToken ct = default);
    Task<Result<MeetingActionItemTaskLinkDto>> GetMeetingActionItemTaskLinkAsync(Guid meetingImportId, int actionItemIndex, CancellationToken ct = default);
    Task<Result<TaskMeetingSourceDto>> GetTaskMeetingSourceAsync(Guid taskId, CancellationToken ct = default);
    Task<Result<AutoChecknoteResponseDto>> CreateAutoChecknoteAsync(Guid meetingSessionId, AutoChecknoteRequest request, CancellationToken ct = default);
}
