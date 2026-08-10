using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text;
using System.Text.Json;

namespace Qaly.Application.Services;

public partial class AiService : IAiService
{
    private readonly IAiGateway _aiGateway;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStorageService _vectorStorage;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;
    private readonly ILogger<AiService> _logger;
    private readonly AiTools _aiTools;
    private const string CollectionName = "qaly_context";
    private static readonly JsonSerializerOptions CategorizationPromptJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };
    private static readonly JsonSerializerOptions CategorizationResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly char[] KeywordSplitSeparators = new[] { ' ', ',', '.', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', '\n', '\r', '\t' };

    public AiService(
        IAiGateway aiGateway,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStorageService vectorStorage,
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy taskAccessPolicy,
        ILogger<AiService> logger,
        AiTools aiTools)
    {
        _aiGateway = aiGateway;

        _embeddingGenerator = embeddingGenerator;
        _vectorStorage = vectorStorage;
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _currentUserService = currentUserService;
        _taskAccessPolicy = taskAccessPolicy;
        _logger = logger;
        _aiTools = aiTools;
    }

    public async Task<string> SuggestTaskPriorityAsync(string taskTitle, string taskDescription, string projectContext)
    {
        var prompt = $@"Dựa trên thông tin công việc sau, hãy đề xuất độ ưu tiên (Low, Medium, High, Critical) và giải thích lý do ngắn gọn.
Dự án: {projectContext}
Công việc: {taskTitle}
Mô tả: {taskDescription}

Trả lời theo định dạng: [Priority] - [Lý do]";

        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "SuggestTaskPriority",
            Prompt = prompt,
            UserId = _currentUserService.UserId,
            UseCache = true
        });
        return response.Content ?? "Medium - Không thể xác định";
    }

    public async Task<string> GenerateProjectSummaryAsync(Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            LogUnauthorizedSummaryRequest(_logger, projectId, _currentUserService.UserId ?? Guid.Empty);
            return "Bạn không có quyền truy cập thông tin dự án này.";
        }

        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project == null) return "Không tìm thấy dự án.";

        var prompt = $"Hãy tóm tắt tình trạng hiện tại của dự án '{project.Name}'. Mô tả: {project.Description}";
        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "GenerateProjectSummary",
            Prompt = prompt,
            ProjectId = projectId,
            TenantId = project.OrganizationId,
            UserId = _currentUserService.UserId,
            UseCache = true
        });
        return response.Content ?? "Không thể tạo tóm tắt.";
    }

    public async Task<string> AnalyzeProjectRisksAsync(Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            LogUnauthorizedRiskAnalysisRequest(_logger, projectId, _currentUserService.UserId ?? Guid.Empty);
            return "Bạn không có quyền truy cập dữ liệu dự án này để phân tích rủi ro.";
        }

        var project = await GetProjectWithTasksAsync(projectId);
        if (project == null) return "Không tìm thấy dự án.";

        var overdueCount = project.Tasks.Count(IsTaskOverdue);
        var prompt = $"Phân tích rủi ro cho dự án '{project.Name}'. Hiện có {overdueCount} task quá hạn.";
        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "AnalyzeProjectRisks",
            Prompt = prompt,
            ProjectId = projectId,
            TenantId = project.OrganizationId,
            UserId = _currentUserService.UserId,
            UseCache = true
        });
        return response.Content ?? "Không thể phân tích rủi ro.";
    }

    public async Task<string> SuggestTaskAssignmentAsync(Guid taskId, Guid projectId)
    {
        var result = await GetTaskAssignmentInsightAsync(taskId, projectId);
        return result.IsSuccess && result.Data != null
            ? result.Data.RecommendationSummary
            : result.Error ?? "Không thể đưa ra đề xuất trong phạm vi được cấp quyền.";
    }

    public async Task<Result<TaskAssignmentInsightDto>> GetTaskAssignmentInsightAsync(Guid taskId, Guid projectId, CancellationToken ct = default)
    {
        var task = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Project)
                .ThenInclude(project => project.Owner)
            .Include(item => item.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(item => item.SkillRequirements)
                .ThenInclude(requirement => requirement.OrganizationSkill)
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId, ct);

        if (task == null ||
            !await _taskAccessPolicy.CanAccessTaskAsync(task, ct) ||
            !await _taskAccessPolicy.CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
        {
            return Result.NotFound<TaskAssignmentInsightDto>("Không tìm thấy công việc trong phạm vi được phép quản lý.");
        }

        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .Include(member => member.User)
            .ToListAsync(ct);
        var memberCandidates = members
            .Select(member => (member.UserId, member.User.FullName, member.Role))
            .ToList();
        if (memberCandidates.All(member => member.UserId != task.Project.OwnerId))
        {
            memberCandidates.Add((task.Project.OwnerId, task.Project.Owner.FullName, "ProjectOwner"));
        }

        var projectTasks = await _taskAccessPolicy.ApplyVisibilityFilter(_taskRepo.GetQueryable())
            .AsNoTracking()
            .Where(taskItem => taskItem.ProjectId == projectId)
            .Include(taskItem => taskItem.Assignees)
            .Include(taskItem => taskItem.SkillRequirements)
                .ThenInclude(requirement => requirement.OrganizationSkill)
            .Include(taskItem => taskItem.CompletionAttributions)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var requiredSkills = task.SkillRequirements
            .Where(requirement => requirement.OrganizationSkill.IsActive)
            .Select(requirement => new
            {
                requirement.OrganizationSkillId,
                requirement.OrganizationSkill.Name
            })
            .DistinctBy(requirement => requirement.OrganizationSkillId)
            .ToList();

        var candidates = memberCandidates.Select(member =>
        {
            var activeAssignments = projectTasks
                .Where(taskItem => IsAssignedTo(taskItem, member.UserId) && !IsClosedForAssignment(taskItem.Status))
                .ToList();
            var overdueTaskCount = activeAssignments.Count(item => item.DueDate.HasValue && item.DueDate.Value < now);
            var completedEvidenceTasks = projectTasks
                .Where(taskItem =>
                    string.Equals(taskItem.Status, "Done", StringComparison.OrdinalIgnoreCase) &&
                    taskItem.CompletionAttributions.Any(attribution =>
                        attribution.ContributorUserId == member.UserId &&
                        attribution.Status == TaskCompletionAttribution.Confirmed))
                .ToList();
            var evidenceBySkill = completedEvidenceTasks
                .SelectMany(completedTask => completedTask.SkillRequirements.Select(requirement => new
                {
                    TaskId = completedTask.Id,
                    requirement.OrganizationSkillId,
                    requirement.RequiredLevel,
                    CompletedAt = completedTask.CompletionAttributions
                        .Where(attribution =>
                            attribution.ContributorUserId == member.UserId &&
                            attribution.Status == TaskCompletionAttribution.Confirmed)
                        .Max(attribution => attribution.CompletedAt)
                }))
                .GroupBy(item => item.OrganizationSkillId)
                .ToDictionary(group => group.Key, group => new
                {
                    Count = group.Select(item => item.TaskId).Distinct().Count(),
                    MaxLevel = group.Max(item => SkillLevelRank(item.RequiredLevel)),
                    Latest = group.Max(item => item.CompletedAt)
                });
            var matching = requiredSkills.Where(requirement => evidenceBySkill.ContainsKey(requirement.OrganizationSkillId)).ToList();
            var matchingNames = matching.Select(requirement => requirement.Name).ToList();
            var matchingSkillIds = matching.Select(requirement => requirement.OrganizationSkillId).ToHashSet();
            var missing = requiredSkills.Where(requirement => !evidenceBySkill.ContainsKey(requirement.OrganizationSkillId)).Select(requirement => requirement.Name).ToList();
            var coveragePercent = requiredSkills.Count == 0
                ? 0
                : (int)Math.Round(matching.Count * 100m / requiredSkills.Count, MidpointRounding.AwayFromZero);
            var matchingConfidence = matching.Count == 0
                ? 0m
                : decimal.Round(matching.Average(requirement =>
                {
                    var evidence = evidenceBySkill[requirement.OrganizationSkillId];
                    return MemberSkillEvidenceService.CalculateConfidence(evidence.Count, evidence.MaxLevel);
                }), 2);
            var matchingBand = matching.Count == 0
                ? "none"
                : matching.Select(requirement =>
                    {
                        var evidence = evidenceBySkill[requirement.OrganizationSkillId];
                        return MemberSkillEvidenceService.CalculateEvidenceBand(evidence.Count, evidence.MaxLevel);
                    })
                    .OrderBy(EvidenceBandRank)
                    .First();
            var evidenceSources = completedEvidenceTasks
                .Select(completedTask => new
                {
                    Task = completedTask,
                    MatchedSkills = completedTask.SkillRequirements
                        .Where(requirement => matchingSkillIds.Contains(requirement.OrganizationSkillId))
                        .Select(requirement => requirement.OrganizationSkill.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    CompletedAt = completedTask.CompletionAttributions
                        .Where(attribution =>
                            attribution.ContributorUserId == member.UserId &&
                            attribution.Status == TaskCompletionAttribution.Confirmed)
                        .Max(attribution => attribution.CompletedAt)
                })
                .Where(item => item.MatchedSkills.Count > 0)
                .OrderByDescending(item => item.CompletedAt)
                .ToList();
            var recentCompletionCount = completedEvidenceTasks.Count(completedTask =>
                completedTask.CompletionAttributions.Any(attribution =>
                    attribution.ContributorUserId == member.UserId &&
                    attribution.Status == TaskCompletionAttribution.Confirmed &&
                    attribution.CompletedAt >= now.AddDays(-90)));

            var activeTaskCount = activeAssignments.Count;
            var activeHours = activeAssignments.Sum(item => item.EstimatedHours ?? 8);
            var workloadScore = Math.Max(0, 40 - Math.Min(32, activeHours / 4) - (overdueTaskCount * 4));
            var skillMatchScore = (int)Math.Round(coveragePercent * 0.4m, MidpointRounding.AwayFromZero);
            var historyScore = matching.Count == 0
                ? 0
                : Math.Min(20, (int)Math.Round(matchingConfidence * 12m, MidpointRounding.AwayFromZero) + Math.Min(8, recentCompletionCount * 2));
            var totalScore = workloadScore + skillMatchScore + historyScore;
            var recentSignals = new List<string>();
            if (activeTaskCount == 0) recentSignals.Add("Không có task mở trong phạm vi xem được");
            if (overdueTaskCount > 0) recentSignals.Add($"{overdueTaskCount} task quá hạn");
            if (recentCompletionCount > 0) recentSignals.Add($"{recentCompletionCount} bằng chứng hoàn thành trong 90 ngày");

            return new TaskAssignmentCandidateDto(
                member.UserId,
                member.FullName,
                member.Role,
                activeTaskCount,
                overdueTaskCount,
                recentCompletionCount,
                skillMatchScore,
                historyScore,
                workloadScore,
                totalScore,
                matchingNames,
                recentSignals,
                coveragePercent,
                matchingConfidence,
                matchingBand,
                missing,
                evidenceSources.Count,
                0,
                evidenceSources.Take(8).Select(item => new TaskAssignmentEvidenceSourceDto(
                    item.Task.Id,
                    item.Task.Title,
                    $"/projects/{projectId}/tasks/{item.Task.Id}",
                    item.CompletedAt,
                    item.MatchedSkills)).ToList());
        })
        .OrderByDescending(candidate => candidate.SkillCoveragePercent > 0)
        .ThenByDescending(candidate => candidate.TotalScore)
        .ThenByDescending(candidate => candidate.SkillCoveragePercent)
        .ThenBy(candidate => candidate.ActiveTaskCount)
        .ThenBy(candidate => candidate.FullName, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var evidenceState = requiredSkills.Count == 0
            ? "task_skills_missing"
            : candidates.Any(candidate => candidate.SkillCoveragePercent > 0)
                ? "ready"
                : "insufficient_evidence";
        var recommended = evidenceState == "ready" ? candidates.FirstOrDefault() : null;
        var summary = evidenceState switch
        {
            "task_skills_missing" => "Task chưa có kỹ năng yêu cầu đã xác nhận. Hệ thống không suy luận skill-fit từ label, mô tả hoặc lịch sử assignee.",
            "insufficient_evidence" => "Chưa có bằng chứng hoàn thành được xác nhận phù hợp với kỹ năng của task. Có thể xem workload, nhưng chưa được gọi đó là skill-fit.",
            _ => $"Ưu tiên xem xét {recommended!.FullName}: phủ {recommended.SkillCoveragePercent}% kỹ năng yêu cầu, confidence {decimal.Round(recommended.EvidenceConfidence * 100m)}%, {recommended.ActiveTaskCount} task đang mở trong phạm vi được phép xem."
        };

        return Result.Success(new TaskAssignmentInsightDto(
            task.Id,
            projectId,
            task.Title,
            task.Description,
            task.Priority,
            task.Status,
            task.DueDate,
            recommended?.UserId,
            recommended?.FullName ?? string.Empty,
            summary,
            now,
            candidates,
            "assignee-evidence-score.v1",
            evidenceState,
            "authorized_project_tasks",
            EncodeRowVersion(task.RowVersion),
            requiredSkills.Select(requirement => requirement.Name).ToList()));
    }

    private async Task<Result<TaskAssignmentInsightDto>> GetLegacyTaskAssignmentInsightAsync(Guid taskId, Guid projectId, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return Result.Forbidden<TaskAssignmentInsightDto>("Báº¡n khÃ´ng cÃ³ quyá»n truy cáº­p dá»± Ã¡n nÃ y.");
        }

        var task = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Project)
            .Include(item => item.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(item => item.Labels)
                .ThenInclude(label => label.ProjectLabel)
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId, ct);

        if (task == null)
        {
            return Result.NotFound<TaskAssignmentInsightDto>("KhÃ´ng tÃ¬m tháº¥y cÃ´ng viá»‡c.");
        }

        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .Include(member => member.User)
            .ToListAsync(ct);

        var projectTasks = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Where(taskItem => taskItem.ProjectId == projectId)
            .Include(taskItem => taskItem.Assignees)
                .ThenInclude(assignment => assignment.User)
            .Include(taskItem => taskItem.Labels)
                .ThenInclude(label => label.ProjectLabel)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var taskKeywords = ExtractKeywords(task.Title, task.Description);
        var taskLabels = task.Labels
            .Select(label => label.ProjectLabel?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var candidates = members.Select(member =>
        {
            var activeAssignments = projectTasks
                .Where(taskItem => IsAssignedTo(taskItem, member.UserId) && taskItem.Status is not ("Done" or "Cancelled"))
                .ToList();

            var overdueTaskCount = activeAssignments.Count(item => item.DueDate.HasValue && item.DueDate.Value < now);
            var recentCompletionCount = projectTasks.Count(taskItem =>
                IsAssignedTo(taskItem, member.UserId) &&
                taskItem.Status == "Done" &&
                taskItem.UpdatedAt.HasValue &&
                taskItem.UpdatedAt.Value >= now.AddDays(-90));

            var completedTasks = projectTasks
                .Where(taskItem => IsAssignedTo(taskItem, member.UserId) && taskItem.Status == "Done")
                .ToList();

            var skillSignals = completedTasks
                .SelectMany(item => item.Labels.Select(label => label.ProjectLabel?.Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => group.Key)
                .ToList();

            var matchingLabelCount = skillSignals.Intersect(taskLabels, StringComparer.OrdinalIgnoreCase).Count();
            var matchingKeywordCount = taskKeywords.Count(keyword =>
                completedTasks.Any(item =>
                    item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (item.Description != null && item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))));

            var activeTaskCount = activeAssignments.Count;
            var workloadScore = Math.Max(0, 60 - activeTaskCount * 12 - overdueTaskCount * 8);
            var skillMatchScore = matchingLabelCount * 20 + Math.Min(20, matchingKeywordCount * 4);
            var historyScore = Math.Min(20, recentCompletionCount * 3 + projectTasks.Count(taskItem =>
                IsAssignedTo(taskItem, member.UserId) &&
                taskItem.ReporterId == task.ReporterId));
            var totalScore = workloadScore + skillMatchScore + historyScore;

            var recentSignals = new List<string>();
            if (activeTaskCount == 0)
            {
                recentSignals.Add("DangTrong");
            }
            if (overdueTaskCount > 0)
            {
                recentSignals.Add($"{overdueTaskCount} task qua han");
            }
            if (recentCompletionCount > 0)
            {
                recentSignals.Add($"{recentCompletionCount} task hoan thanh gan day");
            }

            return new TaskAssignmentCandidateDto(
                member.UserId,
                member.User.FullName,
                member.Role,
                activeTaskCount,
                overdueTaskCount,
                recentCompletionCount,
                skillMatchScore,
                historyScore,
                workloadScore,
                totalScore,
                skillSignals,
                recentSignals);
        })
        .OrderByDescending(candidate => candidate.TotalScore)
        .ThenBy(candidate => candidate.ActiveTaskCount)
        .ThenByDescending(candidate => candidate.SkillMatchScore)
        .ToList();

        var recommended = candidates.FirstOrDefault();
        var summary = recommended == null
            ? "Không có dữ liệu để đề xuất."
            : $"Uu tien {recommended.FullName} vi workload thap, co {recommended.ActiveTaskCount} task dang mo va phu hop voi {string.Join(", ", recommended.SkillSignals.Take(3))}.";

        return Result.Success(new TaskAssignmentInsightDto(
            task.Id,
            projectId,
            task.Title,
            task.Description,
            task.Priority,
            task.Status,
            task.DueDate,
            recommended?.UserId,
            recommended?.FullName ?? string.Empty,
            summary,
            now,
            candidates));
    }

    public async Task<IReadOnlyList<string>> SmartSearchAsync(string query, Guid? projectId = null)
    {
        if (!projectId.HasValue)
        {
            return new List<string> { "Please select a project before using AI search." };
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            return new List<string> { "Bạn không có quyền tìm kiếm trong dự án này." };
        }

        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { query });
        var vector = queryEmbedding[0].Vector.ToArray();

        var filter = new VectorFilter
        {
            ProjectId = projectId.Value,
            OwnerId = _currentUserService.UserId,
            IsPrivate = false // By default, smart search only shows non-private items
        };

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 5);
        return results.Select(r => r.Payload.GetValueOrDefault("Content")?.ToString() ?? "").ToList();
    }

    public async Task<IReadOnlyList<string>> GenerateSubtasksAsync(string taskTitle, string taskDescription)
    {
        var prompt = $@"Hãy chia nhỏ công việc sau thành các sub-tasks thực tế (tối đa 5 task).
Công việc chính: {taskTitle}
Mô tả: {taskDescription}

Trả lời dưới dạng danh sách gạch đầu dòng.";

        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "GenerateSubtasks",
            Prompt = prompt,
            UserId = _currentUserService.UserId,
            UseCache = true
        });
        var text = response.Content ?? "";
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.TrimStart('-', ' ', '1', '2', '3', '.', '*'))
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .ToList();
    }

    public async Task<string> ChatAsync(string userMessage, Guid? projectId = null, string mode = "erumi", IList<AiChatMessageDto>? history = null)
    {
        if (!projectId.HasValue)
        {
            return "Please select a project before using Erumi with project data.";
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            return "Bạn không có quyền truy cập vào dữ liệu của dự án này.";
        }

        // Smart RAG: Refine query
        var searchQueries = await RefineSearchQueriesAsync(userMessage);
        
        var contextBuilder = new StringBuilder();
        foreach (var query in searchQueries)
        {
            var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { query });
            var vector = queryEmbedding[0].Vector.ToArray();

            var filter = new VectorFilter
            {
                ProjectId = projectId.Value,
                OwnerId = _currentUserService.UserId
            };

            var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 3);
            foreach (var res in results)
            {
                contextBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"- [Dữ liệu]: {res.Payload.GetValueOrDefault("Content")}");
                contextBuilder.AppendLine();
            }
        }

        var exportLink = projectId != null ? $"/api/ai/export/{projectId}?format=excel" : "#";
        var systemPrompt = $@"Bạn là Erumi (Erumi-chan), một Trợ lý ảo AI thông minh, tận tâm và chuyên nghiệp của hệ thống quản lý dự án Qaly.

