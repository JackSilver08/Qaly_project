using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public partial class ProjectTrashCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectTrashCleanupWorker> _logger;

    public ProjectTrashCleanupWorker(IServiceProvider serviceProvider, ILogger<ProjectTrashCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            LogScanStarting();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
                var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                var limitTime = DateTimeOffset.UtcNow.AddDays(-30);
                var expiredProjects = await dbContext.Projects
                    .IgnoreQueryFilters()
                    .Where(project => project.IsDeleted && project.DeletedAt < limitTime)
                    .ToListAsync(stoppingToken);

                foreach (var project in expiredProjects)
                {
                    LogHardDeletingProject(project.Name, project.Id, project.DeletedAt);

                    await HardDeleteProjectAttachmentsAsync(dbContext, fileStorage, project.Id, stoppingToken);

                    dbContext.Projects.Remove(project);
                }

                if (expiredProjects.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    LogCleanupCompleted(expiredProjects.Count);
                }
                else
                {
                    LogNoExpiredProjects();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCleanupFailed(ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task HardDeleteProjectAttachmentsAsync(
        QalyDbContext dbContext,
        IFileStorageService fileStorage,
        Guid projectId,
        CancellationToken ct)
    {
        var attachments = await GetProjectAttachmentsForHardDeleteAsync(dbContext, projectId, ct);
        var releaseCounts = attachments
            .Where(attachment => attachment.PhysicalFileId != Guid.Empty)
            .GroupBy(attachment => attachment.PhysicalFileId)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var attachment in attachments)
        {
            dbContext.TaskAttachments.Remove(attachment);
        }

        foreach (var (physicalFileId, releaseCount) in releaseCounts)
        {
            var physicalFile = await dbContext.PhysicalFiles
                .FirstOrDefaultAsync(file => file.Id == physicalFileId, ct);

            if (physicalFile == null)
            {
                continue;
            }

            physicalFile.ReferenceCount -= releaseCount;
            if (physicalFile.ReferenceCount > 0)
            {
                dbContext.PhysicalFiles.Update(physicalFile);
                continue;
            }

            try
            {
                await fileStorage.DeleteAsync(physicalFile.FilePath, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogPhysicalFileDeleteFailed(ex, physicalFile.Id, physicalFile.FilePath);
                // Keep both the physical-file metadata and the trashed Project so a later
                // cleanup pass can retry. Deleting the canonical row here would turn a
                // transient storage failure into an untracked orphan that cannot recover.
                throw;
            }

            dbContext.PhysicalFiles.Remove(physicalFile);
        }
    }

    private static async Task<IReadOnlyList<TaskAttachment>> GetProjectAttachmentsForHardDeleteAsync(
        QalyDbContext dbContext,
        Guid projectId,
        CancellationToken ct)
        => await dbContext.TaskAttachments
            .IgnoreQueryFilters()
            .Include(attachment => attachment.PhysicalFile)
            .Where(attachment =>
                attachment.ProjectId == projectId ||
                (attachment.TaskItem != null && attachment.TaskItem.ProjectId == projectId) ||
                (attachment.Comment != null && attachment.Comment.TaskItem.ProjectId == projectId))
            .ToListAsync(ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting project trash cleanup scan for soft-deleted projects older than 30 days.")]
    private partial void LogScanStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Hard-deleting expired project {ProjectName} ({ProjectId}), deleted at {DeletedAt}.")]
    private partial void LogHardDeletingProject(string projectName, Guid projectId, DateTimeOffset? deletedAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed cleanup for {ProjectCount} expired trashed projects.")]
    private partial void LogCleanupCompleted(int projectCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "No expired trashed projects found.")]
    private partial void LogNoExpiredProjects();

    [LoggerMessage(Level = LogLevel.Error, Message = "Project trash cleanup failed.")]
    private partial void LogCleanupFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete physical file {PhysicalFileId} at {FilePath}.")]
    private partial void LogPhysicalFileDeleteFailed(Exception ex, Guid physicalFileId, string filePath);
}
