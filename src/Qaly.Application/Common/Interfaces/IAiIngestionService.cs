namespace Qaly.Application.Common.Interfaces;

public interface IAiIngestionService
{
    /// <summary>
    /// Vectorize and sync a single project.
    /// </summary>
    Task SyncProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Remove a project and all its related items from the vector database.
    /// </summary>
    Task DeleteProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Vectorize and sync a single task.
    /// </summary>
    Task SyncTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Remove a task and its comments from the vector database.
    /// </summary>
    Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Vectorize and sync a single comment.
    /// </summary>
    Task SyncCommentAsync(Guid commentId, CancellationToken ct = default);

    /// <summary>
    /// Remove a comment from the vector database.
    /// </summary>
    Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default);

    /// <summary>
    /// Vectorize and sync a single attachment (metadata and potentially content).
    /// </summary>
    Task SyncAttachmentAsync(Guid attachmentId, CancellationToken ct = default);

    /// <summary>
    /// Remove an attachment from the vector database.
    /// </summary>
    Task DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct = default);

    /// <summary>
    /// Run a full sync of all existing data to the vector database.
    /// </summary>
    Task SyncAllDataAsync(CancellationToken ct = default);
}