PHONG CÁCH & TÍNH CÁCH:
1. Nhiệt tình, chu đáo: Luôn sẵn sàng hỗ trợ người dùng với thái độ tích cực.
2. Chính xác, chuyên nghiệp: Sử dụng ngôn ngữ Tiếng Việt chuẩn mực. Không 'chém gió' nếu không có dữ liệu.
3. Súc tích: Đi thẳng vào vấn đề, sử dụng định dạng Markdown (gạch đầu dòng, bảng, in đậm) để thông tin dễ đọc.

QUY TRÌNH SUY NGHĨ (Chain of Thought):
- Khi nhận được yêu cầu, hãy phân tích xem bạn có cần thêm thông tin từ hệ thống không.
- Nếu cần, hãy sử dụng các công cụ (Tools) được cung cấp (ví dụ: Tạo task, đổi trạng thái, phân công, tính giờ làm việc).
- Sau khi có kết quả từ tool, hãy kết hợp với ngữ cảnh dữ liệu (RAG Context) bên dưới để đưa ra câu trả lời cuối cùng.
- Luôn ưu tiên dữ liệu thực tế từ hệ thống hơn là kiến thức chung của bạn.

KIẾN THỨC VỀ HỆ THỐNG:
- Bạn có quyền truy cập vào Projects, Tasks, và Time Tracking thông qua công cụ.
- Bạn có thể hỗ trợ xuất báo cáo. Nếu người dùng yêu cầu, hãy cung cấp link tải.
  [📥 Tải báo cáo Excel dự án]({exportLink})

