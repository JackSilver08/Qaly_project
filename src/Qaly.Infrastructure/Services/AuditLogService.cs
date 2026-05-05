using System.Text.Json;
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

    private readonly QalyDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(QalyDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ChangesJson = changes == null ? null : JsonSerializer.Serialize(changes, JsonOptions),
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

    private IQueryable<AuditLog> AuditLogQuery()
        => _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.User);

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
