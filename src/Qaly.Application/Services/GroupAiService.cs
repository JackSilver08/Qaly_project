using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class GroupAiService : IGroupAiService
{
    private const int DefaultMessageLimit = 80;
    private const int MaxMessageLimit = 150;
    private static readonly Action<ILogger, Guid, Exception?> AiExtractionFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(AiExtractionFailed)),
            "AI action item extraction failed for group {GroupId}");
    private static readonly Action<ILogger, Guid, Exception?> DiscussionSummaryFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2, nameof(DiscussionSummaryFailed)),
            "Failed to summarize group discussion for group {GroupId}");
    private static readonly Action<ILogger, Guid, Exception?> DraftProjectPayloadGenerationFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(3, nameof(DraftProjectPayloadGenerationFailed)),
            "Failed to generate draft project payload for group {GroupId}");
    private readonly IAiGateway _aiGateway;
    private readonly IGroupsService _groupsService;
    private readonly IRepository<GroupMessage> _messageRepo;
    private readonly IRepository<GroupMeetingSession> _meetingSessionRepo;
    private readonly IRepository<MeetingImport> _meetingImportRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<GroupAiService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public GroupAiService(
        IAiGateway aiGateway,
        IGroupsService groupsService,
        IRepository<GroupMessage> messageRepo,
        IRepository<GroupMeetingSession> meetingSessionRepo,
        IRepository<MeetingImport> meetingImportRepo,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<GroupAiService> logger,
        ICurrentUserService currentUserService)
    {
        _aiGateway = aiGateway;
        _groupsService = groupsService;
        _messageRepo = messageRepo;
        _meetingSessionRepo = meetingSessionRepo;
        _meetingImportRepo = meetingImportRepo;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<Result<GroupAiActionItemsResponseDto>> ExtractActionItemsAsync(
        Guid groupId,
        GroupAiActionItemsRequest request,
        CancellationToken ct = default)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupAiActionItemsResponseDto>();
        }

        var source = NormalizeSource(request.Source);
        var context = await BuildContextAsync(groupId, request, source, ct);
        if (string.IsNullOrWhiteSpace(context.Text))
        {
            return Result.Success(new GroupAiActionItemsResponseDto(
                groupId,
                source,
                Array.Empty<GroupAiActionItemDto>(),
                ["No group chat or transcript content is available for AI extraction."]));
        }

        try
        {
            var response = await _aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "ActionItemsExtraction",
                Prompt = BuildActionItemsPrompt(context.Text),
                UserId = _currentUserService.UserId,
                ExpectedSchemaId = "ActionItem",
                UseCache = true
            }, ct);
            var text = response.Content;
            var items = ParseActionItems(text);
            var warnings = context.Warnings.ToList();

            if (items.Count == 0)
            {
                warnings.Add("AI did not return any valid action items.");
            }

            await _auditLogService.LogAsync("AI_ACTION_ITEMS_EXTRACTED", "WorkGroup", groupId.ToString(), new { MessageLimit = request.MessageLimit }, ct);

            return Result.Success(new GroupAiActionItemsResponseDto(groupId, source, items, warnings));
        }
        catch (Exception ex)
        {
            AiExtractionFailed(_logger, groupId, ex);
            return Result.Success(new GroupAiActionItemsResponseDto(
                groupId,
                source,
                Array.Empty<GroupAiActionItemDto>(),
                [.. context.Warnings, "AI extraction failed. Please try again later."]));
        }
    }

    private async Task<GroupAiContext> BuildContextAsync(
        Guid groupId,
        GroupAiActionItemsRequest request,
        string source,
        CancellationToken ct)
    {
        var warnings = new List<string>();
        var builder = new StringBuilder();

        if (source is "chat" or "mixed")
        {
            var limit = Math.Clamp(request.MessageLimit ?? DefaultMessageLimit, 1, MaxMessageLimit);
            var messages = await _messageRepo.GetQueryable()
                .AsNoTracking()
                .Include(message => message.User)
                .Where(message => message.WorkGroupId == groupId)
                .OrderByDescending(message => message.CreatedAt)
                .Take(limit)
                .OrderBy(message => message.CreatedAt)
                .ToListAsync(ct);

            if (messages.Count == 0)
            {
                warnings.Add("No chat messages were found for this group.");
            }
            else
            {
                builder.AppendLine("Chat messages:");
                foreach (var message in messages)
                {
                    builder
                        .Append('[')
                        .Append(message.CreatedAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture))
                        .Append("] ")
                        .Append(message.User.FullName)
                        .Append(": ")
                        .AppendLine(message.Content);
                }
            }
        }

        if (source is "transcript" or "mixed")
        {
            var transcript = NormalizeOptional(request.TranscriptText);
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                builder.AppendLine();
                builder.AppendLine("Meeting transcript:");
                builder.AppendLine(transcript);
            }
            else if (request.MeetingSessionId.HasValue)
            {
                var meeting = await _meetingSessionRepo.GetQueryable()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == request.MeetingSessionId.Value && item.WorkGroupId == groupId, ct);

                string? resolvedTranscriptText = null;
                if (meeting != null && !string.IsNullOrWhiteSpace(meeting.TranscriptSourceId))
                {
                    if (Guid.TryParse(meeting.TranscriptSourceId, out var importId))
                    {
                        var import = await _meetingImportRepo.GetQueryable()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(item => item.Id == importId, ct);
                        resolvedTranscriptText = import?.TranscriptText;
                    }
                    else
                    {
                        var sourceId = meeting.TranscriptSourceId;
                        string? provider = null;
                        var colonIdx = meeting.TranscriptSourceId.IndexOf(':');
                        if (colonIdx >= 0)
                        {
                            provider = meeting.TranscriptSourceId.Substring(0, colonIdx);
                            sourceId = meeting.TranscriptSourceId.Substring(colonIdx + 1);
                        }

                        var import = await _meetingImportRepo.GetQueryable()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(item => item.SourceId == sourceId && (provider == null || item.SourceProvider == provider), ct);
                        resolvedTranscriptText = import?.TranscriptText;
                    }
                }

                if (!string.IsNullOrWhiteSpace(resolvedTranscriptText))
                {
                    builder.AppendLine();
                    builder.AppendLine("Meeting transcript:");
                    builder.AppendLine(resolvedTranscriptText);
                }
                else if (!string.IsNullOrWhiteSpace(meeting?.Summary))
                {
                    builder.AppendLine();
                    builder.AppendLine("Meeting summary:");
                    builder.AppendLine(meeting.Summary);
                    warnings.Add("Meeting transcript was not available; extraction used the meeting summary.");
                }
                else
                {
                    warnings.Add("Meeting transcript or summary was not available for the selected meeting.");
                }
            }
        }

        return new GroupAiContext(builder.ToString().Trim(), warnings);
    }

    private static string BuildActionItemsPrompt(string context)
        => $$"""
You extract actionable work items from a project team's group chat or meeting transcript.

Return only valid JSON with this shape:
{
  "items": [
    {
      "title": "short actionable title",
      "description": "optional detail",
      "suggestedOwnerName": "optional person name",
      "dueDateSuggestion": "optional ISO-8601 date",
      "confidence": 0.0,
      "sourceEvidence": "short quote or paraphrase from the source"
    }
  ]
}

Rules:
- Do not create tasks or persist anything.
- Prefer clear commitments, decisions, follow-ups, blockers, and assigned work.
- Keep title under 120 characters.
- confidence must be between 0 and 1.
- Use null when owner or due date is not supported by the source.
- Return an empty items array when there are no action items.

Source:
{{context}}
""";

    private static IReadOnlyList<GroupAiActionItemDto> ParseActionItems(string responseText)
    {
        var json = ExtractJson(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GroupAiActionItemDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var itemsElement = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement
                : document.RootElement.TryGetProperty("items", out var nestedItems)
                    ? nestedItems
                    : default;

            if (itemsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GroupAiActionItemDto>();
            }

            var items = new List<GroupAiActionItemDto>();
            foreach (var item in itemsElement.EnumerateArray())
            {
                var title = ReadString(item, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                items.Add(new GroupAiActionItemDto(
                    title.Trim(),
                    NormalizeOptional(ReadString(item, "description")),
                    NormalizeOptional(ReadString(item, "suggestedOwnerName")),
                    ReadDate(item, "dueDateSuggestion"),
                    ClampConfidence(ReadDecimal(item, "confidence")),
                    NormalizeOptional(ReadString(item, "sourceEvidence"))));
            }

            return items;
        }
        catch (JsonException)
        {
            return Array.Empty<GroupAiActionItemDto>();
        }
    }

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
        return arrayStart >= 0 && arrayEnd > arrayStart
            ? trimmed[arrayStart..(arrayEnd + 1)]
            : string.Empty;
    }

    private static string NormalizeSource(string? source)
    {
        var normalized = source?.Trim().ToLowerInvariant();
        return normalized is "chat" or "transcript" or "mixed" ? normalized : "mixed";
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement element, string propertyName)
        => DateTimeOffset.TryParse(ReadString(element, propertyName), out var value) ? value : null;

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0.5m;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out var parsed)
            ? parsed
            : 0.5m;
    }

    private static decimal ClampConfidence(decimal value)
        => Math.Clamp(value, 0m, 1m);

    public async Task<Result<GroupMeetingSessionDto>> LinkMeetingSummaryAndTranscriptAsync(
        Guid groupId,
        Guid meetingId,
        string? summary,
        string? transcriptSourceId,
        CancellationToken ct = default)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<GroupMeetingSessionDto>();
        }

        var meeting = await _meetingSessionRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.Id == meetingId && item.WorkGroupId == groupId, ct);

        if (meeting == null)
        {
            return Result.NotFound<GroupMeetingSessionDto>("Group meeting session was not found.");
        }

        meeting.Summary = summary;
        meeting.TranscriptSourceId = transcriptSourceId;

        await _meetingSessionRepo.UpdateAsync(meeting, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = new GroupMeetingSessionDto(
            meeting.Id,
            meeting.WorkGroupId,
            meeting.StartedByUserId,
            meeting.Provider,
            meeting.RoomId,
            meeting.JoinUrl,
            meeting.Status,
            meeting.StartedAt,
            meeting.EndedAt,
            meeting.TranscriptSourceId,
            meeting.Summary);

        await _auditLogService.LogAsync("MEETING_SUMMARY_LINKED", "GroupMeetingSession", meetingId.ToString(), new { SummaryLength = summary?.Length, TranscriptSourceId = transcriptSourceId }, ct);

        return Result.Success(dto);
    }

    public async Task<Result<string>> BuildGroupChatContextAsync(
        Guid groupId,
        int? limit = null,
        CancellationToken ct = default)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, ct))
        {
            return Result.Forbidden<string>();
        }

        var msgLimit = Math.Clamp(limit ?? DefaultMessageLimit, 1, MaxMessageLimit);
        var messages = await _messageRepo.GetQueryable()
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.WorkGroupId == groupId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(msgLimit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return Result.Success(string.Empty);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Chat messages:");
        foreach (var message in messages)
        {
            sb.Append('[')
              .Append(message.CreatedAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture))
              .Append("] ")
              .Append(message.User.FullName)
              .Append(": ")
              .AppendLine(message.Content);
        }

        return Result.Success(sb.ToString().Trim());
    }

    public async Task<Result<GroupAiSummaryResponseDto>> SummarizeGroupDiscussionAsync(
        Guid groupId,
        GroupAiSummaryRequest request,
        CancellationToken ct = default)
    {
        var contextResult = await BuildGroupChatContextAsync(groupId, request.MessageLimit, ct);
        if (!contextResult.IsSuccess)
        {
            return Result.Failure<GroupAiSummaryResponseDto>(contextResult.Error!, contextResult.StatusCode);
        }

        var contextText = contextResult.Data ?? string.Empty;
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(contextText))
        {
            return Result.Success(new GroupAiSummaryResponseDto(
                groupId,
                "No group chat discussion is available to summarize.",
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["No chat messages were found for this group."]));
        }

        try
        {
            var response = await _aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "DiscussionSummary",
                Prompt = BuildSummaryPrompt(contextText),
                UserId = _currentUserService.UserId,
                ExpectedSchemaId = "DiscussionSummary",
                UseCache = true
            }, ct);
            var text = response.Content;
            var json = ExtractJson(text);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Result.Success(new GroupAiSummaryResponseDto(
                    groupId,
                    "AI summary generation did not return a valid response.",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    ["AI failed to output valid JSON summary."]));
            }

            using var document = JsonDocument.Parse(json);
            var summary = ReadString(document.RootElement, "summary") ?? "No summary generated.";
            var decisions = ReadStringArray(document.RootElement, "keyDecisions");
            var questions = ReadStringArray(document.RootElement, "unresolvedQuestions");
            var messageSources = ReadStringArray(document.RootElement, "messageSources");

            await _auditLogService.LogAsync("AI_DISCUSSION_SUMMARIZED", "WorkGroup", groupId.ToString(), new { MessageLimit = request.MessageLimit }, ct);

            return Result.Success(new GroupAiSummaryResponseDto(
                groupId,
                summary,
                decisions,
                questions,
                warnings,
                messageSources));
        }
        catch (Exception ex)
        {
            DiscussionSummaryFailed(_logger, groupId, ex);
            return Result.Success(new GroupAiSummaryResponseDto(
                groupId,
                "Failed to summarize the discussion due to an internal AI error.",
                Array.Empty<string>(),
                Array.Empty<string>(),
                ["AI summary call failed. please try again."]));
        }
    }

    public async Task<Result<GroupAiDraftProjectResponseDto>> GenerateDraftProjectPayloadAsync(
        Guid groupId,
        GroupAiDraftProjectRequest request,
        CancellationToken ct = default)
    {
        var contextResult = await BuildGroupChatContextAsync(groupId, request.MessageLimit, ct);
        if (!contextResult.IsSuccess)
        {
            return Result.Failure<GroupAiDraftProjectResponseDto>(contextResult.Error!, contextResult.StatusCode);
        }

        var contextText = contextResult.Data ?? string.Empty;
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(contextText))
        {
            return Result.Success(new GroupAiDraftProjectResponseDto(
                groupId,
                "Draft Project",
                "No discussion available to draft a project from.",
                Array.Empty<GroupAiDraftTaskDto>(),
                ["No chat messages were found for this group."]));
        }

        try
        {
            var response = await _aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "DraftProjectPayload",
                Prompt = BuildDraftProjectPrompt(contextText, request.ExtraInstructions),
                UserId = _currentUserService.UserId,
                ExpectedSchemaId = "DraftProject",
                UseCache = true
            }, ct);
            var text = response.Content;
            var json = ExtractJson(text);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Result.Success(new GroupAiDraftProjectResponseDto(
                    groupId,
                    "Draft Project",
                    "No discussion available to draft a project from.",
                    Array.Empty<GroupAiDraftTaskDto>(),
                    ["AI failed to output valid JSON draft project payload."]));
            }

            using var document = JsonDocument.Parse(json);
            var projectName = ReadString(document.RootElement, "draftProjectName") ?? "Draft Project";
            var projectDesc = ReadString(document.RootElement, "draftProjectDescription") ?? "Draft project generated from discussion.";
            
            var tasks = new List<GroupAiDraftTaskDto>();
            if (document.RootElement.TryGetProperty("draftTasks", out var tasksElement) && tasksElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tasksElement.EnumerateArray())
                {
                    var title = ReadString(item, "title");
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var description = ReadString(item, "description");
                    var priority = ReadString(item, "priority") ?? "P1";
                    int? estimateDays = null;
                    if (item.TryGetProperty("estimateDays", out var estProp) && estProp.ValueKind == JsonValueKind.Number)
                    {
                        estimateDays = estProp.GetInt32();
                    }
                    var owner = ReadString(item, "suggestedOwnerName");

                    tasks.Add(new GroupAiDraftTaskDto(title.Trim(), description, priority, estimateDays, owner));
                }
            }

            await _auditLogService.LogAsync("AI_DRAFT_PROJECT_GENERATED", "WorkGroup", groupId.ToString(), new { MessageLimit = request.MessageLimit }, ct);

            return Result.Success(new GroupAiDraftProjectResponseDto(
                groupId,
                projectName,
                projectDesc,
                tasks,
                warnings));
        }
        catch (Exception ex)
        {
            DraftProjectPayloadGenerationFailed(_logger, groupId, ex);
            return Result.Success(new GroupAiDraftProjectResponseDto(
                groupId,
                "Draft Project",
                "Draft generation failed.",
                Array.Empty<GroupAiDraftTaskDto>(),
                ["AI draft generation failed due to an error."]));
        }
    }

    private static string BuildSummaryPrompt(string context)
        => $$"""
You are an AI assistant analyzing a project team's group chat discussion.
Create a comprehensive summary, extract key decisions made, list unresolved questions, and cite message sources/evidence (specific message statements or key inputs).

Return only valid JSON with this shape:
{
  "summary": "a cohesive high-level summary of the group's discussion",
  "keyDecisions": [
    "decision 1",
    "decision 2"
  ],
  "unresolvedQuestions": [
    "question 1",
    "question 2"
  ],
  "messageSources": [
    "short quote or evidence 1",
    "short quote or evidence 2"
  ]
}

Rules:
- Keep the summary clear, professional, and factual.
- If there are no key decisions, return an empty array for keyDecisions.
- If there are no unresolved questions, return an empty array for unresolvedQuestions.
- If there are no message sources, return an empty array for messageSources.

Source discussion context:
{{context}}
""";

    private static string BuildDraftProjectPrompt(string context, string? extraInstructions)
        => $$"""
You are an AI assistant analyzing a project team's group chat discussion to build a draft project plan.
Based on their discussion, extract:
1. A draft project name.
2. A draft project description.
3. A list of draft tasks needed to execute this project, including estimate days and suggested owner if mentioned in the context.

Return only valid JSON with this shape:
{
  "draftProjectName": "a descriptive, short project name",
  "draftProjectDescription": "a professional high-level summary of the project scope",
  "draftTasks": [
    {
      "title": "short actionable task title (max 120 chars)",
      "description": "optional task details or deliverables",
      "priority": "P0 or P1 or P2 (default to P0 if high priority/critical, else P1/P2)",
      "estimateDays": 2, // estimated effort in days (integer)
      "suggestedOwnerName": "owner name if discussed, else null"
    }
  ]
}

Rules:
- Do not create or persist anything.
- Assign priorities (P0, P1, P2) carefully based on the discussion urgency.
- If extra instructions are provided below, strictly follow them.

{{(string.IsNullOrWhiteSpace(extraInstructions) ? "" : $"Extra instructions:\n{extraInstructions}\n")}}

Source discussion context:
{{context}}
""";

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s.Trim());
                }
            }
            return list;
        }
        return Array.Empty<string>();
    }

    private sealed record GroupAiContext(string Text, IReadOnlyList<string> Warnings);
}