NGỮ CẢNH DỮ LIỆU HIỆN TẠI (RAG Context):
{contextBuilder}

HƯỚNG DẪN TRẢ LỜI:
- Dựa TRỰC TIẾP vào ngữ cảnh và kết quả trả về từ công cụ.
- Nếu không tìm thấy thông tin, hãy nói: 'Erumi không tìm thấy dữ liệu này trong hệ thống, bạn có thể cung cấp thêm chi tiết không?'
- Nếu người dùng muốn thực hiện hành động (tạo task, assign, stop timer...), hãy sử dụng tool tương ứng và thông báo kết quả.

ID dự án hiện tại (nếu có): {projectId}
Thời gian hiện tại: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";

        var chatHistory = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt)
        };

        if (history != null)
        {
            foreach (var msg in history)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                    ? ChatRole.Assistant : ChatRole.User;
                chatHistory.Add(new ChatMessage(role, msg.Content));
            }
        }

        chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var tenantId = await GetProjectTenantIdAsync(projectId);
        var request = new AiRequest
        {
            JobType = "Chat",
            Prompt = userMessage,
            SystemPrompt = systemPrompt,
            ProjectId = projectId,
            TenantId = tenantId,
            UserId = _currentUserService.UserId,
            History = history,
            UseCache = false
        };

        var response = await _aiGateway.ExecuteAsync(request);
        return response.Content;
    }

    public async IAsyncEnumerable<string> ChatStreamingAsync(string userMessage, Guid? projectId = null, string mode = "erumi", IList<AiChatMessageDto>? history = null)
    {
        if (!projectId.HasValue)
        {
            yield return "Please select a project before using Erumi with project data.";
            yield break;
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value))
        {
            yield return "Bạn không có quyền truy cập vào dữ liệu của dự án này.";
            yield break;
        }

        // Smart RAG: Simplified for streaming (single query)
        var queryEmbedding = await _embeddingGenerator.GenerateAsync(new[] { userMessage });
        var vector = queryEmbedding[0].Vector.ToArray();

        var filter = new VectorFilter
        {
            ProjectId = projectId.Value,
            OwnerId = _currentUserService.UserId
        };

        var results = await _vectorStorage.SearchAsync(vector, CollectionName, filter, limit: 5);

        var contextBuilder = new StringBuilder();
        foreach (var res in results)
        {
            contextBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"- [Loại: {res.Payload.GetValueOrDefault("ContentType")}]: {res.Payload.GetValueOrDefault("Content")}");
            contextBuilder.AppendLine();
        }

        var exportLink = projectId != null ? $"/api/ai/export/{projectId}?format=excel" : "#";
        var systemPrompt = $@"Bạn là Erumi (Erumi-chan), một Trợ lý ảo AI thông minh, tận tâm và chuyên nghiệp của hệ thống quản lý dự án Qaly.

