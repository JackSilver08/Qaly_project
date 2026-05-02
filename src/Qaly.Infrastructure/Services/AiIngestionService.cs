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

    private const string CollectionName = "qaly_knowledge_base";
    private const int VectorSize = 768; // nomic-embed-text size

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
        await UpsertToVectorDb(project.Id, "Project", project.Name, text);
    }

    public async Task SyncTaskAsync(Guid taskId)
    {
        var task = await _taskRepo.GetQueryable()
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
            
        if (task == null) return;

        var text = $"Công việc: {task.Title} (trong dự án {task.Project.Name}). Mô tả: {task.Description}. Trạng thái: {task.Status}. Độ ưu tiên: {task.Priority}. Hạn chót: {task.DueDate}.";
        await UpsertToVectorDb(task.Id, "Task", task.Title, text);
    }

    public async Task SyncCommentAsync(Guid commentId)
    {
        var comment = await _commentRepo.GetQueryable()
            .Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == commentId);
            
        if (comment == null) return;

        var text = $"Bình luận về công việc '{comment.TaskItem.Title}': {comment.Content}.";
        await UpsertToVectorDb(comment.Id, "Comment", comment.TaskItem.Title, text);
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

    private async Task UpsertToVectorDb(Guid id, string type, string title, string content)
    {
        try 
        {
            var embeddings = await _embeddingGenerator.GenerateAsync(new[] { content });
            var vector = embeddings[0].Vector.ToArray();

            var payload = new Dictionary<string, object>
            {
                { "Type", type },
                { "Title", title },
                { "Content", content },
                { "LastUpdated", DateTime.UtcNow.ToString("O") }
            };

            await _vectorStorage.UpsertAsync(id, vector, payload, CollectionName);
        }
        catch (Exception)
        {
            // Log error or handle gracefully if Ollama is offline
        }
    }
}
