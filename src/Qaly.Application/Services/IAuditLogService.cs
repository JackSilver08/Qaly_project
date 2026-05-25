using Qaly.Application.Common.Models;

namespace Qaly.Application.Services;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default);
    Task<Result<PagedResult<AuditLogDto>>> GetByEntityAsync(string entityType, string entityId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result<PagedResult<AuditLogDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result<PagedResult<AuditLogDto>>> GetRecentWorkspaceActivityAsync(int limit = 10, CancellationToken ct = default);
}

public record AuditLogDto(
    long Id,
    string Action,
    string EntityType,
    string EntityId,
    string? ChangesJson,
    Guid? UserId,
    string? UserName,
    string? IpAddress,
    DateTimeOffset Timestamp);