PHONG CÁCH LÀM VIỆC:
1. Luôn lịch sự, sử dụng ngôn ngữ Tiếng Việt chuẩn mực, chuyên nghiệp nhưng vẫn thân thiện.
2. Trả lời súc tích, đi thẳng vào vấn đề.
3. Sử dụng định dạng Markdown (gạch đầu dòng, in đậm, bảng).

KIẾN THỨC VỀ HỆ THỐNG:
- Bạn có quyền truy cập và thực thi các tác vụ: Quản lý Task, Phân công, Tính giờ làm việc, Tìm kiếm tri thức.
- Bạn có thể hỗ trợ xuất báo cáo. Link format:
  [📥 Tải báo cáo Excel dự án]({exportLink})

NGỮ CẢNH DỮ LIỆU HIỆN TẠI:
{contextBuilder}

HƯỚNG DẪN:
- Dựa TRỰC TIẾP vào ngữ cảnh để trả lời.
- Sử dụng các công cụ (tools) được cung cấp để thực hiện hành động nếu người dùng yêu cầu.
- Nếu người dùng muốn xuất file, hãy đưa ra link tải như hướng dẫn trên.

ID dự án hiện tại (nếu có): {projectId}
Thời gian: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";

        var chatHistory = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt)
        };

        if (history != null)
        {
            foreach (var msg in history)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                    ? ChatRole.Assistant : ChatRole.User;
                chatHistory.Add(new ChatMessage(role, msg.Content));
            }
        }

        chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var tenantId = await GetProjectTenantIdAsync(projectId);
        var request = new AiRequest
        {
            JobType = "ChatStream",
            Prompt = userMessage,
            SystemPrompt = systemPrompt,
            ProjectId = projectId,
            TenantId = tenantId,
            UserId = _currentUserService.UserId,
            History = history,
            UseCache = false
        };

        AiResponse? response = null;
        string? connectionError = null;
        try
        {
            response = await _aiGateway.ExecuteAsync(request);
        }
        catch (Exception ex)
        {
            LogAiStreamStartFailed(_logger, ex);
            connectionError = "Xin lỗi, hiện tại tôi không thể kết nối tới máy chủ AI. Bạn hãy thử lại sau nhé.";
        }

        if (connectionError != null)
        {
            yield return connectionError;
            yield break;
        }

        if (response != null && !string.IsNullOrEmpty(response.Content))
        {
            var content = response.Content;
            var words = content.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                yield return words[i] + (i == words.Length - 1 ? "" : " ");
                await Task.Delay(10);
            }
        }
    }

    private async Task<List<string>> RefineSearchQueriesAsync(string userMessage)
    {
        var prompt = $@"Dựa trên tin nhắn của người dùng sau, hãy tạo ra tối đa 2 câu truy vấn tìm kiếm ngắn gọn (bằng tiếng Việt) để tìm kiếm thông tin liên quan trong kho dữ liệu dự án.
Chỉ trả về danh sách các câu truy vấn, mỗi câu một dòng.

Tin nhắn: {userMessage}";

        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "RefineSearchQueries",
            Prompt = prompt,
            UserId = _currentUserService.UserId,
            UseCache = true
        });
        var text = response.Content ?? userMessage;
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim().TrimStart('-'))
                   .Take(2)
                   .ToList();
    }

    private List<AITool> GetTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(_aiTools.GetProjectSummary),
            AIFunctionFactory.Create(_aiTools.GetOverdueTasks),
            AIFunctionFactory.Create(_aiTools.CreateTask),
            AIFunctionFactory.Create(_aiTools.UpdateTaskStatus),
            AIFunctionFactory.Create(_aiTools.AssignTask),
            AIFunctionFactory.Create(_aiTools.SuggestTaskAssignment),
            AIFunctionFactory.Create(_aiTools.SetTaskPriority),
            AIFunctionFactory.Create(_aiTools.AddDueDate),
            AIFunctionFactory.Create(_aiTools.AddComment),
            AIFunctionFactory.Create(_aiTools.GetMemberWorkload),
            AIFunctionFactory.Create(_aiTools.SearchKnowledge),
            AIFunctionFactory.Create(_aiTools.StartTimeTracking),
            AIFunctionFactory.Create(_aiTools.StopTimeTracking),
            AIFunctionFactory.Create(_aiTools.GetMyTimeLogs)
        };
    }

    public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return null;
        }

        return await _projectRepo.GetQueryable()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId);
    }

    public async Task<string> GenerateAnalyticsInsightsAsync(Guid projectId, string analyticsData)
    {
        if (!await CanAccessProjectAsync(projectId))
        {
            return "Bạn không có quyền truy cập dữ liệu phân tích của dự án này.";
        }

        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project == null) return "Không tìm thấy dự án.";

        var prompt = $@"Bạn là chuyên gia phân tích dữ liệu dự án. Hãy xem xét dữ liệu phân tích sau đây của dự án '{project.Name}' và đưa ra các nhận xét thông minh, phát hiện xu hướng, rủi ro tiềm ẩn hoặc cơ hội cải thiện hiệu suất.

