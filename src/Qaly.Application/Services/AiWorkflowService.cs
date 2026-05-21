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
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskAssignment> _taskAssignmentRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public AiWorkflowService(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<AiJob> aiJobRepo,
        IRepository<AiGeneratedDraft> aiDraftRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskAssignment> taskAssignmentRepo,
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _aiJobRepo = aiJobRepo;
        _aiDraftRepo = aiDraftRepo;
        _taskRepo = taskRepo;
        _taskAssignmentRepo = taskAssignmentRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
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

        var draftPayload = BuildTaskDraftPayload(dto.SourceText);
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
        AiTaskDraftPayload payload;
        try
        {
            payload = DeserializeTaskDraftPayload(payloadJson, draft.DraftType);
        }
        catch (JsonException)
        {
            return Result.Failure<AiDraftConfirmResultDto>("edited_payload is invalid JSON.", 400);
        }

        var createdTaskIds = new List<Guid>();
        var normalizedAction = dto.ConfirmAction.Trim();
        if (string.Equals(normalizedAction, "create_tasks", StringComparison.OrdinalIgnoreCase))
        {
            if (!await CanManageProjectAsync(draft.Project, currentUserId.Value, ct))
            {
                return Result.Failure<AiDraftConfirmResultDto>("Access denied for create_tasks confirm action.", 403);
            }

            foreach (var item in payload.Tasks)
            {
                if (string.IsNullOrWhiteSpace(item.Title))
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
