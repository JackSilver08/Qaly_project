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
    private const int RecentWorkspaceActivityScanLimit = 500;

    private readonly QalyDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(QalyDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default)
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
        await _context.SaveChangesAsync(ct);
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

    public async Task<Result<PagedResult<AuditLogDto>>> GetRecentWorkspaceActivityAsync(int limit = 10, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<AuditLogDto>>();
        }

        var isAdmin = string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        var recentLogs = await AuditLogQuery()
            .OrderByDescending(log => log.Timestamp)
            .Take(isAdmin ? limit : RecentWorkspaceActivityScanLimit)
            .ToListAsync(ct);

        if (!isAdmin)
        {
            var accessibleLogs = new List<AuditLog>(limit);
            foreach (var log in recentLogs)
            {
                if (await CanSeeRecentLogAsync(log, currentUserId.Value, ct))
                {
                    accessibleLogs.Add(log);
                }

                if (accessibleLogs.Count == limit)
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

        return await PageAsync(recentLogs.AsQueryable(), 1, limit, ct);
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

    private async Task<bool> CanSeeRecentLogAsync(AuditLog log, Guid currentUserId, CancellationToken ct)
    {
        if (log.UserId == currentUserId)
        {
            return true;
        }

        var projectId = await InferProjectIdAsync(log.EntityType, log.EntityId, log.ChangesJson is null ? null : JsonNode.Parse(log.ChangesJson), ct);
        if (!projectId.HasValue)
        {
            return false;
        }

        return await CanAccessProjectAsync(projectId.Value, currentUserId, ct);
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid currentUserId, CancellationToken ct)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .Include(item => item.Organization)
                .ThenInclude(item => item!.Members)
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);

        if (project == null)
        {
            return false;
        }

        if (project.OwnerId == currentUserId)
        {
            return true;
        }

        if (project.Members.Any(member => member.UserId == currentUserId))
        {
            return true;
        }

        if (project.OrganizationId == null || project.Organization == null)
        {
            return false;
        }

        if (!project.Organization.IsActive)
        {
            return false;
        }

        if (project.Organization.OwnerId == currentUserId)
        {
            return true;
        }

        return project.Organization.Members.Any(member => member.UserId == currentUserId);
    }

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
