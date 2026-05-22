using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public partial class MeetingImportService : IMeetingImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _projectMemberRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<MeetingImport> _meetingImportRepo;
    private readonly IRepository<AiJob> _aiJobRepo;
    private readonly IRepository<AiGeneratedDraft> _aiDraftRepo;
    private readonly IRepository<MeetingActionItemMapping> _mappingRepo;
    private readonly ITaskService _taskService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public MeetingImportService(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<MeetingImport> meetingImportRepo,
        IRepository<AiJob> aiJobRepo,
        IRepository<AiGeneratedDraft> aiDraftRepo,
        IRepository<MeetingActionItemMapping> mappingRepo,
        ITaskService taskService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _meetingImportRepo = meetingImportRepo;
        _aiJobRepo = aiJobRepo;
        _aiDraftRepo = aiDraftRepo;
        _mappingRepo = mappingRepo;
        _taskService = taskService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<MeetilyImportResult>> ImportMeetilyAsync(MeetilyImportRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<MeetilyImportResult>();
        }

        var validation = ValidateRequest(request);
        if (!validation.IsSuccess)
        {
            return Result.Failure<MeetilyImportResult>(validation.Error!, validation.StatusCode);
        }

        var project = await _projectRepo.GetQueryable()
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId, ct);
        if (project == null)
        {
            return Result.NotFound<MeetilyImportResult>("Project was not found.");
        }

        if (!await CanAccessProjectAsync(project, currentUserId.Value, ct))
        {
            return Result.Forbidden<MeetilyImportResult>();
        }

        var sourceHash = GenerateSourceHash(request);
        var existing = await _meetingImportRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.ProjectId == request.ProjectId &&
                item.SourceProvider == "meetily" &&
                item.SourceHash == sourceHash,
                ct);

        if (existing != null)
        {
            var duplicateExtraction = BuildExtraction(request, sourceHash);
            return Result.Success(new MeetilyImportResult(
                existing.Id,
                existing.ProjectId,
                existing.SourceProvider,
                existing.SourceHash,
                Duplicate: true,
                existing.AiJobId,
                existing.AiDraftId,
                duplicateExtraction));
        }

        var extraction = BuildExtraction(request, sourceHash);
        var sourceText = BuildSourceText(request, extraction);
        var aiJob = new AiJob
        {
            JobType = "AI-06_MEETING_EXTRACT",
            ProjectId = project.Id,
            SourceType = "meetily_import",
            SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? sourceHash : request.SourceId.Trim(),
            ProviderHint = "local",
            Sensitive = true,
            Status = "DraftReady",
            EstimatedCostUsd = EstimateCost(sourceText),
            CacheKey = sourceHash,
            RequestedById = currentUserId.Value
        };

        await _aiJobRepo.AddAsync(aiJob, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var draft = new AiGeneratedDraft
        {
            AiJobId = aiJob.Id,
            ProjectId = project.Id,
            DraftType = "MeetingActionItems",
            PayloadJson = JsonSerializer.Serialize(extraction, JsonOptions),
            Status = "Pending"
        };

        await _aiDraftRepo.AddAsync(draft, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var meetingImport = new MeetingImport
        {
            ProjectId = project.Id,
            ImportedById = currentUserId.Value,
            SourceProvider = "meetily",
            SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? sourceHash : request.SourceId.Trim(),
            SourceHash = sourceHash,
            Title = request.Title.Trim(),
            MeetingStartedAt = request.MeetingStartedAt,
            Summary = NormalizeOptional(request.Summary),
            TranscriptText = NormalizeOptional(request.TranscriptText) ?? string.Empty,
            ParticipantsJson = JsonSerializer.Serialize(NormalizeParticipants(request.Participants), JsonOptions),
            RawPayloadJson = NormalizeRawPayload(request),
            AiJobId = aiJob.Id,
            AiDraftId = draft.Id
        };

        await _meetingImportRepo.AddAsync(meetingImport, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            "ImportMeetilyMeeting",
            nameof(MeetingImport),
            meetingImport.Id.ToString(),
            new { meetingImport.ProjectId, meetingImport.SourceHash, meetingImport.AiJobId, meetingImport.AiDraftId },
            ct);

        return Result.Created(new MeetilyImportResult(
            meetingImport.Id,
            meetingImport.ProjectId,
            meetingImport.SourceProvider,
            meetingImport.SourceHash,
            Duplicate: false,
            aiJob.Id,
            draft.Id,
            extraction));
    }

    private static Result ValidateRequest(MeetilyImportRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return Result.Failure("projectId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure("title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary) && string.IsNullOrWhiteSpace(request.TranscriptText) && request.ActionItems is not { Count: > 0 })
        {
            return Result.Failure("At least one of summary, transcriptText, or actionItems is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.RawPayloadJson))
        {
            try
            {
                JsonDocument.Parse(request.RawPayloadJson);
            }
            catch (JsonException)
            {
                return Result.Failure("rawPayloadJson must be valid JSON.", 400);
            }
        }

        return Result.Success();
    }

    private static MeetingExtractionPayload BuildExtraction(MeetilyImportRequest request, string sourceHash)
    {
        var participants = NormalizeParticipants(request.Participants);
        var actionItems = BuildActionDrafts(request);
        var keywords = ExtractKeywords($"{request.Title} {request.Summary} {request.TranscriptText}");
        var warnings = new List<string>();

        if (actionItems.Count == 0)
        {
            warnings.Add("No action item was detected. Review the transcript before confirming.");
        }

        if (string.IsNullOrWhiteSpace(request.TranscriptText))
        {
            warnings.Add("Transcript is empty; extraction used summary/action item input only.");
        }

        return new MeetingExtractionPayload(
            "meetily-import.v1",
            sourceHash,
            new MeetingSummaryDto(
                request.Title.Trim(),
                request.MeetingStartedAt,
                NormalizeOptional(request.Summary),
                participants),
            actionItems,
            keywords,
            warnings);
    }

    private static List<MeetingActionDraftDto> BuildActionDrafts(MeetilyImportRequest request)
    {
        var explicitItems = (request.ActionItems ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => new MeetingActionDraftDto(
                item.Title.Trim(),
                NormalizeOptional(item.Evidence),
                NormalizePriority(item.Priority),
                item.DueDate,
                NormalizeOptional(item.Evidence),
                NormalizeOptional(item.Owner)))
            .ToList();

        if (explicitItems.Count > 0)
        {
            return explicitItems.Take(20).ToList();
        }

        return (request.TranscriptText ?? request.Summary ?? string.Empty)
            .Split(['\r', '\n', '.', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => ActionLinePattern().IsMatch(line))
            .Take(10)
            .Select(line => new MeetingActionDraftDto(
                CleanupActionTitle(line),
                "Extracted from Meetily meeting text.",
                "Medium",
                null,
                line,
                null))
            .ToList();
    }

    public async Task<Result<TaskItemDto>> CreateTaskFromMeetingActionItemAsync(
        Guid meetingImportId,
        int actionItemIndex,
        MeetingActionItemCreateRequest request,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<TaskItemDto>();
        }

        if (actionItemIndex < 0)
        {
            return Result.Failure<TaskItemDto>("Action item index must be zero or greater.", 400);
        }

        var meetingImport = await _meetingImportRepo.GetQueryable()
            .Include(item => item.Project)
            .Include(item => item.AiDraft)
            .FirstOrDefaultAsync(item => item.Id == meetingImportId, ct);

        if (meetingImport == null)
        {
            return Result.NotFound<TaskItemDto>("Meeting import not found.");
        }

        if (!await CanAccessProjectAsync(meetingImport.Project, currentUserId.Value, ct))
        {
            return Result.Forbidden<TaskItemDto>();
        }

        var existingMapping = await _mappingRepo.GetQueryable()
            .FirstOrDefaultAsync(mapping => mapping.MeetingImportId == meetingImportId && mapping.ActionItemIndex == actionItemIndex, ct);

        if (existingMapping != null && existingMapping.TaskId.HasValue)
        {
            return Result.Failure<TaskItemDto>("This action item is already linked to a task.", 409);
        }

        if (meetingImport.AiDraft == null || string.IsNullOrWhiteSpace(meetingImport.AiDraft.PayloadJson))
        {
            return Result.Failure<TaskItemDto>("Meeting action items are not available for this import.", 400);
        }

        var extraction = JsonSerializer.Deserialize<MeetingExtractionPayload>(meetingImport.AiDraft.PayloadJson, JsonOptions);
        if (extraction == null)
        {
            return Result.Failure<TaskItemDto>("Could not parse meeting action item extraction.", 500);
        }

        if (actionItemIndex >= extraction.ActionItems.Count)
        {
            return Result.Failure<TaskItemDto>("Action item index is out of range.", 404);
        }

        var actionItem = extraction.ActionItems[actionItemIndex];
        var title = string.IsNullOrWhiteSpace(request.Title) ? actionItem.Title : request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<TaskItemDto>("Task title is required.", 400);
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? (string.IsNullOrWhiteSpace(actionItem.Description) ? actionItem.SourceEvidence : actionItem.Description)
            : request.Description.Trim();

        var priority = !string.IsNullOrWhiteSpace(request.Priority)
            ? request.Priority
            : actionItem.Priority;

        var dueDate = request.DueDate ?? actionItem.DueDate;

        var createDto = new CreateTaskDto(
            title,
            description,
            string.IsNullOrWhiteSpace(priority) ? "Medium" : priority,
            dueDate,
            null,
            meetingImport.ProjectId,
            request.AssigneeId,
            false,
            false,
            true,
            null,
            request.LabelIds);

        var taskResult = await _taskService.CreateAsync(createDto, ct);
        if (!taskResult.IsSuccess || taskResult.Data == null)
        {
            return taskResult;
        }

        var mapping = new MeetingActionItemMapping
        {
            MeetingImportId = meetingImportId,
            ActionItemIndex = actionItemIndex,
            TaskId = taskResult.Data.Id,
            Status = "Linked",
            SourceTitle = actionItem.Title,
            SourcePriority = actionItem.Priority,
            SourceDueDate = actionItem.DueDate,
            SourceQuote = actionItem.SourceEvidence ?? actionItem.Description,
            CreatedById = currentUserId.Value
        };

        if (existingMapping != null)
        {
            existingMapping.TaskId = mapping.TaskId;
            existingMapping.Status = mapping.Status;
            existingMapping.SourceTitle = mapping.SourceTitle;
            existingMapping.SourcePriority = mapping.SourcePriority;
            existingMapping.SourceDueDate = mapping.SourceDueDate;
            existingMapping.SourceQuote = mapping.SourceQuote;
            existingMapping.CreatedById = mapping.CreatedById;
            await _mappingRepo.UpdateAsync(existingMapping, ct);
        }
        else
        {
            await _mappingRepo.AddAsync(mapping, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
            "LinkMeetingActionItemToTask",
            nameof(MeetingActionItemMapping),
            mapping.Id.ToString(),
            new { mapping.MeetingImportId, mapping.TaskId, mapping.ActionItemIndex },
            ct);

        return taskResult;
    }

    public async Task<Result<TaskMeetingSourceDto>> GetTaskMeetingSourceAsync(Guid taskId, CancellationToken ct = default)
    {
        var mapping = await _mappingRepo.GetQueryable()
            .Include(item => item.MeetingImport)
            .FirstOrDefaultAsync(item => item.TaskId == taskId, ct);

        if (mapping == null)
        {
            return Result.NotFound<TaskMeetingSourceDto>("No meeting source mapping found for this task.");
        }

        var meetingImport = mapping.MeetingImport;
        if (meetingImport == null)
        {
            return Result.NotFound<TaskMeetingSourceDto>("Meeting import source is not available.");
        }

        var result = new TaskMeetingSourceDto(
            taskId,
            meetingImport.Id,
            meetingImport.Title,
            meetingImport.MeetingStartedAt,
            mapping.ActionItemIndex,
            mapping.SourceTitle,
            null,
            mapping.SourcePriority,
            mapping.SourceDueDate,
            mapping.SourceQuote,
            mapping.Status,
            mapping.TaskId);

        return Result.Success(result);
    }

    private static string GenerateSourceHash(MeetilyImportRequest request)
    {
        var participants = string.Join("|", NormalizeParticipants(request.Participants));
        var raw = string.Join('\n',
            request.ProjectId.ToString("D"),
            request.SourceId?.Trim() ?? string.Empty,
            request.Title.Trim(),
            request.MeetingStartedAt?.ToUniversalTime().ToString("O") ?? string.Empty,
            request.Summary?.Trim() ?? string.Empty,
            request.TranscriptText?.Trim() ?? string.Empty,
            participants,
            JsonSerializer.Serialize(request.ActionItems ?? [], JsonOptions));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private static string BuildSourceText(MeetilyImportRequest request, MeetingExtractionPayload extraction)
        => string.Join('\n',
            request.Title,
            request.Summary,
            request.TranscriptText,
            string.Join('\n', extraction.ActionItems.Select(item => item.Title)));

    private static decimal EstimateCost(string sourceText)
        => Math.Round((Math.Max(200, sourceText.Length) / 4000m) * 0.002m, 6, MidpointRounding.AwayFromZero);

    private static string NormalizeRawPayload(MeetilyImportRequest request)
        => string.IsNullOrWhiteSpace(request.RawPayloadJson)
            ? JsonSerializer.Serialize(request, JsonOptions)
            : request.RawPayloadJson.Trim();

    private static List<string> NormalizeParticipants(IReadOnlyList<string>? participants)
        => (participants ?? [])
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePriority(string? priority)
        => TaskStatusRules.IsValidPriority(priority ?? string.Empty)
            ? TaskStatusRules.NormalizePriority(priority!)
            : "Medium";

    private static List<string> ExtractKeywords(string text)
        => KeywordPattern()
            .Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length >= 4)
            .GroupBy(word => word)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(12)
            .Select(group => group.Key)
            .ToList();

    private static string CleanupActionTitle(string value)
        => ActionPrefixPattern().Replace(value.Trim(), string.Empty).Trim();

    private async Task<bool> CanAccessProjectAsync(Project project, Guid currentUserId, CancellationToken ct)
    {
        if (IsAdmin() || project.OwnerId == currentUserId)
        {
            return true;
        }

        if (await _projectMemberRepo.GetQueryable().AnyAsync(member => member.ProjectId == project.Id && member.UserId == currentUserId, ct))
        {
            return true;
        }

        if (project.OrganizationId == null)
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

    private bool IsAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

    [GeneratedRegex(@"\b(todo|action|follow up|fix|implement|review|prepare|update|create|assign|deadline|làm|sửa|cập nhật|chuẩn bị|xử lý)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActionLinePattern();

    [GeneratedRegex(@"^(todo|action|follow up|fix|implement|review|prepare|update|create|assign|deadline)\s*[:\-]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActionPrefixPattern();

    [GeneratedRegex(@"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex KeywordPattern();
}