Dữ liệu phân tích:
{analyticsData}

Yêu cầu:
1. Đưa ra 3-4 nhận xét quan trọng nhất.
2. Đề xuất hành động cụ thể để cải thiện dự án.
3. Trả lời bằng Tiếng Việt, súc tích và mang tính hành động cao.";

        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "GenerateAnalyticsInsights",
            Prompt = prompt,
            ProjectId = projectId,
            TenantId = project.OrganizationId,
            UserId = _currentUserService.UserId,
            UseCache = true
        });
        return response.Content ?? "Không thể tạo nhận xét phân tích.";
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId)
    {
        var projectOwnerId = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => (Guid?)item.OwnerId)
            .FirstOrDefaultAsync();
        return projectOwnerId.HasValue &&
            await _taskAccessPolicy.CanAccessProjectAsync(projectId, projectOwnerId.Value, CancellationToken.None);
    }

    private async Task<Guid?> GetProjectTenantIdAsync(Guid? projectId)
    {
        if (!projectId.HasValue) return null;
        var project = await _projectRepo.GetByIdAsync(projectId.Value);
        return project?.OrganizationId;
    }

    private static bool IsDone(TaskItem task)
        => IsStatus(task, "Done");

    private static bool IsStatus(TaskItem task, string status)
        => string.Equals(task.Status, status, StringComparison.OrdinalIgnoreCase);

    private static List<string> ExtractKeywords(string? title, string? description)
        => string.Join(' ', new[] { title, description }.Where(value => !string.IsNullOrWhiteSpace(value)))
            .Split(KeywordSplitSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Select(token => token.ToLowerInvariant())
            .Distinct()
            .Take(12)
            .ToList();

    private static bool IsAssignedTo(TaskItem task, Guid userId)
        => task.AssigneeId == userId || task.Assignees.Any(assignment => assignment.UserId == userId);

    private static bool IsClosedForAssignment(string? status)
        => string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);

    private static int SkillLevelRank(string value)
        => value switch
        {
            TaskSkillService.LevelExpert => 3,
            TaskSkillService.LevelProficient => 2,
            _ => 1
        };

    private static int EvidenceBandRank(string band)
        => band == "experienced" ? 3 : band == "practiced" ? 2 : band == "emerging" ? 1 : 0;

    private static string EncodeRowVersion(byte[] rowVersion)
        => rowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(rowVersion);

    private static bool IsTaskOverdue(TaskItem task)
        => task.DueDate.HasValue && task.DueDate.Value < DateTimeOffset.UtcNow && !IsDone(task);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Unauthorized AI summary request for project {ProjectId} by user {UserId}")]
    private static partial void LogUnauthorizedSummaryRequest(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unauthorized AI risk analysis request for project {ProjectId} by user {UserId}")]
    private static partial void LogUnauthorizedRiskAnalysisRequest(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error while starting AI stream.")]
    private static partial void LogAiStreamStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error while streaming AI response.")]
    private static partial void LogAiStreamFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Error while executing AI categorization batch.")]
    private static partial void LogAiCategorizationBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Error while generating an AI project plan.")]
    private static partial void LogAiPlanGenerationFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "AI project plan output was unusable; returning a deterministic fallback plan.")]
    private static partial void LogAiPlanFallback(ILogger logger);

    public async Task<List<Qaly.Application.DTOs.Import.AiCategorizationResult>> CategorizeTasksBatchAsync(List<Qaly.Application.DTOs.Import.AiCategorizationRequest> tasks)
    {
        if (tasks.Count == 0) return new List<Qaly.Application.DTOs.Import.AiCategorizationResult>();

        var taskJson = JsonSerializer.Serialize(tasks, CategorizationPromptJsonOptions);
        var prompt = $@"Bạn là trợ lý AI chuyên môn về Agile/Kanban. Nhiệm vụ của bạn là đọc các task sau và phân loại chúng vào các cột (Status) phù hợp, độ ưu tiên (Priority) hợp lý, và tối đa 2 nhãn (Labels) cho mỗi task.

Dữ liệu đầu vào:
{taskJson}

Bạn PHẢI trả về KẾT QUẢ ĐẦU RA dưới dạng một JSON Array HỢP LỆ, định dạng CHÍNH XÁC như mẫu sau (KHÔNG ĐƯỢC chứa thêm bất kỳ text nào khác ngoài mảng JSON):
[
  {{ ""RowIndex"": 1, ""Status"": ""Todo"", ""Priority"": ""High"", ""Labels"": [""Bug"", ""Frontend""] }}
]
Chỉ được chọn Status từ: Todo, InProgress, InReview, OnHold, Done.
Chỉ được chọn Priority từ: Low, Medium, High, Critical.
Chỉ xuất ra đúng mảng JSON, tuyệt đối không giải thích.";

        try
        {
            var response = await _aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "CategorizeTasksBatch",
                Prompt = prompt,
                UserId = _currentUserService.UserId,
                UseCache = true
            });
            var text = response.Content ?? "[]";
            
            // Extract json array if the AI enclosed it in markdown block ```json ... ```
            var startIdx = text.IndexOf('[');
            var endIdx = text.LastIndexOf(']');
            if (startIdx >= 0 && endIdx >= startIdx)
            {
                var jsonStr = text.Substring(startIdx, endIdx - startIdx + 1);
                var results = JsonSerializer.Deserialize<List<Qaly.Application.DTOs.Import.AiCategorizationResult>>(
                    jsonStr, 
                    CategorizationResponseJsonOptions
                );
                return results ?? new List<Qaly.Application.DTOs.Import.AiCategorizationResult>();
            }
        }
        catch (Exception ex)
        {
            LogAiCategorizationBatchFailed(_logger, ex);
        }

        return new List<Qaly.Application.DTOs.Import.AiCategorizationResult>();
    }

    public async Task<Result<GeneratedPlanDto>> GeneratePlanAsync(string userPrompt, Guid? projectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return Result.Failure<GeneratedPlanDto>("Yêu cầu không được để trống.", 400);
        }

        Project? projectContext = null;
        string projectInfoSection = "Đây là yêu cầu tạo một DỰ ÁN MỚI hoàn toàn.";

        if (projectId.HasValue)
        {
            projectContext = await _projectRepo.GetByIdAsync(projectId.Value, ct);
            if (projectContext == null)
            {
                return Result.Failure<GeneratedPlanDto>("Không tìm thấy dự án được chỉ định.", 404);
            }

            if (!await CanAccessProjectAsync(projectId.Value))
            {
                return Result.Forbidden<GeneratedPlanDto>("Bạn không có quyền truy cập dự án này.");
            }

            var existingTasks = await _taskRepo.GetQueryable()
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId.Value && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .Take(15)
                .Select(t => new { t.Title, t.Status, t.Priority })
                .ToListAsync(ct);

            var members = await _memberRepo.GetQueryable()
                .AsNoTracking()
                .Where(m => m.ProjectId == projectId.Value)
                .Include(m => m.User)
                .Select(m => new { m.User.FullName, m.Role })
                .ToListAsync(ct);

            var existingTasksText = existingTasks.Count > 0
                ? string.Join("\n", existingTasks.Select(t => $"- {t.Title} [{t.Status} / {t.Priority}]"))
                : "Chưa có công việc nào.";

            var membersText = members.Count > 0
                ? string.Join(", ", members.Select(m => $"{m.FullName} ({m.Role})"))
                : "Chưa có thông tin thành viên.";

            projectInfoSection = $@"Dự án hiện tại (BỔ SUNG TASK VÀO DỰ ÁN NÀY):
- Tên dự án: {projectContext.Name}
- Mô tả: {projectContext.Description ?? "Không có"}
- Thành viên dự án: {membersText}
- Các công việc đã có gần đây (TRÁNH TẠO TRÙNG):
{existingTasksText}";
        }

        var isNewProjectStr = projectContext == null ? "true" : "false";
        var defaultProjectNameStr = projectContext != null ? JsonSerializer.Serialize(projectContext.Name) : "\"Tên dự án được gợi ý\"";
        var defaultProjectDescStr = projectContext != null ? JsonSerializer.Serialize(projectContext.Description ?? "") : "\"Mô tả dự án được gợi ý\"";

        var prompt = $@"Bạn là trợ lý AI chuyên về quản lý dự án phần mềm Agile/Kanban chuyên nghiệp.
