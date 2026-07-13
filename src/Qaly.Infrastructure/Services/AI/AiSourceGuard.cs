using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiSourceGuard : IAiSourceGuard
{
    private readonly QalyDbContext _db;

    public AiSourceGuard(QalyDbContext db)
    {
        _db = db;
    }

    public async Task<AiSourceGuardResult> ValidateAsync(
        Guid projectId,
        Guid userId,
        IReadOnlyList<AiJobSourceInputDto> sources,
        bool enforceFreshness,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project == null || !await CanAccessProjectAsync(project, userId, cancellationToken))
        {
            return Denied();
        }

        foreach (var source in sources)
        {
            var validation = await ValidateSourceAsync(project, userId, source, enforceFreshness, cancellationToken);
            if (!validation.IsAllowed) return validation;
        }

        return new AiSourceGuardResult(true);
    }

    private async Task<AiSourceGuardResult> ValidateSourceAsync(
        Project project,
        Guid userId,
        AiJobSourceInputDto source,
        bool enforceFreshness,
        CancellationToken ct)
    {
        var sourceType = NormalizeType(source.SourceType);
        if (!source.SourceEntityId.HasValue)
        {
            return sourceType is "manual" or "manualtext" or "text" or "legacy"
                ? new AiSourceGuardResult(true)
                : Denied();
        }

        var sourceId = source.SourceEntityId.Value;
        SourceState? state = sourceType switch
        {
            "project" => await GetProjectStateAsync(project, sourceId),
            "group" or "workgroup" => await GetGroupStateAsync(project, userId, sourceId, ct),
            "sprint" => await GetSprintStateAsync(project.Id, sourceId, ct),
            "task" or "taskitem" => await GetTaskStateAsync(project.Id, userId, sourceId, ct),
            "wiki" or "wikipage" => await GetWikiStateAsync(project, userId, sourceId, ct),
            "meeting" or "meetingimport" => await GetMeetingImportStateAsync(project.Id, sourceId, ct),
            "message" or "groupmessage" => await GetMessageStateAsync(project, userId, sourceId, ct),
            "groupmeetingsession" or "meetingsession" => await GetMeetingSessionStateAsync(project, userId, sourceId, ct),
            "actionitem" or "meetingactionitem" => await GetActionItemStateAsync(project.Id, sourceId, ct),
            _ => null
        };

        if (state == null) return Denied();
        if (!enforceFreshness || string.Equals(source.SourceVersion, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            return new AiSourceGuardResult(true);
        }

        if (!string.IsNullOrWhiteSpace(source.SourceHash) &&
            !string.Equals(source.SourceHash, state.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return Stale();
        }

        if (!string.IsNullOrWhiteSpace(source.SourceVersion) &&
            !state.Versions.Contains(source.SourceVersion, StringComparer.Ordinal))
        {
            return Stale();
        }

        return new AiSourceGuardResult(true);
    }

    private static Task<SourceState?> GetProjectStateAsync(Project project, Guid sourceId)
        => Task.FromResult(project.Id == sourceId
            ? CreateState(project.UpdatedAt, $"{project.Id}|{project.Name}|{project.Description}|{project.Status}|{project.UpdatedAt:O}")
            : null);

    private async Task<SourceState?> GetGroupStateAsync(Project project, Guid userId, Guid sourceId, CancellationToken ct)
    {
        if (project.SourceGroupId != sourceId || !await CanAccessGroupAsync(sourceId, userId, ct)) return null;
        var group = await _db.WorkGroups.AsNoTracking().FirstOrDefaultAsync(item => item.Id == sourceId, ct);
        return group == null
            ? null
            : CreateState(group.UpdatedAt, $"{group.Id}|{group.Name}|{group.Status}|{group.UpdatedAt:O}");
    }

    private async Task<SourceState?> GetSprintStateAsync(Guid projectId, Guid sourceId, CancellationToken ct)
    {
        var sprint = await _db.Set<Sprint>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.ProjectId == projectId, ct);
        return sprint == null
            ? null
            : CreateState(sprint.UpdatedAt, $"{sprint.Id}|{sprint.Name}|{sprint.Status}|{sprint.Goal}|{sprint.StartDate:O}|{sprint.EndDate:O}|{sprint.UpdatedAt:O}");
    }

    private async Task<SourceState?> GetTaskStateAsync(Guid projectId, Guid userId, Guid sourceId, CancellationToken ct)
    {
        var task = await _db.TaskItems
            .AsNoTracking()
            .Include(item => item.Assignees)
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.ProjectId == projectId, ct);
        if (task == null) return null;
        if (task.IsPrivate && task.ReporterId != userId && task.AssigneeId != userId &&
            !task.Assignees.Any(item => item.UserId == userId) && !await CanManageProjectAsync(projectId, userId, ct))
        {
            return null;
        }

        var rowVersion = task.RowVersion.Length == 0 ? null : Convert.ToBase64String(task.RowVersion);
        return CreateState(task.UpdatedAt, $"{task.Id}|{task.Title}|{task.Description}|{task.Status}|{task.Priority}|{task.DueDate:O}|{task.UpdatedAt:O}", rowVersion);
    }

    private async Task<SourceState?> GetWikiStateAsync(Project project, Guid userId, Guid sourceId, CancellationToken ct)
    {
        var page = await _db.WikiPages.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.ProjectId == project.Id, ct);
        if (page == null) return null;
        if (string.Equals(page.Visibility, "private", StringComparison.OrdinalIgnoreCase) &&
            page.AuthorId != userId && project.OwnerId != userId && !await CanManageProjectAsync(project.Id, userId, ct))
        {
            return null;
        }

        return CreateState(page.UpdatedAt, $"{page.Id}|{page.Title}|{page.Content}|{page.Visibility}|{page.UpdatedAt:O}");
    }

    private async Task<SourceState?> GetMeetingImportStateAsync(Guid projectId, Guid sourceId, CancellationToken ct)
    {
        var meeting = await _db.MeetingImports.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.ProjectId == projectId, ct);
        return meeting == null
            ? null
            : CreateState(meeting.UpdatedAt, $"{meeting.Id}|{meeting.SourceHash}|{meeting.Title}|{meeting.Summary}|{meeting.TranscriptText}|{meeting.UpdatedAt:O}");
    }

    private async Task<SourceState?> GetMessageStateAsync(Project project, Guid userId, Guid sourceId, CancellationToken ct)
    {
        if (!project.SourceGroupId.HasValue || !await CanAccessGroupAsync(project.SourceGroupId.Value, userId, ct)) return null;
        var message = await _db.GroupMessages.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.WorkGroupId == project.SourceGroupId.Value, ct);
        return message == null
            ? null
            : CreateState(message.UpdatedAt, $"{message.Id}|{message.Content}|{message.EditedAt:O}|{message.IsDeleted}|{message.UpdatedAt:O}");
    }

    private async Task<SourceState?> GetMeetingSessionStateAsync(Project project, Guid userId, Guid sourceId, CancellationToken ct)
    {
        if (!project.SourceGroupId.HasValue || !await CanAccessGroupAsync(project.SourceGroupId.Value, userId, ct)) return null;
        var meeting = await _db.GroupMeetingSessions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.WorkGroupId == project.SourceGroupId.Value, ct);
        return meeting == null
            ? null
            : CreateState(meeting.UpdatedAt, $"{meeting.Id}|{meeting.Status}|{meeting.TranscriptSourceId}|{meeting.Summary}|{meeting.UpdatedAt:O}");
    }

    private async Task<SourceState?> GetActionItemStateAsync(Guid projectId, Guid sourceId, CancellationToken ct)
    {
        var item = await _db.MeetingActionItemMappings.AsNoTracking()
            .Include(mapping => mapping.MeetingImport)
            .FirstOrDefaultAsync(mapping => mapping.Id == sourceId && mapping.MeetingImport.ProjectId == projectId, ct);
        return item == null
            ? null
            : CreateState(item.UpdatedAt, $"{item.Id}|{item.Status}|{item.SourceTitle}|{item.SourcePriority}|{item.SourceDueDate:O}|{item.SourceQuote}|{item.UpdatedAt:O}");
    }

    private async Task<bool> CanAccessProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId || await IsAdminAsync(userId, ct)) return true;
        if (await _db.ProjectMembers.AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct)) return true;
        if (!project.OrganizationId.HasValue) return false;
        if (project.Organization?.OwnerId == userId) return true;
        return await _db.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId,
            ct);
    }

    private async Task<bool> CanManageProjectAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        if (await IsAdminAsync(userId, ct)) return true;
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId, ct);
        if (project == null) return false;
        if (project.OwnerId == userId) return true;
        var role = await _db.ProjectMembers.Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return ProjectRoleRules.CanManageProject(role);
    }

    private async Task<bool> CanAccessGroupAsync(Guid groupId, Guid userId, CancellationToken ct)
        => await _db.WorkGroups.AnyAsync(group => group.Id == groupId && group.OwnerId == userId, ct) ||
           await _db.WorkGroupMembers.AnyAsync(member => member.WorkGroupId == groupId && member.UserId == userId, ct);

    private async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct)
        => await _db.Users.AnyAsync(user => user.Id == userId && user.Role == "Admin", ct);

    private static SourceState CreateState(DateTimeOffset? updatedAt, string hashInput, string? additionalVersion = null)
    {
        var versions = new List<string>();
        if (updatedAt.HasValue)
        {
            versions.Add(updatedAt.Value.ToString("O"));
            versions.Add(updatedAt.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(additionalVersion)) versions.Add(additionalVersion);
        return new SourceState(Hash(hashInput), versions);
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string NormalizeType(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static AiSourceGuardResult Denied()
        => new(false, AiErrorCodes.PermissionDenied, "The source is absent or not visible to the requester.");

    private static AiSourceGuardResult Stale()
        => new(false, AiErrorCodes.SourceStale, "The source changed after the AI request was created.");

    private sealed record SourceState(string Hash, IReadOnlyList<string> Versions);
}
