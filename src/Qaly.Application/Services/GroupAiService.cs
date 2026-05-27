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
    private readonly IChatClient _chatClient;
    private readonly IGroupsService _groupsService;
    private readonly IRepository<GroupMessage> _messageRepo;
    private readonly IRepository<GroupMeetingSession> _meetingSessionRepo;
    private readonly ILogger<GroupAiService> _logger;

    public GroupAiService(
        IChatClient chatClient,
        IGroupsService groupsService,
        IRepository<GroupMessage> messageRepo,
        IRepository<GroupMeetingSession> meetingSessionRepo,
        ILogger<GroupAiService> logger)
    {
        _chatClient = chatClient;
        _groupsService = groupsService;
        _messageRepo = messageRepo;
        _meetingSessionRepo = meetingSessionRepo;
        _logger = logger;
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
            var response = await _chatClient.CompleteAsync(
                BuildActionItemsPrompt(context.Text),
                cancellationToken: ct);
            var text = response.Message.Text ?? string.Empty;
            var items = ParseActionItems(text);
            var warnings = context.Warnings.ToList();

            if (items.Count == 0)
            {
                warnings.Add("AI did not return any valid action items.");
            }

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

                if (!string.IsNullOrWhiteSpace(meeting?.Summary))
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

    private sealed record GroupAiContext(string Text, IReadOnlyList<string> Warnings);
}
