using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

/// <summary>
/// Background worker định kỳ quét DB (Visibility Health Check):
/// Kiểm tra xem có User nào có record ProjectMember nhưng 0 project visible do lỗi join query/permission.
/// Phát hiện tự động và cảnh báo ngay cho System Admin trước khi người dùng phát hiện ra bug.
/// </summary>
public class ProjectVisibilityHealthCheckWorker : BackgroundService
{
    private static readonly Action<ILogger, Exception?> WorkerStarted =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(WorkerStarted)), "ProjectVisibilityHealthCheckWorker started.");
    private static readonly Action<ILogger, Exception?> WorkerFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(WorkerFailed)), "Error occurred during ProjectVisibilityHealthCheckWorker execution.");
    private static readonly Action<ILogger, int, Exception?> OrphanedMembersFound =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(3, nameof(OrphanedMembersFound)), "[Visibility Alert] Found {Count} users with orphaned ProjectMember records for deleted/inaccessible projects.");
    private static readonly Action<ILogger, Exception?> VisibilityHealthy =
        LoggerMessage.Define(LogLevel.Information, new EventId(4, nameof(VisibilityHealthy)), "[Visibility Health Check] All ProjectMember visibility constraints OK.");

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectVisibilityHealthCheckWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public ProjectVisibilityHealthCheckWorker(
        IServiceProvider serviceProvider,
        ILogger<ProjectVisibilityHealthCheckWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerStarted(_logger, null);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                WorkerFailed(_logger, ex);
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task RunHealthCheckAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        // Tìm danh sách UserIds có trong ProjectMember nhưng project đó bị inactive hoặc query không thấy
        var orphanedMemberUserIds = await dbContext.ProjectMembers
            .AsNoTracking()
            .Where(m => !dbContext.Projects.Any(p => p.Id == m.ProjectId && !p.IsDeleted))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (orphanedMemberUserIds.Count > 0)
        {
            OrphanedMembersFound(_logger, orphanedMemberUserIds.Count, null);

            // Audit log warning
            var auditLog = new AuditLog
            {
                Action = "VisibilityHealthCheckAlert",
                EntityType = "ProjectMember",
                EntityId = "SystemHealthCheck",
                ChangesJson = $"Detected {orphanedMemberUserIds.Count} orphaned members. UserIds: {string.Join(',', orphanedMemberUserIds.Take(10))}",
                Timestamp = DateTimeOffset.UtcNow
            };
            await dbContext.AuditLogs.AddAsync(auditLog, ct);
            await dbContext.SaveChangesAsync(ct);
        }
        else
        {
            VisibilityHealthy(_logger, null);
        }
    }
}
