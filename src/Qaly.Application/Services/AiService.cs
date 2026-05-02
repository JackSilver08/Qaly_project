using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class AiService : IAiService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskComment> _commentRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;

    public AiService(
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskComment> commentRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<User> userRepo)
    {
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _commentRepo = commentRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
    }

    public Task<string> SuggestTaskPriorityAsync(string taskTitle, string taskDescription, string projectContext)
    {
        var combined = $"{taskTitle} {taskDescription} {projectContext}".ToLowerInvariant();
        var score = 0;

        score += CountAny(combined, "production", "security", "data loss", "down", "blocked", "urgent", "critical") * 3;
        score += CountAny(combined, "bug", "deadline", "overdue", "risk", "customer", "payment", "login") * 2;
        score -= CountAny(combined, "polish", "copy", "nice to have", "optional", "cleanup");

        var priority = score switch
        {
            >= 5 => "Critical",
            >= 3 => "High",
            <= -1 => "Low",
            _ => "Medium"
        };

        return Task.FromResult($"{priority} - Suggested from task wording, urgency terms, and project context.");
    }

    public async Task<string> GenerateProjectSummaryAsync(Guid projectId)
    {
        var project = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Tasks)
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == projectId);

        if (project == null)
        {
            return "Project was not found.";
        }

        var total = project.Tasks.Count;
        var done = project.Tasks.Count(IsDone);
        var overdue = project.Tasks.Count(IsOverdue);
        var inProgress = project.Tasks.Count(task => IsStatus(task, "InProgress"));
        var progress = total == 0 ? 0 : Math.Round(done * 100d / total);

        return $"{project.Name} is {progress:0}% complete with {done}/{total} tasks done. " +
               $"{inProgress} tasks are in progress and {overdue} tasks are overdue. " +
               $"The project has {project.Members.Count + 1} people including the owner.";
    }

    public async Task<string> AnalyzeProjectRisksAsync(Guid projectId)
    {
        var tasks = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .ToListAsync();

        if (tasks.Count == 0)
        {
            return "No delivery risks yet because the project has no tasks.";
        }

        var overdue = tasks.Count(IsOverdue);
        var highOpen = tasks.Count(task => !IsDone(task) && IsHighPriority(task.Priority));
        var unassignedHigh = tasks.Count(task => !IsDone(task) && IsHighPriority(task.Priority) && task.AssigneeId == null);
        var reviewBottleneck = tasks.Count(task => IsStatus(task, "InReview"));

        if (overdue == 0 && highOpen == 0 && reviewBottleneck <= 2)
        {
            return "Risk is stable. Keep the current cadence and review upcoming due dates.";
        }

        return $"Risk signals: {overdue} overdue tasks, {highOpen} open high-priority tasks, " +
               $"{unassignedHigh} high-priority tasks without an assignee, and {reviewBottleneck} tasks waiting for review.";
    }

    public async Task<string> SuggestTaskAssignmentAsync(Guid taskId, Guid projectId)
    {
        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .Select(member => member.UserId)
            .ToListAsync();

        var project = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId);

        if (project != null)
        {
            members.Add(project.OwnerId);
        }

        members = members.Distinct().ToList();

        if (members.Count == 0)
        {
            return "No project members are available for assignment.";
        }

        var openTasks = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Where(task => task.AssigneeId.HasValue && members.Contains(task.AssigneeId.Value) && task.Status != "Done")
            .GroupBy(task => task.AssigneeId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count);

        var users = await _userRepo.GetQueryable()
            .AsNoTracking()
            .Where(user => members.Contains(user.Id) && user.IsActive)
            .ToListAsync();

        var suggestion = users
            .OrderBy(user => openTasks.GetValueOrDefault(user.Id))
            .ThenBy(user => user.FullName)
            .FirstOrDefault();

        if (suggestion == null)
        {
            return "No active project members are available for assignment.";
        }

        return $"{suggestion.FullName} is the best fit right now with {openTasks.GetValueOrDefault(suggestion.Id)} open assigned tasks.";
    }

    public async Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var normalized = query.Trim();
        var taskQuery = _taskRepo.GetQueryable()
            .AsNoTracking()
            .Include(task => task.Project)
            .Where(task => task.Title.Contains(normalized) || (task.Description != null && task.Description.Contains(normalized)));

        if (projectId.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.ProjectId == projectId.Value);
        }

        var taskResults = await taskQuery
            .OrderByDescending(task => task.CreatedAt)
            .Take(5)
            .Select(task => $"Task: {task.Title} ({task.Project.Name})")
            .ToListAsync();

        var commentQuery = _commentRepo.GetQueryable()
            .AsNoTracking()
            .Include(comment => comment.TaskItem)
            .ThenInclude(task => task.Project)
            .Where(comment => comment.Content.Contains(normalized));

        if (projectId.HasValue)
        {
            commentQuery = commentQuery.Where(comment => comment.TaskItem.ProjectId == projectId.Value);
        }

        var commentResults = await commentQuery
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(5)
            .Select(comment => $"Comment on {comment.TaskItem.Title}: {comment.Content}")
            .ToListAsync();

        return taskResults.Concat(commentResults).Take(8).ToList();
    }

    public Task<IReadOnlyList<string>> GenerateSubtasksAsync(string taskTitle, string taskDescription)
    {
        var combined = $"{taskTitle} {taskDescription}".ToLowerInvariant();
        var subtasks = new List<string>
        {
            $"Clarify acceptance criteria for {taskTitle}",
            "Break down implementation scope",
            "Implement the main workflow",
            "Add validation and error handling",
            "Write focused tests",
            "Review and prepare release notes"
        };

        if (combined.Contains("auth") || combined.Contains("login") || combined.Contains("password"))
        {
            subtasks.Insert(2, "Verify authentication and authorization paths");
        }

        if (combined.Contains("ui") || combined.Contains("dashboard") || combined.Contains("page"))
        {
            subtasks.Insert(2, "Design responsive UI states");
        }

        if (combined.Contains("api") || combined.Contains("endpoint"))
        {
            subtasks.Insert(2, "Define request and response contracts");
        }

        return Task.FromResult<IReadOnlyList<string>>(subtasks.Distinct().Take(8).ToList());
    }

    public async Task<string> ChatAsync(string userMessage, Guid? projectId = null)
    {
        var normalized = userMessage.ToLowerInvariant();
        var keywords = new[] 
        { 
            "risk", "rủi ro", "rui ro", "summary", "tóm tắt", "tom tat", "overdue", "quá hạn", "qua han", 
            "priority", "ưu tiên", "u tien", "assignment", "phân công", "phan cong", "task", "công việc", "cong viec", 
            "project", "dự án", "du an", "status", "trạng thái", "trang thai", "deadline", "hạn", "han chot", 
            "progress", "tiến độ", "tien do", "member", "thành viên", "thanh vien", "done", "hoàn thành", "hoan thanh",
            "todo", "cần làm", "can lam", "doing", "đang làm", "dang lam"
        };

        if (!keywords.Any(normalized.Contains))
        {
            return "Tao đéo biết";
        }

        if (projectId.HasValue && (normalized.Contains("risk") || normalized.Contains("rủi ro") || normalized.Contains("rui ro")))
        {
            return await AnalyzeProjectRisksAsync(projectId.Value);
        }

        if (projectId.HasValue && (normalized.Contains("summary") || normalized.Contains("tóm tắt") || normalized.Contains("tom tat")))
        {
            return await GenerateProjectSummaryAsync(projectId.Value);
        }

        if (normalized.Contains("overdue") || normalized.Contains("quá hạn") || normalized.Contains("qua han"))
        {
            var now = DateTimeOffset.UtcNow;
            var overdue = await _taskRepo.GetQueryable()
                .AsNoTracking()
                .CountAsync(task => task.DueDate.HasValue && task.DueDate.Value < now && task.Status != "Done");

            return $"There are {overdue} overdue open tasks across the workspace.";
        }

        if (projectId.HasValue)
        {
            return await GenerateProjectSummaryAsync(projectId.Value);
        }

        return "Ask me about project summary, risk, overdue tasks, priority, or assignment suggestions.";
    }

    private static int CountAny(string value, params string[] needles)
        => needles.Count(value.Contains);

    private static bool IsDone(TaskItem task)
        => IsStatus(task, "Done");

    private static bool IsStatus(TaskItem task, string status)
        => string.Equals(task.Status, status, StringComparison.OrdinalIgnoreCase);

    private static bool IsOverdue(TaskItem task)
        => task.DueDate.HasValue && task.DueDate.Value < DateTimeOffset.UtcNow && !IsDone(task);

    private static bool IsHighPriority(string priority)
        => string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase);
}
