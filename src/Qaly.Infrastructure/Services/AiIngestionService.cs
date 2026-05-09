using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Infrastructure.Services;

public class AiIngestionService : IAiIngestionService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskComment> _commentRepo;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStorageService _vectorStorage;

    private const string CollectionName = "qaly_context";
    private const int VectorSize = 768;

    public AiIngestionService(
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskComment> commentRepo,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStorageService vectorStorage)
    {
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _commentRepo = commentRepo;
        _embeddingGenerator = embeddingGenerator;
        _vectorStorage = vectorStorage;
    }

    public async Task SyncProjectAsync(Guid projectId)
    {
        var project = await _projectRepo.GetByIdAsync(projectId);
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

        await UpsertToVectorDb(project.Id, project.Name, text, metadata);
    }

    public async Task DeleteProjectAsync(Guid projectId)
    {
        // Delete project entry
        await _vectorStorage.DeleteAsync(projectId, CollectionName);
        
        // Delete all items related to this project
        await _vectorStorage.DeleteByFilterAsync(new VectorFilter { ProjectId = projectId }, CollectionName);
    }

    public async Task SyncTaskAsync(Guid taskId)
    {
        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
            
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

        await UpsertToVectorDb(task.Id, task.Title, text, metadata);
    }

    public async Task DeleteTaskAsync(Guid taskId)
    {
        await _vectorStorage.DeleteAsync(taskId, CollectionName);
        await _vectorStorage.DeleteByFilterAsync(new VectorFilter { TaskId = taskId }, CollectionName);
    }

    public async Task SyncCommentAsync(Guid commentId)
    {
        var comment = await _commentRepo.GetQueryable()
            .Include(c => c.TaskItem)
            .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(c => c.Id == commentId);
            
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

        await UpsertToVectorDb(comment.Id, comment.TaskItem.Title, text, metadata);
    }

    public async Task DeleteCommentAsync(Guid commentId)
    {
        await _vectorStorage.DeleteAsync(commentId, CollectionName);
    }

    public async Task SyncAllDataAsync()
    {
        await _vectorStorage.EnsureCollectionExistsAsync(CollectionName, (ulong)VectorSize);

        var projects = await _projectRepo.GetAllAsync();
        foreach (var p in projects) await SyncProjectAsync(p.Id);

        var tasks = await _taskRepo.GetAllAsync();
        foreach (var t in tasks) await SyncTaskAsync(t.Id);

        var comments = await _commentRepo.GetAllAsync();
        foreach (var c in comments) await SyncCommentAsync(c.Id);
    }

    private async Task UpsertToVectorDb(Guid id, string title, string content, Dictionary<string, object> metadata)
    {
        try 
        {
            var embeddings = await _embeddingGenerator.GenerateAsync(new[] { content });
            var vector = embeddings[0].Vector.ToArray();

            metadata["Title"] = title;
            metadata["Content"] = content;
            metadata["LastUpdated"] = DateTime.UtcNow.ToString("O");

            await _vectorStorage.UpsertAsync(id, vector, metadata, CollectionName);
        }
        catch (Exception)
        {
            // Log error
        }
    }
}
