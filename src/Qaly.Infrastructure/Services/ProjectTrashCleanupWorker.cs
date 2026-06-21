using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public class ProjectTrashCleanupWorker : BackgroundService
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
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Starting project trash cleanup scan for soft-deleted projects older than 30 days.");

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
                    _logger.LogInformation(
                        "Hard-deleting expired project {ProjectName} ({ProjectId}), deleted at {DeletedAt}.",
                        project.Name,
                        project.Id,
                        project.DeletedAt);

                    await HardDeleteProjectAttachmentsAsync(dbContext, fileStorage, project.Id, stoppingToken);

                    dbContext.Projects.Remove(project);
                }

                if (expiredProjects.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Completed cleanup for {ProjectCount} expired trashed projects.", expiredProjects.Count);
                }
                else
                {
                    _logger.LogInformation("No expired trashed projects found.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project trash cleanup failed.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task HardDeleteProjectAttachmentsAsync(
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete physical file {PhysicalFileId} at {FilePath}.", physicalFile.Id, physicalFile.FilePath);
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
}
