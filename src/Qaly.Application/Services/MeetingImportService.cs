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
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<GroupMeetingSession> _meetingSessionRepo;
    private readonly IAiGateway _aiGateway;
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
        IRepository<TaskItem> taskRepo,
        IRepository<GroupMeetingSession> meetingSessionRepo,
        IAiGateway aiGateway,
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
        _taskRepo = taskRepo;
        _meetingSessionRepo = meetingSessionRepo;
        _aiGateway = aiGateway;
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

    public async Task<Result<MeetingActionItemsResponseDto>> GetMeetingActionItemsAsync(Guid meetingImportId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<MeetingActionItemsResponseDto>();
        }

        var meetingImportResult = await GetMeetingImportForActionItemsAsync(meetingImportId, currentUserId.Value, ct);
        if (!meetingImportResult.IsSuccess || meetingImportResult.Data == null)
        {
            return Result.Failure<MeetingActionItemsResponseDto>(meetingImportResult.Error!, meetingImportResult.StatusCode);
        }

        var extractionResult = ParseExtractionPayload(meetingImportResult.Data.AiDraft?.PayloadJson);
        if (!extractionResult.IsSuccess || extractionResult.Data == null)
        {
            return Result.Failure<MeetingActionItemsResponseDto>(extractionResult.Error!, extractionResult.StatusCode);
        }

        var mappings = await _mappingRepo.GetQueryable()
            .Where(mapping => mapping.MeetingImportId == meetingImportId)
            .ToDictionaryAsync(mapping => mapping.ActionItemIndex, ct);

        var items = extractionResult.Data.ActionItems
            .Select((item, index) =>
            {
                mappings.TryGetValue(index, out var mapping);
                var title = string.IsNullOrWhiteSpace(item.Title)
                    ? (item.Description ?? item.SourceEvidence ?? $"Action item #{index}")
                    : item.Title;
                var description = string.IsNullOrWhiteSpace(item.Description) ? item.SourceEvidence : item.Description;
                var mappingStatus = ResolveMappingStatus(mapping);

                return new MeetingActionItemDto(
                    index,
                    title,
                    description,
                    item.SuggestedOwnerName,
                    string.IsNullOrWhiteSpace(item.Priority) ? "Medium" : item.Priority,
                    item.DueDate,
                    mappingStatus,
                    mapping?.TaskId);
            })
            .ToList();

        return Result.Success(new MeetingActionItemsResponseDto(meetingImportId, items));
    }

    public async Task<Result<MeetingActionItemTaskLinkDto>> LinkMeetingActionItemToTaskAsync(
        Guid meetingImportId,
        int actionItemIndex,
        LinkMeetingActionItemTaskRequest request,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<MeetingActionItemTaskLinkDto>();
        }

        if (actionItemIndex < 0)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("Action item index must be zero or greater.", 400);
        }

        if (request == null)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("Request body is required.", 400);
        }

        if (request.TaskId == Guid.Empty)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("taskId is required.", 400);
        }

        var meetingImportResult = await GetMeetingImportForActionItemsAsync(meetingImportId, currentUserId.Value, ct);
        if (!meetingImportResult.IsSuccess || meetingImportResult.Data == null)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>(meetingImportResult.Error!, meetingImportResult.StatusCode);
        }

        var extractionResult = ParseExtractionPayload(meetingImportResult.Data.AiDraft?.PayloadJson);
        if (!extractionResult.IsSuccess || extractionResult.Data == null)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>(extractionResult.Error!, extractionResult.StatusCode);
        }

        if (actionItemIndex >= extractionResult.Data.ActionItems.Count)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("Action item index is out of range.", 404);
        }

        var task = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.TaskId, ct);
        if (task == null)
        {
            return Result.NotFound<MeetingActionItemTaskLinkDto>("Task not found.");
        }

        if (task.ProjectId != meetingImportResult.Data.ProjectId)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("Task does not belong to the same project as this meeting import.", 400);
        }

        var existingMapping = await _mappingRepo.GetQueryable()
            .FirstOrDefaultAsync(mapping =>
                mapping.MeetingImportId == meetingImportId &&
                mapping.ActionItemIndex == actionItemIndex,
                ct);
        if (existingMapping != null)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("This action item already has a task mapping.", 409);
        }

        var actionItem = extractionResult.Data.ActionItems[actionItemIndex];
        var mapping = new MeetingActionItemMapping
        {
            MeetingImportId = meetingImportId,
            ActionItemIndex = actionItemIndex,
            TaskId = request.TaskId,
            Status = "Linked",
            SourceTitle = actionItem.Title,
            SourcePriority = actionItem.Priority,
            SourceDueDate = actionItem.DueDate,
            SourceQuote = actionItem.SourceEvidence ?? actionItem.Description,
            CreatedById = currentUserId.Value
        };

        await _mappingRepo.AddAsync(mapping, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
            "LinkMeetingActionItemToExistingTask",
            nameof(MeetingActionItemMapping),
            mapping.Id.ToString(),
            new { mapping.MeetingImportId, mapping.ActionItemIndex, mapping.TaskId },
            ct);

        return Result.Success(new MeetingActionItemTaskLinkDto(
            meetingImportId,
            actionItemIndex,
            true,
            request.TaskId,
            mapping.Status));
    }

    public async Task<Result<MeetingActionItemTaskLinkDto>> GetMeetingActionItemTaskLinkAsync(
        Guid meetingImportId,
        int actionItemIndex,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<MeetingActionItemTaskLinkDto>();
        }

        if (actionItemIndex < 0)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("Action item index must be zero or greater.", 400);
        }

        var meetingImportResult = await GetMeetingImportForActionItemsAsync(meetingImportId, currentUserId.Value, ct);
        if (!meetingImportResult.IsSuccess || meetingImportResult.Data == null)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>(meetingImportResult.Error!, meetingImportResult.StatusCode);
        }

        var extractionResult = ParseExtractionPayload(meetingImportResult.Data.AiDraft?.PayloadJson);
        if (!extractionResult.IsSuccess || extractionResult.Data == null)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>(extractionResult.Error!, extractionResult.StatusCode);
        }

        if (actionItemIndex >= extractionResult.Data.ActionItems.Count)
        {
            return Result.Failure<MeetingActionItemTaskLinkDto>("Action item index is out of range.", 404);
        }

        var mapping = await _mappingRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.MeetingImportId == meetingImportId &&
                item.ActionItemIndex == actionItemIndex,
                ct);

        return Result.Success(new MeetingActionItemTaskLinkDto(
            meetingImportId,
            actionItemIndex,
            mapping?.TaskId != null,
            mapping?.TaskId,
            ResolveMappingStatus(mapping)));
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
                .ThenInclude(import => import.AiDraft)
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

        MeetingActionDraftDto? actionItem = null;
        var extractionResult = ParseExtractionPayload(meetingImport.AiDraft?.PayloadJson);
        if (extractionResult.IsSuccess && extractionResult.Data != null &&
            mapping.ActionItemIndex >= 0 &&
            mapping.ActionItemIndex < extractionResult.Data.ActionItems.Count)
        {
            actionItem = extractionResult.Data.ActionItems[mapping.ActionItemIndex];
        }

        var sourceTitle = !string.IsNullOrWhiteSpace(mapping.SourceTitle)
            ? mapping.SourceTitle
            : actionItem?.Title;
        var sourceDescription = actionItem == null
            ? null
            : string.IsNullOrWhiteSpace(actionItem.Description)
                ? actionItem.SourceEvidence
                : actionItem.Description;
        var sourcePriority = !string.IsNullOrWhiteSpace(mapping.SourcePriority)
            ? mapping.SourcePriority
            : actionItem?.Priority;
        var sourceDueDate = mapping.SourceDueDate ?? actionItem?.DueDate;
        var sourceQuote = !string.IsNullOrWhiteSpace(mapping.SourceQuote)
            ? mapping.SourceQuote
            : actionItem?.SourceEvidence ?? actionItem?.Description;

        var result = new TaskMeetingSourceDto(
            taskId,
            meetingImport.Id,
            meetingImport.Title,
            meetingImport.MeetingStartedAt,
            mapping.ActionItemIndex,
            sourceTitle,
            sourceDescription,
            sourcePriority,
            sourceDueDate,
            sourceQuote,
            mapping.Status,
            mapping.TaskId);

        return Result.Success(result);
    }

    private async Task<Result<MeetingImport>> GetMeetingImportForActionItemsAsync(Guid meetingImportId, Guid currentUserId, CancellationToken ct)
    {
        var meetingImport = await _meetingImportRepo.GetQueryable()
            .Include(item => item.Project)
            .Include(item => item.AiDraft)
            .FirstOrDefaultAsync(item => item.Id == meetingImportId, ct);

        if (meetingImport == null)
        {
            return Result.NotFound<MeetingImport>("Meeting import not found.");
        }

        if (!await CanAccessProjectAsync(meetingImport.Project, currentUserId, ct))
        {
            return Result.Forbidden<MeetingImport>();
        }

        return Result.Success(meetingImport);
    }

    private static Result<MeetingExtractionPayload> ParseExtractionPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return Result.Failure<MeetingExtractionPayload>("Meeting action items are not available for this import.", 400);
        }

        try
        {
            var extraction = JsonSerializer.Deserialize<MeetingExtractionPayload>(payloadJson, JsonOptions);
            if (extraction == null)
            {
                return Result.Failure<MeetingExtractionPayload>("Could not parse meeting action items draft payload.", 400);
            }

            return Result.Success(extraction);
        }
        catch (JsonException)
        {
            return Result.Failure<MeetingExtractionPayload>("Could not parse meeting action items draft payload.", 400);
        }
    }

    private static string ResolveMappingStatus(MeetingActionItemMapping? mapping)
    {
        if (mapping == null)
        {
            return "NotLinked";
        }

        if (!string.IsNullOrWhiteSpace(mapping.Status))
        {
            return mapping.Status;
        }

        return mapping.TaskId.HasValue ? "Linked" : "NotLinked";
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

    public async Task<Result<AutoChecknoteResponseDto>> CreateAutoChecknoteAsync(
        Guid meetingSessionId,
        AutoChecknoteRequest request,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<AutoChecknoteResponseDto>();
        }

        // --- HARD-CAP Transcript length (Sprint 1) ---
        if (request.TranscriptText != null && request.TranscriptText.Length > 80000)
        {
            return Result.Failure<AutoChecknoteResponseDto>("Độ dài transcript vượt quá giới hạn cho phép (80,000 ký tự). Vui lòng giới hạn hoặc tóm tắt thủ công.", 400);
        }

        var project = await _projectRepo.GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct);
        if (project == null)
        {
            return Result.NotFound<AutoChecknoteResponseDto>("Không tìm thấy dự án.");
        }

        if (!await CanAccessProjectAsync(project, currentUserId.Value, ct))
        {
            return Result.Forbidden<AutoChecknoteResponseDto>();
        }

        // --- IDEMPOTENCY check (Sprint 2) ---
        var sourceHash = ComputeSha256Hash(request.ProjectId, meetingSessionId, request.TranscriptText ?? string.Empty);
        var existing = await _meetingImportRepo.GetQueryable()
            .Include(item => item.AiDraft)
            .FirstOrDefaultAsync(item =>
                item.ProjectId == request.ProjectId &&
                item.SourceProvider == "qaly-meet" &&
                item.SourceHash == sourceHash,
                ct);

        if (existing != null)
        {
            var existingItemsResult = await GetMeetingActionItemsAsync(existing.Id, ct);
            var actionItems = existingItemsResult.IsSuccess && existingItemsResult.Data != null
                ? existingItemsResult.Data.Items
                : Array.Empty<MeetingActionItemDto>();

            return Result.Success(new AutoChecknoteResponseDto(
                existing.Id,
                existing.ProjectId,
                existing.AiJobId ?? Guid.Empty,
                existing.AiDraftId ?? Guid.Empty,
                existing.Summary ?? string.Empty,
                actionItems));
        }

        // 1. Call AI Gateway
        var prompt = BuildAutoChecknotePrompt(request.TranscriptText ?? string.Empty);
        var aiResponse = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "AI-06_MEETING_EXTRACT",
            Prompt = prompt,
            UserId = currentUserId.Value,
            ExpectedSchemaId = "AutoChecknote",
            UseCache = true
        }, ct);

        // 2. Parse AI response
        var (summary, actionItemsList) = ParseAutoChecknoteResponse(aiResponse.Content);

        // 3. Build MeetingExtractionPayload for Draft
        var normalizedParticipants = NormalizeParticipants(request.Participants);
        var actionDrafts = actionItemsList.Select(x => new MeetingActionDraftDto(
            x.Title,
            x.Description,
            NormalizePriority(x.Priority),
            string.IsNullOrEmpty(x.DueDate) ? null : DateTimeOffset.Parse(x.DueDate, System.Globalization.CultureInfo.InvariantCulture),
            x.Evidence,
            x.SuggestedOwner
        )).ToList();

        var extraction = new MeetingExtractionPayload(
            "qaly-meet.v1",
            sourceHash,
            new MeetingSummaryDto(
                request.Title.Trim(),
                DateTimeOffset.UtcNow,
                summary,
                normalizedParticipants),
            actionDrafts,
            ExtractKeywords($"{request.Title} {summary} {request.TranscriptText}"),
            actionDrafts.Count == 0 ? ["No action item was detected."] : new List<string>());

        // 4. Create AiJob
        var aiJob = new AiJob
        {
            JobType = "AI-06_MEETING_EXTRACT",
            ProjectId = project.Id,
            SourceType = "qaly_meet",
            SourceId = meetingSessionId.ToString(),
            ProviderHint = "auto",
            Sensitive = true,
            Status = "DraftReady",
            EstimatedCostUsd = EstimateCost(request.TranscriptText ?? string.Empty),
            CacheKey = sourceHash,
            RequestedById = currentUserId.Value
        };
        await _aiJobRepo.AddAsync(aiJob, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 5. Create AiGeneratedDraft
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

        // 6. Create MeetingImport
        var meetingImport = new MeetingImport
        {
            ProjectId = project.Id,
            ImportedById = currentUserId.Value,
            SourceProvider = "qaly-meet",
            SourceId = meetingSessionId.ToString(),
            SourceHash = sourceHash,
            Title = request.Title.Trim(),
            MeetingStartedAt = DateTimeOffset.UtcNow,
            Summary = summary,
            TranscriptText = request.TranscriptText ?? string.Empty,
            ParticipantsJson = JsonSerializer.Serialize(normalizedParticipants, JsonOptions),
            AiJobId = aiJob.Id,
            AiDraftId = draft.Id
        };
        await _meetingImportRepo.AddAsync(meetingImport, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 7. Update Meeting Session if exists
        var meetingSession = await _meetingSessionRepo.GetByIdAsync(meetingSessionId, ct);
        if (meetingSession != null)
        {
            meetingSession.Summary = summary;
            meetingSession.TranscriptSourceId = meetingImport.Id.ToString();
            meetingSession.Status = "Ended";
            meetingSession.EndedAt = DateTimeOffset.UtcNow;
            await _meetingSessionRepo.UpdateAsync(meetingSession, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        await _auditLogService.LogAsync(
            "AutoChecknoteImport",
            nameof(MeetingImport),
            meetingImport.Id.ToString(),
            new { meetingImport.ProjectId, meetingImport.SourceHash, meetingImport.AiJobId },
            ct);

        // Get MeetingActionItemDto list
        var actionItemsResult = await GetMeetingActionItemsAsync(meetingImport.Id, ct);
        var finalActionItems = actionItemsResult.IsSuccess && actionItemsResult.Data != null
            ? actionItemsResult.Data.Items
            : Array.Empty<MeetingActionItemDto>();

        return Result.Created(new AutoChecknoteResponseDto(
            meetingImport.Id,
            project.Id,
            aiJob.Id,
            draft.Id,
            summary,
            finalActionItems));
    }

    private static string ComputeSha256Hash(Guid projectId, Guid meetingSessionId, string transcript)
    {
        var raw = string.Join('\n',
            projectId.ToString("D"),
            meetingSessionId.ToString("D"),
            transcript ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private static string BuildAutoChecknotePrompt(string transcriptText)
        => $$"""
You are an expert AI meeting assistant. Analyze the following meeting transcript in Vietnamese and:
1. Summarize the meeting in Vietnamese (brief summary under 200 words).
2. Extract actionable work items (Action Items) discussed in the meeting.

Return only valid JSON with this shape:
{
  "summary": "Tóm tắt cuộc họp ngắn gọn...",
  "actionItems": [
    {
      "title": "Tiêu đề công việc ngắn gọn và rõ ràng",
      "description": "Chi tiết công việc (nếu có)",
      "suggestedOwner": "Tên người được giao việc (hoặc null)",
      "dueDate": "ISO-8601 date (yyyy-MM-dd) (hoặc null)",
      "priority": "High hoặc Medium hoặc Low (mặc định Medium)",
      "evidence": "Trích dẫn câu nói trong transcript làm bằng chứng cho việc giao việc này"
    }
  ]
}

Rules:
- Title, description and summary MUST be in Vietnamese.
- Keep title under 120 characters.
- Use null when owner or due date is not mentioned or supported.
- Return an empty actionItems array when there are no action items.
- Ensure the output is strictly a valid JSON object. Do not include markdown blocks or conversational text.

Transcript:
{{transcriptText}}
""";

    private static string ExtractJson(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return trimmed;
        }

        var objectStart = trimmed.IndexOf('{', StringComparison.Ordinal);
        var objectEnd = trimmed.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
        {
            return trimmed[objectStart..(objectEnd + 1)];
        }

        var arrayStart = trimmed.IndexOf('[', StringComparison.Ordinal);
        var arrayEnd = trimmed.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            return trimmed[arrayStart..(arrayEnd + 1)];
        }

        return trimmed;
    }

    private static (string Summary, List<AutoChecknoteActionItem> ActionItems) ParseAutoChecknoteResponse(string content)
    {
        var json = ExtractJson(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return ("Không có tóm tắt cuộc họp.", new List<AutoChecknoteActionItem>());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var summary = root.TryGetProperty("summary", out var summaryProp) && summaryProp.ValueKind == JsonValueKind.String
                ? summaryProp.GetString() ?? string.Empty
                : string.Empty;

            var actionItems = new List<AutoChecknoteActionItem>();
            if (root.TryGetProperty("actionItems", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsProp.EnumerateArray())
                {
                    var title = item.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                        ? titleProp.GetString() ?? string.Empty
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var description = item.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                        ? descProp.GetString()
                        : null;

                    var owner = item.TryGetProperty("suggestedOwner", out var ownerProp) && ownerProp.ValueKind == JsonValueKind.String
                        ? ownerProp.GetString()
                        : null;

                    string? dueDate = null;
                    if (item.TryGetProperty("dueDate", out var dueProp) && dueProp.ValueKind == JsonValueKind.String)
                    {
                        dueDate = dueProp.GetString();
                    }

                    var priority = item.TryGetProperty("priority", out var priorityProp) && priorityProp.ValueKind == JsonValueKind.String
                        ? priorityProp.GetString()
                        : "Medium";

                    var evidence = item.TryGetProperty("evidence", out var evProp) && evProp.ValueKind == JsonValueKind.String
                        ? evProp.GetString()
                        : null;

                    actionItems.Add(new AutoChecknoteActionItem
                    {
                        Title = title,
                        Description = description,
                        SuggestedOwner = owner,
                        DueDate = dueDate,
                        Priority = priority,
                        Evidence = evidence
                    });
                }
            }

            return (summary, actionItems);
        }
        catch (JsonException)
        {
            return ("Không thể phân tích tóm tắt cuộc họp.", new List<AutoChecknoteActionItem>());
        }
    }

    private class AutoChecknoteActionItem
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? SuggestedOwner { get; set; }
        public string? DueDate { get; set; }
        public string? Priority { get; set; }
        public string? Evidence { get; set; }
    }
}