Nhiệm vụ của bạn là đọc yêu cầu của người dùng và lên kế hoạch tạo dự án mới hoặc tạo danh sách công việc (tasks) tương ứng.

Yêu cầu người dùng: {userPrompt}

{projectInfoSection}

Bạn PHẢI trả về KẾT QUẢ ĐẦU RA dưới dạng một đối tượng JSON HỢP LỆ, định dạng CHÍNH XÁC như mẫu sau (KHÔNG ĐƯỢC chứa thêm bất kỳ text nào khác ngoài JSON):
{{
  ""isNewProject"": {isNewProjectStr},
  ""projectName"": {defaultProjectNameStr},
  ""projectDescription"": {defaultProjectDescStr},
  ""tasks"": [
    {{
      ""title"": ""Tiêu đề công việc 1"",
      ""description"": ""Mô tả chi tiết công việc 1"",
      ""priority"": ""High"",
      ""dueDateOffsetDays"": 5,
      ""estimatedHours"": 8,
      ""suggestedRole"": ""Backend Developer"",
      ""category"": ""API""
    }},
    {{
      ""title"": ""Tiêu đề công việc 2"",
      ""description"": ""Mô tả chi tiết công việc 2"",
      ""priority"": ""Medium"",
      ""dueDateOffsetDays"": 10,
      ""estimatedHours"": 16,
      ""suggestedRole"": ""Frontend Developer"",
      ""category"": ""UI/UX""
    }}
  ]
}}

