using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qaly.Application.Services;

public class AiWorkflowService : IAiWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _projectMemberRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<AiJob> _aiJobRepo;
    private readonly IRepository<AiGeneratedDraft> _aiDraftRepo;
    private readonly IRepository<MeetingImport> _meetingImportRepo;
    private readonly IRepository<MeetingActionItemMapping> _meetingActionItemMappingRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskAssignment> _taskAssignmentRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ITaskService? _taskService;
    private readonly ICommentService? _commentService;
    private readonly ITimeTrackingService? _timeTrackingService;
    private readonly IAiComplianceService? _complianceService;
    private readonly IAiAgentOrchestrator? _agentOrchestrator;

    public AiWorkflowService(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<AiJob> aiJobRepo,
        IRepository<AiGeneratedDraft> aiDraftRepo,
        IRepository<MeetingImport> meetingImportRepo,
        IRepository<MeetingActionItemMapping> meetingActionItemMappingRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskAssignment> taskAssignmentRepo,
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ITaskService? taskService = null,
        ICommentService? commentService = null,
        ITimeTrackingService? timeTrackingService = null,
        IAiComplianceService? complianceService = null,
        IAiAgentOrchestrator? agentOrchestrator = null)
    {
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _aiJobRepo = aiJobRepo;
        _aiDraftRepo = aiDraftRepo;
        _meetingImportRepo = meetingImportRepo;
        _meetingActionItemMappingRepo = meetingActionItemMappingRepo;
        _taskRepo = taskRepo;
        _taskAssignmentRepo = taskAssignmentRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _taskService = taskService;
        _commentService = commentService;
        _timeTrackingService = timeTrackingService;
        _complianceService = complianceService;
        _agentOrchestrator = agentOrchestrator;
    }

    public async Task<Result<AiJobCreatedDto>> CreateJobAsync(CreateAiJobDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<AiJobCreatedDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.JobType))
        {
            return Result.Failure<AiJobCreatedDto>("job_type is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.SourceType))
        {
            return Result.Failure<AiJobCreatedDto>("source_type is required.", 400);
        }

        var project = await _projectRepo.GetQueryable()
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == dto.ProjectId, ct);
        if (project == null)
        {
            return Result.Failure<AiJobCreatedDto>("Project was not found.", 404);
        }

        if (!await CanAccessProjectAsync(project, currentUserId.Value, ct))
        {
            return Result.Forbidden<AiJobCreatedDto>();
        }

        var estimatedCost = EstimateCost(dto.SourceText);
        var cacheKey = GenerateCacheKey(dto.JobType, dto.ProjectId, dto.SourceType, dto.SourceId, dto.SourceText);

        var job = new AiJob
        {
            JobType = dto.JobType.Trim(),
            ProjectId = dto.ProjectId,
            SourceType = dto.SourceType.Trim(),
            SourceId = string.IsNullOrWhiteSpace(dto.SourceId) ? null : dto.SourceId.Trim(),
            ProviderHint = string.IsNullOrWhiteSpace(dto.ProviderHint) ? "auto" : dto.ProviderHint.Trim(),
            Sensitive = dto.Sensitive,
            Status = "DraftReady",
            EstimatedCostUsd = estimatedCost,
            CacheKey = cacheKey,
            RequestedById = currentUserId.Value
        };

        await _aiJobRepo.AddAsync(job, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var draftPayload = await BuildTaskDraftPayloadAsync(dto.SourceText, project, ct);
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = project.Id,
            DraftType = "TaskDraft",
            PayloadJson = JsonSerializer.Serialize(draftPayload),
            Status = "Pending"
        };

        await _aiDraftRepo.AddAsync(draft, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
            "CreateAiJob",
            nameof(AiJob),
            job.Id.ToString(),
            new { job.JobType, job.ProjectId, job.SourceType, job.SourceId, job.CacheKey, draft.Id },
            ct);

        return Result.Created(new AiJobCreatedDto(job.Id, job.Status, job.EstimatedCostUsd, job.CacheKey, draft.Id));
    }

    public async Task<Result<AiDraftConfirmResultDto>> ConfirmDraftAsync(Guid draftId, ConfirmAiDraftDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<AiDraftConfirmResultDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.ConfirmAction))
        {
            return Result.Failure<AiDraftConfirmResultDto>("confirm_action is required.", 400);
        }

        var draft = await _aiDraftRepo.GetQueryable()
            .Include(item => item.Project)
                .ThenInclude(project => project.Organization)
            .Include(item => item.AiJob)
            .FirstOrDefaultAsync(item => item.Id == draftId, ct);
        if (draft == null)
        {
            return Result.Failure<AiDraftConfirmResultDto>("Draft was not found.", 404);
        }

        if (!await CanAccessProjectAsync(draft.Project, currentUserId.Value, ct))
        {
            return Result.Forbidden<AiDraftConfirmResultDto>();
        }

        if (string.Equals(draft.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AiDraftConfirmResultDto>("Draft was already confirmed.", 409);
        }

        var payloadJson = string.IsNullOrWhiteSpace(dto.EditedPayloadJson) ? draft.PayloadJson : dto.EditedPayloadJson.Trim();
        AiTaskDraftPayload? payload = null;
        if (string.Equals(draft.DraftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(draft.DraftType, "TaskDraft", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                payload = DeserializeTaskDraftPayload(payloadJson, draft.DraftType);
            }
            catch (JsonException)
            {
                return Result.Failure<AiDraftConfirmResultDto>("edited_payload is invalid JSON.", 400);
            }
        }

        var createdTaskIds = new List<Guid>();
        var normalizedAction = dto.ConfirmAction.Trim();

        if (string.Equals(normalizedAction, "reject", StringComparison.OrdinalIgnoreCase))
        {
            draft.Status = "Rejected";
            draft.ConfirmedById = currentUserId.Value;
            draft.ConfirmedAt = DateTimeOffset.UtcNow;
            draft.ConfirmAction = normalizedAction;
            draft.ConfirmationNote = string.IsNullOrWhiteSpace(dto.ConfirmationNote) ? null : dto.ConfirmationNote.Trim();
            draft.AiJob.Status = "Rejected";

            await _aiDraftRepo.UpdateAsync(draft, ct);
            await _aiJobRepo.UpdateAsync(draft.AiJob, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (_complianceService != null)
            {
                await _complianceService.LogAuditEventAsync(
                    draft.Project.OrganizationId,
                    draft.ProjectId,
                    currentUserId.Value,
                    "AI_TOOL_REJECTED",
                    "AiGeneratedDraft",
                    null,
                    draft.PayloadJson,
                    null,
                    ct
                );
            }

            await _auditLogService.LogAsync(
                "RejectAiDraft",
                nameof(AiGeneratedDraft),
                draft.Id.ToString(),
                new { draft.ProjectId, draft.ConfirmAction },
                ct);

            return Result.Success(new AiDraftConfirmResultDto(
                draft.Id,
                draft.Status,
                normalizedAction,
                0,
                Array.Empty<Guid>()));
        }

        if (string.Equals(normalizedAction, "execute_action", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(draft.DraftType, "CreateTask", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                
                string title = root.GetProperty("title").GetString() ?? string.Empty;
                string? description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                string priority = root.TryGetProperty("priority", out var prioProp) ? prioProp.GetString() ?? "Medium" : "Medium";
                Guid? assigneeId = null;
                if (root.TryGetProperty("assigneeId", out var assProp) && assProp.ValueKind == JsonValueKind.String && Guid.TryParse(assProp.GetString(), out var parsedAssignee))
                {
                    assigneeId = parsedAssignee;
                }
                DateTimeOffset? dueDate = null;
                if (root.TryGetProperty("dueDate", out var dueProp) && dueProp.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(dueProp.GetString(), out var parsedDue))
                {
                    dueDate = parsedDue;
                }

                if (_taskService != null)
                {
                    var taskDto = new Qaly.Application.DTOs.Task.CreateTaskDto(title, description, priority, dueDate, null, draft.ProjectId, assigneeId);
                    var taskResult = await _taskService.CreateAsync(taskDto, ct);
                    if (!taskResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute CreateTask: {taskResult.Error}", taskResult.StatusCode);
                    }
                    createdTaskIds.Add(taskResult.Data!.Id);
                }
                else
                {
                    var task = new TaskItem
                    {
                        Title = title,
                        Description = description,
                        Priority = priority,
                        Status = "Todo",
                        DueDate = dueDate,
                        ProjectId = draft.ProjectId,
                        ReporterId = currentUserId.Value,
                        AssigneeId = assigneeId
                    };
                    await _taskRepo.AddAsync(task, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    createdTaskIds.Add(task.Id);
                }
            }
            else if (string.Equals(draft.DraftType, "UpdateTaskStatus", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                string status = root.GetProperty("status").GetString()!;

                if (_taskService != null)
                {
                    var taskResult = await _taskService.UpdateStatusAsync(taskId, status, ct: ct);
                    if (!taskResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute UpdateTaskStatus: {taskResult.Error}", taskResult.StatusCode);
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.Status = status;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "AssignTask", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                Guid assigneeId = Guid.Parse(root.GetProperty("assigneeId").GetString()!);

                if (_taskService != null)
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        var taskDto = new Qaly.Application.DTOs.Task.UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, assigneeId, task.IsPrivate);
                        var taskResult = await _taskService.UpdateAsync(taskId, taskDto, ct);
                        if (!taskResult.IsSuccess)
                        {
                            return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute AssignTask: {taskResult.Error}", taskResult.StatusCode);
                        }
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.AssigneeId = assigneeId;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "SetTaskPriority", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                string priority = root.GetProperty("priority").GetString()!;

                if (_taskService != null)
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        var taskDto = new Qaly.Application.DTOs.Task.UpdateTaskDto(task.Title, task.Description, task.Status, priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);
                        var taskResult = await _taskService.UpdateAsync(taskId, taskDto, ct);
                        if (!taskResult.IsSuccess)
                        {
                            return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute SetTaskPriority: {taskResult.Error}", taskResult.StatusCode);
                        }
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.Priority = priority;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "AddDueDate", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                DateTimeOffset dueDate = DateTimeOffset.Parse(root.GetProperty("dueDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

                if (_taskService != null)
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        var taskDto = new Qaly.Application.DTOs.Task.UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, dueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);
                        var taskResult = await _taskService.UpdateAsync(taskId, taskDto, ct);
                        if (!taskResult.IsSuccess)
                        {
                            return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute AddDueDate: {taskResult.Error}", taskResult.StatusCode);
                        }
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.DueDate = dueDate;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "AddComment", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                string content = root.GetProperty("content").GetString()!;

                if (_commentService != null)
                {
                    var commentDto = new Qaly.Application.DTOs.Comment.CreateCommentDto(content, taskId);
                    var commentResult = await _commentService.CreateAsync(commentDto, ct);
                    if (!commentResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute AddComment: {commentResult.Error}", commentResult.StatusCode);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "StartTimeTracking", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);

                if (_timeTrackingService != null)
                {
                    var ttResult = await _timeTrackingService.StartTimerAsync(taskId, ct);
                    if (!ttResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute StartTimeTracking: {ttResult.Error}", ttResult.StatusCode);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "StopTimeTracking", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid entryId = Guid.Parse(root.GetProperty("entryId").GetString()!);

                if (_timeTrackingService != null)
                {
                    var ttResult = await _timeTrackingService.StopTimerAsync(entryId, ct);
                    if (!ttResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute StopTimeTracking: {ttResult.Error}", ttResult.StatusCode);
                    }
                }
            }
        }
        else if (string.Equals(normalizedAction, "create_tasks", StringComparison.OrdinalIgnoreCase))
        {
            if (!await CanManageProjectAsync(draft.Project, currentUserId.Value, ct))
            {
                return Result.Failure<AiDraftConfirmResultDto>("Access denied for create_tasks confirm action.", 403);
            }

            if (payload == null)
            {
                return Result.Failure<AiDraftConfirmResultDto>("Invalid draft payload.", 400);
            }

            MeetingImport? meetingImport = null;
            Dictionary<int, MeetingActionItemMapping>? existingMappingsByIndex = null;
            MeetingExtractionPayload? meetingExtraction = null;

            if (string.Equals(draft.DraftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase))
            {
                meetingImport = await _meetingImportRepo.GetQueryable()
                    .FirstOrDefaultAsync(item => item.AiDraftId == draft.Id, ct);
                if (meetingImport != null)
                {
                    existingMappingsByIndex = await _meetingActionItemMappingRepo.GetQueryable()
                        .Where(mapping => mapping.MeetingImportId == meetingImport.Id)
                        .ToDictionaryAsync(mapping => mapping.ActionItemIndex, ct);
                    meetingExtraction = TryDeserializeMeetingExtractionPayload(payloadJson);
                }
            }

            for (var itemIndex = 0; itemIndex < payload.Tasks.Count; itemIndex++)
            {
                var item = payload.Tasks[itemIndex];
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    continue;
                }

                if (existingMappingsByIndex != null &&
                    existingMappingsByIndex.TryGetValue(itemIndex, out var existingMapping) &&
                    existingMapping.TaskId.HasValue)
                {
                    continue;
                }

                var normalizedPriority = TaskStatusRules.IsValidPriority(item.Priority)
                    ? TaskStatusRules.NormalizePriority(item.Priority)
                    : "Medium";
                var normalizedStatus = TaskStatusRules.IsValidStatus(item.Status)
                    ? TaskStatusRules.NormalizeStatus(item.Status)
                    : "Todo";

                Guid? validAssigneeId = null;
                if (item.AssigneeId.HasValue && item.AssigneeId.Value != Guid.Empty)
                {
                    var isValidAssignee = await _userRepo.GetQueryable()
                        .AnyAsync(user => user.Id == item.AssigneeId.Value && user.IsActive, ct) &&
                        await IsProjectUserAsync(draft.Project, item.AssigneeId.Value, ct);
                    if (isValidAssignee)
                    {
                        validAssigneeId = item.AssigneeId.Value;
                    }
                }

                var task = new TaskItem
                {
                    Title = item.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                    Priority = normalizedPriority,
                    Status = normalizedStatus,
                    DueDate = item.DueDate,
                    ProjectId = draft.ProjectId,
                    ReporterId = currentUserId.Value,
                    AssigneeId = validAssigneeId
                };

                await _taskRepo.AddAsync(task, ct);
                createdTaskIds.Add(task.Id);

                if (existingMappingsByIndex != null && meetingImport != null)
                {
                    var sourceActionItem = (meetingExtraction != null && itemIndex >= 0 && itemIndex < meetingExtraction.ActionItems.Count)
                        ? meetingExtraction.ActionItems[itemIndex]
                        : null;

                    if (existingMappingsByIndex.TryGetValue(itemIndex, out var existingMappingWithoutTask))
                    {
                        existingMappingWithoutTask.TaskId = task.Id;
                        existingMappingWithoutTask.Status = "Linked";
                        existingMappingWithoutTask.SourceTitle = sourceActionItem?.Title ?? item.Title.Trim();
                        existingMappingWithoutTask.SourcePriority = sourceActionItem?.Priority ?? normalizedPriority;
                        existingMappingWithoutTask.SourceDueDate = sourceActionItem?.DueDate ?? item.DueDate;
                        existingMappingWithoutTask.SourceQuote = sourceActionItem?.SourceEvidence ?? sourceActionItem?.Description ?? item.Description;
                        existingMappingWithoutTask.CreatedById = currentUserId.Value;
                        await _meetingActionItemMappingRepo.UpdateAsync(existingMappingWithoutTask, ct);
                    }
                    else
                    {
                        var mapping = new MeetingActionItemMapping
                        {
                            MeetingImportId = meetingImport.Id,
                            ActionItemIndex = itemIndex,
                            TaskId = task.Id,
                            Status = "Linked",
                            SourceTitle = sourceActionItem?.Title ?? item.Title.Trim(),
                            SourcePriority = sourceActionItem?.Priority ?? normalizedPriority,
                            SourceDueDate = sourceActionItem?.DueDate ?? item.DueDate,
                            SourceQuote = sourceActionItem?.SourceEvidence ?? sourceActionItem?.Description ?? item.Description,
                            CreatedById = currentUserId.Value
                        };

                        await _meetingActionItemMappingRepo.AddAsync(mapping, ct);
                        existingMappingsByIndex[itemIndex] = mapping;
                    }
                }

                if (validAssigneeId.HasValue)
                {
                    await _taskAssignmentRepo.AddAsync(new TaskAssignment
                    {
                        TaskItemId = task.Id,
                        UserId = validAssigneeId.Value,
                        AssignedAt = DateTimeOffset.UtcNow,
                        AssignedByUserId = currentUserId
                    }, ct);
                }
            }
        }

        draft.PayloadJson = payloadJson;
        draft.Status = "Confirmed";
        draft.ConfirmedById = currentUserId.Value;
        draft.ConfirmedAt = DateTimeOffset.UtcNow;
        draft.ConfirmAction = normalizedAction;
        draft.ConfirmationNote = string.IsNullOrWhiteSpace(dto.ConfirmationNote) ? null : dto.ConfirmationNote.Trim();
        draft.AiJob.Status = "Confirmed";

        await _aiDraftRepo.UpdateAsync(draft, ct);
        await _aiJobRepo.UpdateAsync(draft.AiJob, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (_complianceService != null)
        {
            await _complianceService.LogAuditEventAsync(
                draft.Project.OrganizationId,
                draft.ProjectId,
                currentUserId.Value,
                "AI_DRAFT_CONFIRMED",
                "AiGeneratedDraft",
                null,
                null,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    draft.Id,
                    draft.DraftType,
                    draft.PayloadJson,
                    draft.Status
                }),
                ct
            );

            await _complianceService.LogAuditEventAsync(
                draft.Project.OrganizationId,
                draft.ProjectId,
                currentUserId.Value,
                "AI_TOOL_EXECUTED",
                draft.DraftType,
                null,
                draft.PayloadJson,
                null,
                ct
            );
        }

        await _auditLogService.LogAsync(
            "ConfirmAiDraft",
            nameof(AiGeneratedDraft),
            draft.Id.ToString(),
            new
            {
                draft.ProjectId,
                draft.ConfirmAction,
                createdTaskIds.Count
            },
            ct);

        return Result.Success(new AiDraftConfirmResultDto(
            draft.Id,
            draft.Status,
            normalizedAction,
            createdTaskIds.Count,
            createdTaskIds));
    }

    private static decimal EstimateCost(string? sourceText)
    {
        var characters = Math.Max(200, sourceText?.Length ?? 200);
        return Math.Round((characters / 4000m) * 0.002m, 6, MidpointRounding.AwayFromZero);
    }

    private static string GenerateCacheKey(string jobType, Guid projectId, string sourceType, string? sourceId, string? sourceText)
    {
        var raw = $"{jobType}|{projectId}|{sourceType}|{sourceId}|{sourceText}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<AiTaskDraftPayload> BuildTaskDraftPayloadAsync(
        string? sourceText,
        Project project,
        CancellationToken ct)
    {
        if (_agentOrchestrator?.IsEnabled == true && !string.IsNullOrWhiteSpace(sourceText))
        {
            try
            {
                var existingTasks = await _taskRepo.GetQueryable()
                    .Where(task => task.ProjectId == project.Id)
                    .OrderByDescending(task => task.CreatedAt)
                    .Take(30)
                    .Select(task => new { task.Title, task.Status, task.Priority, task.DueDate })
                    .ToListAsync(ct);

                var contextJson = JsonSerializer.Serialize(new
                {
                    project.Name,
                    project.Description,
                    ExistingTasks = existingTasks
                });

                var response = await _agentOrchestrator.ExecuteAsync(new AiRequest
                {
                    JobType = "erumi_task_planner",
                    SystemPrompt = """
You are Erumi's task planning engine for Qaly. Convert the user's goal into a small, executable project plan.
Return JSON only with this exact shape:
{"tasks":[{"title":"...","description":"...","priority":"Low|Medium|High|Critical","status":"Todo","dueDate":null,"assigneeId":null}]}
Rules: create 1-8 non-duplicate tasks; use concise action titles; include acceptance criteria in descriptions; do not claim execution; do not invent member IDs; preserve the user's language.
""",
                    Prompt = $"Project context: {contextJson}\nUser goal: {sourceText}",
                    ExpectedSchemaId = "TaskDraft.v1",
                    ProjectId = project.Id,
                    TenantId = project.OrganizationId,
                    UserId = _currentUserService.UserId,
                    IsSensitive = false,
                    UseCache = false,
                    Tools = Array.Empty<Microsoft.Extensions.AI.AITool>()
                }, ct);

                var parsed = TryParseAgentTaskDraft(response.Content);
                if (parsed is { Tasks.Count: > 0 })
                {
                    return new AiTaskDraftPayload(parsed.Tasks.Take(8).ToList());
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Preserve availability: deterministic draft generation remains the fallback.
            }
        }

        return BuildTaskDraftPayload(sourceText);
    }

    private static AiTaskDraftPayload? TryParseAgentTaskDraft(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var json = content.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                json = json[(firstNewLine + 1)..lastFence].Trim();
            }
        }

        try
        {
            var payload = JsonSerializer.Deserialize<AiTaskDraftPayload>(json, JsonOptions);
            if (payload == null) return null;

            var validTasks = payload.Tasks
                .Where(task => !string.IsNullOrWhiteSpace(task.Title))
                .Select(task => task with
                {
                    Title = task.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(task.Description) ? null : task.Description.Trim(),
                    Priority = NormalizePriority(task.Priority),
                    Status = "Todo",
                    AssigneeId = null
                })
                .DistinctBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new AiTaskDraftPayload(validTasks);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizePriority(string? priority)
        => priority?.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            "critical" => "Critical",
            _ => "Medium"
        };

    private static AiTaskDraftPayload BuildTaskDraftPayload(string? sourceText)
    {
        var lines = (sourceText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(5)
            .ToList();

        if (lines.Count == 0)
        {
            return new AiTaskDraftPayload([
                new AiTaskDraftItem(
                    "Follow up AI action items",
                    "Review meeting context and confirm final task details before creating tasks.")
            ]);
        }

        var tasks = lines
            .Select(line => new AiTaskDraftItem(line, "Generated from AI source text. Please review before confirm."))
            .ToList();
        return new AiTaskDraftPayload(tasks);
    }

    private static AiTaskDraftPayload DeserializeTaskDraftPayload(string payloadJson, string draftType)
    {
        if (string.Equals(draftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase))
        {
            var meetingPayload = JsonSerializer.Deserialize<MeetingExtractionPayload>(payloadJson, JsonOptions)
                ?? new MeetingExtractionPayload("meetily-import.v1", string.Empty, new MeetingSummaryDto(string.Empty, null, null, []), [], [], []);

            return new AiTaskDraftPayload(meetingPayload.ActionItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .Select(item => new AiTaskDraftItem(
                    item.Title,
                    item.Description ?? item.SourceEvidence,
                    item.Priority,
                    "Todo",
                    item.DueDate,
                    null))
                .ToList());
        }

        return JsonSerializer.Deserialize<AiTaskDraftPayload>(payloadJson, JsonOptions) ?? new AiTaskDraftPayload([]);
    }

    private static MeetingExtractionPayload? TryDeserializeMeetingExtractionPayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<MeetingExtractionPayload>(payloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> CanAccessProjectAsync(Project project, Guid currentUserId, CancellationToken ct)
    {
        if (IsAdmin() || project.OwnerId == currentUserId)
        {
            return true;
        }

        var isProjectMember = await _projectMemberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == project.Id && member.UserId == currentUserId, ct);
        if (isProjectMember)
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        if (project.Organization?.OwnerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageProjectAsync(Project project, Guid currentUserId, CancellationToken ct)
    {
        if (IsAdmin() || project.OwnerId == currentUserId)
        {
            return true;
        }

        var projectRole = await _projectMemberRepo.GetQueryable()
            .Where(member => member.ProjectId == project.Id && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        if (ProjectRoleRules.CanManageProject(projectRole))
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        if (project.Organization?.OwnerId == currentUserId)
        {
            return true;
        }

        var organizationRole = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return ProjectRoleRules.CanManageProject(organizationRole);
    }

    private async Task<bool> IsProjectUserAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId)
        {
            return true;
        }

        if (await _projectMemberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct))
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        if (project.Organization?.OwnerId == userId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId, ct);
    }

    private bool IsAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);
}
