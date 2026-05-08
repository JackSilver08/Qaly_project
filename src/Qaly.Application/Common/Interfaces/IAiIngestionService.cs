namespace Qaly.Application.Common.Interfaces;

public interface IAiIngestionService
{
    /// <summary>
    /// Vectorize and sync a single project.
    /// </summary>
    Task SyncProjectAsync(Guid projectId);

    /// <summary>
    /// Remove a project and all its related items from the vector database.
    /// </summary>
    Task DeleteProjectAsync(Guid projectId);

    /// <summary>
    /// Vectorize and sync a single task.
    /// </summary>
    Task SyncTaskAsync(Guid taskId);

    /// <summary>
    /// Remove a task and its comments from the vector database.
    /// </summary>
    Task DeleteTaskAsync(Guid taskId);

    /// <summary>
    /// Vectorize and sync a single comment.
    /// </summary>
    Task SyncCommentAsync(Guid commentId);

    /// <summary>
    /// Remove a comment from the vector database.
    /// </summary>
    Task DeleteCommentAsync(Guid commentId);

    /// <summary>
    /// Run a full sync of all existing data to the vector database.
    /// </summary>
    Task SyncAllDataAsync();
}
