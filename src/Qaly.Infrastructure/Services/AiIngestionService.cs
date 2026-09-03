using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Infrastructure.Services;

public class AiIngestionService : IAiIngestionService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskComment> _commentRepo;
    private readonly IRepository<TaskAttachment> _attachmentRepo;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStorageService _vectorStorage;

    private const string CollectionName = "qaly_context";
    private readonly int _vectorSize;

    public AiIngestionService(
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskComment> commentRepo,
        IRepository<TaskAttachment> attachmentRepo,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStorageService vectorStorage,
        IConfiguration configuration)
    {
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _commentRepo = commentRepo;
        _attachmentRepo = attachmentRepo;
        _embeddingGenerator = embeddingGenerator;
        _vectorStorage = vectorStorage;
        _vectorSize = configuration.GetValue<int>("Ai:VectorSize", 768);
    }

    public async Task SyncProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return;

        var text = $"Dự án: {project.Name}. Mô tả: {project.Description}. Trạng thái: {project.Status}.";
        
        var metadata = new Dictionary<string, object>
        {
            { "project_id", project.Id },
            { "owner_id", project.OwnerId },
            { "is_private", false },
            { "visibility", "member" },
            { "content_type", "project" },
            { "created_at", project.CreatedAt.ToString("O") }
        };

        await UpsertToVectorDb(project.Id, project.Name, text, metadata, ct);
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        // Delete project entry
        await _vectorStorage.DeleteAsync(projectId, CollectionName, ct);
        
        // Delete all items related to this project
        await _vectorStorage.DeleteByFilterAsync(new VectorFilter { ProjectId = projectId }, CollectionName, ct);
    }

    public async Task SyncTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
            
        if (task == null) return;

        var text = $"Công việc: {task.Title} (trong dự án {task.Project.Name}). Mô tả: {task.Description}. Trạng thái: {task.Status}. Độ ưu tiên: {task.Priority}. Hạn chót: {task.DueDate}.";
        
        var metadata = new Dictionary<string, object>
        {
            { "project_id", task.ProjectId },
            { "task_id", task.Id },
            { "owner_id", task.ReporterId },
            { "is_private", task.IsPrivate },
            { "visibility", task.IsPrivate ? "private" : "member" },
            { "content_type", "task" },
            { "created_at", task.CreatedAt.ToString("O") }
        };

        await UpsertToVectorDb(task.Id, task.Title, text, metadata, ct);
    }

    public async Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        await _vectorStorage.DeleteAsync(taskId, CollectionName, ct);
        await _vectorStorage.DeleteByFilterAsync(new VectorFilter { TaskId = taskId }, CollectionName, ct);
    }

    public async Task SyncCommentAsync(Guid commentId, CancellationToken ct = default)
    {
        var comment = await _commentRepo.GetQueryable()
            .Include(c => c.TaskItem)
            .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
            
        if (comment == null) return;

        var text = $"Bình luận về công việc '{comment.TaskItem.Title}': {comment.Content}.";
        
        var metadata = new Dictionary<string, object>
        {
            { "project_id", comment.TaskItem.ProjectId },
            { "task_id", comment.TaskItemId },
            { "owner_id", comment.AuthorId },
            { "is_private", comment.TaskItem.IsPrivate },
            { "visibility", comment.TaskItem.IsPrivate ? "private" : "member" },
            { "content_type", "comment" },
            { "created_at", comment.CreatedAt.ToString("O") }
        };

        await UpsertToVectorDb(comment.Id, comment.TaskItem.Title, text, metadata, ct);
    }

    public async Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default)
    {
        await _vectorStorage.DeleteAsync(commentId, CollectionName, ct);
    }

    public async Task SyncAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _attachmentRepo.GetQueryable()
            .Include(a => a.TaskItem)
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
            
        if (attachment == null) return;

        var projectId = attachment.ProjectId ?? attachment.TaskItem?.ProjectId;
        if (projectId == null) return;

        var isPrivate = attachment.TaskItem?.IsPrivate ?? false;
        var text = $"Tệp đính kèm: {attachment.FileName}. Loại: {attachment.ContentType}. Phạm vi: {attachment.Scope}.";
        
        var metadata = new Dictionary<string, object>
        {
            { "project_id", projectId.Value },
            { "task_id", attachment.TaskItemId ?? Guid.Empty },
            { "owner_id", attachment.UploadedById },
            { "is_private", isPrivate },
            { "visibility", isPrivate ? "private" : "member" },
            { "content_type", "attachment" },
            { "created_at", attachment.CreatedAt.ToString("O") }
        };

        await UpsertToVectorDb(attachment.Id, attachment.FileName, text, metadata, ct);
    }

    public async Task DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        await _vectorStorage.DeleteAsync(attachmentId, CollectionName, ct);
    }

    public async Task SyncAllDataAsync(CancellationToken ct = default)
    {
        await _vectorStorage.EnsureCollectionExistsAsync(CollectionName, (ulong)_vectorSize, ct);

        var projects = await _projectRepo.GetAllAsync(ct);
        foreach (var p in projects) await SyncProjectAsync(p.Id, ct);

        var tasks = await _taskRepo.GetAllAsync(ct);
        foreach (var t in tasks) await SyncTaskAsync(t.Id, ct);

        var comments = await _commentRepo.GetAllAsync(ct);
        foreach (var c in comments) await SyncCommentAsync(c.Id, ct);

        var attachments = await _attachmentRepo.GetAllAsync(ct);
        foreach (var a in attachments) await SyncAttachmentAsync(a.Id, ct);
    }

    private async Task UpsertToVectorDb(
        Guid id,
        string title,
        string content,
        Dictionary<string, object> metadata,
        CancellationToken ct)
    {
        var embeddings = await _embeddingGenerator.GenerateAsync([content], cancellationToken: ct);
        var vector = embeddings[0].Vector.ToArray();

        metadata["Title"] = title;
        metadata["Content"] = content;
        metadata["LastUpdated"] = DateTime.UtcNow.ToString("O");

        // Propagate provider/storage failures. The outbox worker must see the error
        // so it can retry, and the administrative full-sync endpoint must not return
        // a false success when no canonical vector record was written.
        await _vectorStorage.UpsertAsync(id, vector, metadata, CollectionName, ct);
    }
}
