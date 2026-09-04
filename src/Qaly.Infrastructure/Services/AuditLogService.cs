using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int RecentWorkspaceActivityBatchSize = 200;

    private readonly QalyDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(QalyDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default)
    {
        await StageAsync(action, entityType, entityId, changes, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task StageAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default)
    {
        JsonNode? changesNode = changes == null ? null : JsonSerializer.SerializeToNode(changes, JsonOptions);
        var projectId = await InferProjectIdAsync(entityType, entityId, changesNode, ct);
        if (changesNode is JsonObject changesObject && projectId.HasValue && !changesObject.ContainsKey("projectId") && !changesObject.ContainsKey("ProjectId"))
        {
            changesObject["projectId"] = projectId.Value.ToString();
        }

        var log = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ChangesJson = changesNode?.ToJsonString(JsonOptions),
            UserId = _currentUserService.UserId,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _context.AuditLogs.AddAsync(log, ct);
    }

    public async Task<Result<PagedResult<AuditLogDto>>> GetByEntityAsync(string entityType, string entityId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = AuditLogQuery()
            .Where(log => log.EntityType == entityType && log.EntityId == entityId);

        return await PageAsync(query, page, pageSize, ct);
    }

    public async Task<Result<PagedResult<AuditLogDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = AuditLogQuery()
            .Where(log => log.UserId == userId);

        return await PageAsync(query, page, pageSize, ct);
    }

    public async Task<Result<PagedResult<AuditLogDto>>> GetByOrganizationAsync(
        Guid organizationId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<AuditLogDto>>();
        }

        var organization = await _context.Organizations
            .AsNoTracking()
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == organizationId, ct);
        if (organization == null)
        {
            return Result.NotFound<PagedResult<AuditLogDto>>();
        }

        if (!organization.IsActive)
        {
            return Result.Forbidden<PagedResult<AuditLogDto>>();
        }

        var isSystemAdmin = ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);
        var hasAccess = isSystemAdmin ||
            organization.OwnerId == currentUserId ||
            organization.Members.Any(member => member.UserId == currentUserId);
        if (!hasAccess)
        {
            return Result.Forbidden<PagedResult<AuditLogDto>>();
        }

        var organizationEntityId = organizationId.ToString();
        var projectIds = await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId)
            .Select(project => project.Id)
            .ToListAsync(ct);
        var projectEntityIds = projectIds.Select(id => id.ToString()).ToList();

        var groupIds = await _context.WorkGroups
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(group => group.OrganizationId == organizationId)
            .Select(group => group.Id)
            .ToListAsync(ct);
        var groupEntityIds = groupIds.Select(id => id.ToString()).ToList();

        var query = AuditLogQuery().Where(log =>
            (log.EntityType == nameof(Organization) && log.EntityId == organizationEntityId) ||
            (log.EntityType == nameof(Project) && projectEntityIds.Contains(log.EntityId)) ||
            (log.EntityType == nameof(WorkGroup) && groupEntityIds.Contains(log.EntityId)) ||
            (log.ChangesJson != null && log.ChangesJson.Contains(organizationEntityId)));

        return await PageAsync(query, page, pageSize, ct);
    }

    public async Task<Result<PagedResult<AuditLogDto>>> GetRecentWorkspaceActivityAsync(int limit = 10, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<AuditLogDto>>();
        }

        var isAdmin = string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            var recentLogs = await AuditLogQuery()
                .OrderByDescending(log => log.Timestamp)
                .ThenByDescending(log => log.Id)
                .Take(limit)
                .ToListAsync(ct);
            return await PageAsync(recentLogs.AsQueryable(), 1, limit, ct);
        }

        var accessibleLogs = new List<AuditLog>(limit);
        var offset = 0;
        while (accessibleLogs.Count < limit)
        {
            var candidates = await AuditLogQuery()
                .OrderByDescending(log => log.Timestamp)
                .ThenByDescending(log => log.Id)
                .Skip(offset)
                .Take(RecentWorkspaceActivityBatchSize)
                .ToListAsync(ct);
            if (candidates.Count == 0)
            {
                break;
            }

            var visibleIds = await ResolveVisibleLogIdsAsync(candidates, currentUserId.Value, ct);
            foreach (var log in candidates)
            {
                if (visibleIds.Contains(log.Id))
                {
                    accessibleLogs.Add(log);
                }

                if (accessibleLogs.Count == limit)
                {
                    break;
                }
            }

            offset += candidates.Count;
            if (candidates.Count < RecentWorkspaceActivityBatchSize)
            {
                break;
            }
        }

        return Result.Success(new PagedResult<AuditLogDto>
        {
            Items = accessibleLogs.Select(ToDto).ToList(),
            TotalCount = accessibleLogs.Count,
            PageNumber = 1,
            PageSize = limit
        });
    }

    private IQueryable<AuditLog> AuditLogQuery()
        => _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.User);

    private async Task<Guid?> InferProjectIdAsync(string entityType, string entityId, JsonNode? changes, CancellationToken ct)
    {
        if (entityType == nameof(Project) && Guid.TryParse(entityId, out var projectId))
        {
            return projectId;
        }

        if (changes is JsonObject changesObject)
        {
            if (TryReadGuid(changesObject, "projectId", out var parsedProjectId) || TryReadGuid(changesObject, "ProjectId", out parsedProjectId))
            {
                return parsedProjectId;
            }

            if (entityType == nameof(TaskComment) || entityType == nameof(TaskAttachment))
            {
                if (TryReadGuid(changesObject, "taskItemId", out var taskItemId) || TryReadGuid(changesObject, "TaskItemId", out taskItemId))
                {
                    return await _context.TaskItems
                        .AsNoTracking()
                        .Where(task => task.Id == taskItemId)
                        .Select(task => (Guid?)task.ProjectId)
                        .FirstOrDefaultAsync(ct);
                }
            }

            if (entityType == nameof(Sprint))
            {
                if (TryReadGuid(changesObject, "sprintId", out var sprintId) || TryReadGuid(changesObject, "SprintId", out sprintId))
                {
                    return await _context.TaskItems
                        .AsNoTracking()
                        .Where(task => task.SprintId == sprintId)
                        .Select(task => (Guid?)task.ProjectId)
                        .FirstOrDefaultAsync(ct);
                }
            }
        }

        if (entityType == nameof(TaskItem) && Guid.TryParse(entityId, out var taskId))
        {
            return await _context.TaskItems
                .AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => (Guid?)task.ProjectId)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    private async Task<HashSet<long>> ResolveVisibleLogIdsAsync(
        IReadOnlyList<AuditLog> logs,
        Guid currentUserId,
        CancellationToken ct)
    {
        var visibleLogIds = logs
            .Where(log => log.UserId == currentUserId)
            .Select(log => log.Id)
            .ToHashSet();
        var unresolved = logs
            .Where(log => log.UserId != currentUserId)
            .Select(log => new AuditLogTarget(log, TryParseChanges(log.ChangesJson)))
            .ToList();

        var taskIds = new HashSet<Guid>();
        var sprintIds = new HashSet<Guid>();
        var projectByLogId = new Dictionary<long, Guid>();
        foreach (var target in unresolved)
        {
            if (TryGetDirectProjectId(target.Log, target.Changes, out var projectId))
            {
                projectByLogId[target.Log.Id] = projectId;
                continue;
            }

            if (target.Log.EntityType == nameof(TaskItem) && Guid.TryParse(target.Log.EntityId, out var taskId))
            {
                taskIds.Add(taskId);
                continue;
            }

            if ((target.Log.EntityType == nameof(TaskComment) || target.Log.EntityType == nameof(TaskAttachment)) &&
                target.Changes is not null &&
                (TryReadGuid(target.Changes, "taskItemId", out taskId) || TryReadGuid(target.Changes, "TaskItemId", out taskId)))
            {
                taskIds.Add(taskId);
                continue;
            }

            if (target.Log.EntityType == nameof(Sprint) && target.Changes is not null &&
                (TryReadGuid(target.Changes, "sprintId", out var sprintId) || TryReadGuid(target.Changes, "SprintId", out sprintId)))
            {
                sprintIds.Add(sprintId);
            }
        }

        var taskProjects = taskIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await _context.TaskItems
                .AsNoTracking()
                .Where(task => taskIds.Contains(task.Id))
                .Select(task => new { task.Id, task.ProjectId })
                .ToDictionaryAsync(task => task.Id, task => task.ProjectId, ct);
        var sprintProjects = sprintIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await _context.Set<Sprint>()
                .AsNoTracking()
                .Where(sprint => sprintIds.Contains(sprint.Id))
                .Select(sprint => new { sprint.Id, sprint.ProjectId })
                .ToDictionaryAsync(sprint => sprint.Id, sprint => sprint.ProjectId, ct);

        foreach (var target in unresolved.Where(target => !projectByLogId.ContainsKey(target.Log.Id)))
        {
            if (target.Log.EntityType == nameof(TaskItem) && Guid.TryParse(target.Log.EntityId, out var taskId) &&
                taskProjects.TryGetValue(taskId, out var taskProjectId))
            {
                projectByLogId[target.Log.Id] = taskProjectId;
            }
            else if ((target.Log.EntityType == nameof(TaskComment) || target.Log.EntityType == nameof(TaskAttachment)) &&
                target.Changes is not null &&
                (TryReadGuid(target.Changes, "taskItemId", out taskId) || TryReadGuid(target.Changes, "TaskItemId", out taskId)) &&
                taskProjects.TryGetValue(taskId, out taskProjectId))
            {
                projectByLogId[target.Log.Id] = taskProjectId;
            }
            else if (target.Log.EntityType == nameof(Sprint) && target.Changes is not null &&
                (TryReadGuid(target.Changes, "sprintId", out var sprintId) || TryReadGuid(target.Changes, "SprintId", out sprintId)) &&
                sprintProjects.TryGetValue(sprintId, out var sprintProjectId))
            {
                projectByLogId[target.Log.Id] = sprintProjectId;
            }
        }

        var projectIds = projectByLogId.Values.ToHashSet();
        if (projectIds.Count == 0)
        {
            return visibleLogIds;
        }

        var projects = await _context.Projects
            .AsNoTracking()
            .Include(project => project.Organization)
                .ThenInclude(organization => organization!.Members)
            .Include(project => project.Members)
            .Where(project => projectIds.Contains(project.Id))
            .ToListAsync(ct);
        var accessibleProjectIds = projects
            .Where(project => CanAccessProject(project, currentUserId))
            .Select(project => project.Id)
            .ToHashSet();

        foreach (var (logId, projectId) in projectByLogId)
        {
            if (accessibleProjectIds.Contains(projectId))
            {
                visibleLogIds.Add(logId);
            }
        }

        return visibleLogIds;
    }

    private static bool TryGetDirectProjectId(AuditLog log, JsonObject? changes, out Guid projectId)
    {
        if (log.EntityType == nameof(Project) && Guid.TryParse(log.EntityId, out projectId))
        {
            return true;
        }

        if (changes is not null &&
            (TryReadGuid(changes, "projectId", out projectId) || TryReadGuid(changes, "ProjectId", out projectId)))
        {
            return true;
        }

        projectId = Guid.Empty;
        return false;
    }

    private static JsonObject? TryParseChanges(string? changesJson)
    {
        if (string.IsNullOrWhiteSpace(changesJson)) return null;
        try
        {
            return JsonNode.Parse(changesJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool CanAccessProject(Project project, Guid currentUserId)
    {
        if (!project.OrganizationId.HasValue)
        {
            return project.OwnerId == currentUserId ||
                project.Members.Any(member => member.UserId == currentUserId);
        }

        if (project.Organization == null || !project.Organization.IsActive)
        {
            return false;
        }

        if (project.Organization.OwnerId == currentUserId)
        {
            return true;
        }

        var organizationRole = project.Organization.Members
            .Where(member => member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefault();
        return !string.IsNullOrWhiteSpace(organizationRole) &&
            (OrganizationRoleRules.CanManageOrganization(organizationRole) ||
             project.OwnerId == currentUserId ||
             project.Members.Any(member => member.UserId == currentUserId));
    }

    private sealed record AuditLogTarget(AuditLog Log, JsonObject? Changes);

    private static bool TryReadGuid(JsonObject obj, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue valueNode)
        {
            return false;
        }

        return valueNode.TryGetValue<string>(out var raw) && Guid.TryParse(raw, out value);
    }

    private static AuditLogDto ToDto(AuditLog log)
        => new(
            log.Id,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.ChangesJson,
            log.UserId,
            log.User?.FullName,
            log.IpAddress,
            log.Timestamp);

    private static async Task<Result<PagedResult<AuditLogDto>>> PageAsync(IQueryable<AuditLog> query, int page, int pageSize, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(log => log.Timestamp)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AuditLogDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }
}