Lưu ý quan trọng:
1. Trường priority chỉ được chọn một trong các giá trị: Low, Medium, High, Critical.
2. Trường dueDateOffsetDays là số ngày ước lượng từ hôm nay để hoàn thành công việc đó (ví dụ: 3, 5, 7, 14). Hãy chọn số ngày phù hợp với độ phức tạp.
3. Trường estimatedHours là số giờ làm việc ước tính (ví dụ: 4, 8, 12, 16, 24).
4. Nội dung các công việc phải được viết bằng tiếng Việt chi tiết, rõ ràng, thực tế, đúng chuyên môn công nghệ/quản lý dự án. Hãy đề xuất từ 4-8 công việc chất lượng cao.
5. Chỉ xuất ra đúng đối tượng JSON hợp lệ bắt đầu bằng {{ và kết thúc bằng }}, tuyệt đối không thêm markdown wrapper hay bất kỳ ký tự nào bên ngoài JSON.";

        try
        {
            var response = await _aiGateway.ExecuteAsync(new AiRequest
            {
                JobType = "GenerateProjectPlan",
                Prompt = prompt,
                UserId = _currentUserService.UserId,
                UseCache = false
            }, ct);

            var text = response.Content ?? "{}";
            var result = CleanAndExtractJson<GeneratedPlanDto>(text);
            if (result != null && result.Tasks != null && result.Tasks.Count > 0)
            {
                return Result.Success(result);
            }

            LogAiPlanFallback(_logger);
            return Result.Success(BuildFallbackPlan(userPrompt, projectContext));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogAiPlanGenerationFailed(_logger, ex);
            return Result.Success(BuildFallbackPlan(userPrompt, projectContext));
        }
    }

    private static GeneratedPlanDto BuildFallbackPlan(string userPrompt, Project? projectContext)
    {
        var normalizedPrompt = userPrompt.Trim();
        var fallbackProjectName = normalizedPrompt.Length <= 80
            ? normalizedPrompt
            : normalizedPrompt[..80].TrimEnd();
        var scope = string.IsNullOrWhiteSpace(normalizedPrompt)
            ? "yêu cầu dự án"
            : normalizedPrompt;

        return new GeneratedPlanDto(
            projectContext == null,
            projectContext?.Name ?? fallbackProjectName,
            projectContext?.Description ?? $"Kế hoạch dự phòng được tạo từ yêu cầu: {scope}",
            new List<GeneratedPlanTaskDto>
            {
                new("Làm rõ phạm vi và tiêu chí hoàn thành", $"Xác nhận mục tiêu, đối tượng sử dụng và tiêu chí nghiệm thu cho: {scope}.", "High", 2, 4, "Manager", "Planning"),
                new("Thiết kế giải pháp và luồng chính", "Phác thảo kiến trúc, dữ liệu và các luồng người dùng quan trọng trước khi triển khai.", "High", 5, 6, "Architect", "Design"),
                new("Chuẩn bị nền tảng triển khai", "Thiết lập cấu trúc dự án, cấu hình môi trường và các phụ thuộc cần thiết.", "Medium", 7, 6, "DevOps", "Infra"),
                new("Phát triển chức năng cốt lõi", "Hiện thực các chức năng có giá trị cao nhất theo phạm vi đã thống nhất.", "High", 12, 12, "Developer", "Feature"),
                new("Kiểm thử và xử lý trường hợp biên", "Bổ sung kiểm thử tự động, kiểm tra phân quyền, dữ liệu lỗi và các luồng phục hồi.", "High", 16, 8, "QA", "Testing"),
                new("Nghiệm thu và bàn giao", "Rà soát tiêu chí hoàn thành, hoàn thiện tài liệu và chuẩn bị phát hành.", "Medium", 20, 4, "Manager", "Deployment")
            });
    }

    private static T? CleanAndExtractJson<T>(string rawContent) where T : class
    {
        if (string.IsNullOrWhiteSpace(rawContent)) return null;

        var text = rawContent.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                text = text.Substring(firstLineEnd + 1);
            }
            if (text.EndsWith("```", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 3).Trim();
            }
        }

        var startIdx = text.IndexOf('{');
        var endIdx = text.LastIndexOf('}');
        if (startIdx >= 0 && endIdx >= startIdx)
        {
            var jsonStr = text.Substring(startIdx, endIdx - startIdx + 1);
            try
            {
                return JsonSerializer.Deserialize<T>(jsonStr, CategorizationResponseJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}



