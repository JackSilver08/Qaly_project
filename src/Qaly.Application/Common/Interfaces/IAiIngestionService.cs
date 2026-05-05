namespace Qaly.Application.Common.Interfaces;

public interface IAiIngestionService
{
    /// <summary>
    /// Vectorize and sync a single project.
    /// </summary>
    Task SyncProjectAsync(Guid projectId);

    /// <summary>
    /// Vectorize and sync a single task.
    /// </summary>
    Task SyncTaskAsync(Guid taskId);

    /// <summary>
    /// Vectorize and sync a single comment.
    /// </summary>
    Task SyncCommentAsync(Guid commentId);

    /// <summary>
    /// Run a full sync of all existing data to the vector database.
    /// </summary>
    Task SyncAllDataAsync();
}
