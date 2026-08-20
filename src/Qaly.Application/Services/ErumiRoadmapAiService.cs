using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Qaly.Application.Services;

public class ErumiRoadmapAiService : IErumiRoadmapAiService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<Sprint> _sprintRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<SystemModulePermission> _systemPermRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IAiGateway _aiGateway;

    private static readonly ConcurrentDictionary<Guid, ProposalScope> Proposals = new();
    private static readonly ConcurrentDictionary<Guid, AppliedSnapshot> Snapshots = new();

    public ErumiRoadmapAiService(
        IRepository<Project> projectRepo,
        IRepository<Sprint> sprintRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<SystemModulePermission> systemPermRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IAiGateway aiGateway)
    {
        _projectRepo = projectRepo;
        _sprintRepo = sprintRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _systemPermRepo = systemPermRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _aiGateway = aiGateway;
    }

    public async Task<Result<ErumiRoadmapChatResponseDto>> ChatAndProposeRoadmapAsync(ErumiRoadmapChatRequestDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<ErumiRoadmapChatResponseDto>();
        if (string.IsNullOrWhiteSpace(dto.UserMessage))
            return Result.Failure<ErumiRoadmapChatResponseDto>("Hãy mô tả mục tiêu của phương án lộ trình.", 400);

        // Check System Permission & AI Tier
        var userRole = _currentUserService.Role ?? "User";
        var systemPerm = await _systemPermRepo.GetQueryable()
            .FirstOrDefaultAsync(p => p.SystemRole == userRole && p.ModuleKey == "AiHub", ct);

        if (systemPerm != null && !systemPerm.IsAllowed)
        {
            return Result.Failure<ErumiRoadmapChatResponseDto>("Tài khoản của bạn đã bị System Admin giới hạn tính năng AI.", 403);
        }

        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound<ErumiRoadmapChatResponseDto>();
        if (!await CanReadProjectAsync(project, currentUserId.Value, ct))
            return Result.Forbidden<ErumiRoadmapChatResponseDto>();

        var members = await _memberRepo.GetQueryable()
            .Include(m => m.User)
            .Where(m => m.ProjectId == dto.ProjectId)
            .ToListAsync(ct);
        var currentTasks = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Where(task => task.ProjectId == dto.ProjectId && task.Status != "Cancelled")
            .OrderBy(task => task.SortOrder)
            .Take(80)
            .ToListAsync(ct);
        var currentSprints = await _sprintRepo.GetQueryable()
            .AsNoTracking()
            .Where(sprint => sprint.ProjectId == dto.ProjectId)
            .OrderBy(sprint => sprint.StartDate)
            .Take(30)
            .ToListAsync(ct);

        var startDate = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
        var model = await GenerateRoadmapModelAsync(project, dto.UserMessage.Trim(), currentTasks, currentSprints, currentUserId.Value, ct);
        var durationDays = Math.Clamp(model.DurationDays, 7, 42);
        var endDate = startDate.AddDays(durationDays);
        var proposedTasks = model.Tasks.Where(task => !string.IsNullOrWhiteSpace(task.Title)).Take(12).Select(task =>
        {
            var role = string.IsNullOrWhiteSpace(task.RecommendedRole) ? "Thành viên phù hợp" : task.RecommendedRole.Trim();
            var assignee = members.FirstOrDefault(member => RoleMatches(member.Role, role));
            return new ErumiTaskProposalDto(
                task.Title.Trim(),
                task.Description?.Trim() ?? string.Empty,
                NormalizePriority(task.Priority),
                Math.Clamp(task.EstimatedHours, 1, 80),
                role,
                assignee?.UserId,
                assignee?.User?.FullName);
        }).ToList();
        if (proposedTasks.Count == 0)
            return Result.Failure<ErumiRoadmapChatResponseDto>("Không lập được task có thể duyệt từ mục tiêu này.", 422);

        var activeAssigned = await _taskRepo.GetQueryable()
            .AsNoTracking()
            .Where(task => task.AssigneeId != null && task.Status != "Done" && task.Status != "Cancelled")
            .GroupBy(task => task.AssigneeId!.Value)
            .Select(group => new { UserId = group.Key, Hours = group.Sum(task => task.EstimatedHours ?? 0) })
            .ToDictionaryAsync(item => item.UserId, item => item.Hours, ct);
        var workloadImpacts = members.Select(member =>
        {
            var committed = activeAssigned.GetValueOrDefault(member.UserId);
            var additional = proposedTasks.Where(task => task.RecommendedAssigneeId == member.UserId).Sum(task => task.EstimatedHours);
            var overloaded = committed + additional > 40;
            return new ErumiWorkloadImpactDto(
                member.UserId,
                member.User?.FullName ?? "Thành viên",
                member.Role,
                committed,
                additional,
                overloaded,
                overloaded ? "Vượt ngưỡng 40 giờ công đang mở; cần đổi người hoặc giảm phạm vi." : "Chưa vượt ngưỡng 40 giờ công đang mở; vẫn cần kiểm tra lịch trước khi giao.");
        }).ToList();

        var snapshotId = Guid.NewGuid();
        var phaseName = string.IsNullOrWhiteSpace(model.PhaseName) ? "Giai đoạn đề xuất" : model.PhaseName.Trim();
        var summary = string.IsNullOrWhiteSpace(model.Summary) ? $"Phương án cho mục tiêu: {dto.UserMessage.Trim()}" : model.Summary.Trim();

        var proposal = new ErumiRoadmapDiffProposalDto(
            snapshotId,
            dto.ProjectId,
            phaseName,
            startDate,
            endDate,
            proposedTasks,
            workloadImpacts,
            summary
        );
        Proposals[snapshotId] = new ProposalScope(dto.ProjectId, phaseName, startDate, endDate, DateTimeOffset.UtcNow, proposedTasks);

        var replyMessage = $"Đã lập phương án cho {project.Name} từ {currentTasks.Count} task và {currentSprints.Count} Sprint hiện có. Hãy chỉnh rồi xác nhận nếu phù hợp.";

        return Result.Success(new ErumiRoadmapChatResponseDto(replyMessage, true, proposal));
    }

    public async Task<Result> ApproveRoadmapProposalAsync(ApproveErumiRoadmapProposalDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden();

        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound();
        if (!await CanManageProjectAsync(project, currentUserId.Value, ct)) return Result.Forbidden();
        if (!Proposals.TryGetValue(dto.SnapshotId, out var proposalScope) ||
            proposalScope.ProjectId != dto.ProjectId ||
            proposalScope.CreatedAt < DateTimeOffset.UtcNow.AddHours(-24))
        {
            return Result.Failure("Phương án không còn hợp lệ; hãy tạo lại từ dữ liệu mới nhất.", 409);
        }
        if (Snapshots.ContainsKey(dto.SnapshotId))
            return Result.Failure("Phương án này đã được áp dụng; không tạo lặp.", 409);
        if (dto.ApprovedTasks.Count is < 1 or > 20 || dto.ApprovedTasks.Any(task => string.IsNullOrWhiteSpace(task.Title)))
            return Result.Failure("Hãy chọn từ 1 đến 20 task hợp lệ.", 400);

        var allowedAssignees = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == dto.ProjectId)
            .Select(member => member.UserId)
            .ToHashSetAsync(ct);
        if (dto.ApprovedTasks.Any(task => task.RecommendedAssigneeId.HasValue && !allowedAssignees.Contains(task.RecommendedAssigneeId.Value)))
            return Result.Failure("Người được đề xuất không còn là thành viên dự án.", 409);

        // 1. Tạo Sprint mới
        var sprint = new Sprint
        {
            ProjectId = dto.ProjectId,
            Name = proposalScope.PhaseName,
            Goal = $"Phương án đã duyệt cho {project.Name}",
            StartDate = proposalScope.StartDate,
            EndDate = proposalScope.EndDate,
            Status = "Planning"
        };
        await _sprintRepo.AddAsync(sprint, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 2. Tạo các Tasks và đẩy vào Database/Kanban
        var createdTaskIds = new List<Guid>();
        foreach (var taskDto in dto.ApprovedTasks)
        {
            var task = new TaskItem
            {
                ProjectId = dto.ProjectId,
                SprintId = sprint.Id,
                Title = taskDto.Title.Trim(),
                Description = taskDto.Description,
                Priority = NormalizePriority(taskDto.Priority),
                EstimatedHours = Math.Clamp(taskDto.EstimatedHours, 1, 80),
                AssigneeId = taskDto.RecommendedAssigneeId,
                ReporterId = currentUserId.Value,
                Status = "Todo"
            };

            await _taskRepo.AddAsync(task, ct);
            createdTaskIds.Add(task.Id);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Lưu Snapshot để hỗ trợ 1-Click Rollback trong 24h
        Snapshots[dto.SnapshotId] = new AppliedSnapshot(dto.ProjectId, sprint.Id, createdTaskIds, DateTimeOffset.UtcNow);
        Proposals.TryRemove(dto.SnapshotId, out _);

        await _auditLogService.LogAsync("ApproveErumiRoadmap", nameof(Sprint), sprint.Id.ToString(), new { dto.SnapshotId, TaskCount = createdTaskIds.Count }, ct);

        return Result.Success();
    }

    public async Task<Result> RollbackRoadmapSnapshotAsync(RollbackErumiRoadmapSnapshotDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden();
        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound();
        if (!await CanManageProjectAsync(project, currentUserId.Value, ct)) return Result.Forbidden();
        if (!Snapshots.TryGetValue(dto.SnapshotId, out var snapshot) || snapshot.ProjectId != dto.ProjectId)
        {
            return Result.Failure("Không tìm thấy snapshot hoặc thời gian rollback đã hết hạn.", 404);
        }
        if (snapshot.CreatedAt < DateTimeOffset.UtcNow.AddHours(-24))
            return Result.Failure("Thời gian rollback 24 giờ đã hết.", 409);

        // Xóa tasks đã sinh
        foreach (var taskId in snapshot.TaskIds)
        {
            var task = await _taskRepo.GetByIdAsync(taskId, ct);
            if (task != null)
            {
                await _taskRepo.DeleteAsync(task, ct);
            }
        }

        // Xóa sprint
        var sprint = await _sprintRepo.GetByIdAsync(snapshot.SprintId, ct);
        if (sprint != null)
        {
            await _sprintRepo.DeleteAsync(sprint, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        Snapshots.TryRemove(dto.SnapshotId, out _);

        await _auditLogService.LogAsync("RollbackErumiRoadmap", nameof(Sprint), snapshot.SprintId.ToString(), new { dto.SnapshotId }, ct);

        return Result.Success();
    }

    private async Task<RoadmapModel> GenerateRoadmapModelAsync(
        Project project,
        string objective,
        IReadOnlyList<TaskItem> tasks,
        IReadOnlyList<Sprint> sprints,
        Guid userId,
        CancellationToken ct)
    {
        var facts = new
        {
            project = new { project.Name, project.Description, project.Status, project.StartDate, project.EndDate },
            objective,
            sprints = sprints.Select(item => new { item.Name, item.Goal, item.Status, item.StartDate, item.EndDate }),
            tasks = tasks.Select(item => new { item.Title, item.Status, item.Priority, item.EstimatedHours, item.DueDate })
        };
        var response = await _aiGateway.ExecuteAsync(new AiRequest
        {
            JobType = "roadmap_proposal",
            ProjectId = project.Id,
            TenantId = project.OrganizationId,
            UserId = userId,
            SourceType = "project",
            SourceEntityId = project.Id,
            Purpose = "Create an editable roadmap proposal",
            UseCache = false,
            AllowMockFallback = false,
            SystemPrompt = "Bạn là chuyên gia lập kế hoạch dự án. Chỉ dùng dữ kiện được cấp, không bịa lịch, kỹ năng hay capacity. Trả JSON thuần.",
            Prompt = $$"""
                Lập một giai đoạn Roadmap có thể duyệt cho dữ liệu sau:
                {{JsonSerializer.Serialize(facts)}}

                Trả đúng JSON dạng:
                {"phaseName":"...","summary":"...","durationDays":14,"tasks":[{"title":"...","description":"...","priority":"Low|Medium|High|Critical","estimatedHours":8,"recommendedRole":"..."}]}
                Tạo 3-8 task không trùng task hiện có. Tiêu đề và mô tả bằng tiếng Việt, rõ kết quả nghiệm thu. Không gán đích danh thành viên.
                """,
        }, ct);

        if (response.IsSuccess && !response.IsMock)
        {
            try
            {
                var json = response.Content.Trim();
                if (json.StartsWith("```", StringComparison.Ordinal))
                {
                    var firstNewLine = json.IndexOf('\n');
                    var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                    if (firstNewLine >= 0 && lastFence > firstNewLine)
                        json = json[(firstNewLine + 1)..lastFence].Trim();
                }
                var parsed = JsonSerializer.Deserialize<RoadmapModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed is { Tasks.Count: > 0 }) return parsed;
            }
            catch (JsonException)
            {
                // Fall through to a useful, source-bound local plan.
            }
        }

        var conciseObjective = objective.Length > 100 ? objective[..100].Trim() + "…" : objective;
        return new RoadmapModel
        {
            PhaseName = $"Giai đoạn: {conciseObjective}",
            Summary = "Model chưa trả phương án hợp lệ; Qaly dùng khung lập kế hoạch an toàn để cuộc làm việc không bị dừng.",
            DurationDays = 14,
            Tasks =
            [
                new() { Title = $"Làm rõ phạm vi và tiêu chí nghiệm thu: {conciseObjective}", Description = "Chốt phạm vi, đầu ra, rủi ro và tiêu chí nghiệm thu trước khi triển khai.", Priority = "High", EstimatedHours = 6, RecommendedRole = "Product" },
                new() { Title = $"Triển khai phạm vi: {conciseObjective}", Description = "Thực hiện phạm vi đã được duyệt và ghi lại bằng chứng có thể kiểm tra.", Priority = "High", EstimatedHours = 16, RecommendedRole = "Development" },
                new() { Title = $"Kiểm thử và nghiệm thu: {conciseObjective}", Description = "Kiểm thử end-to-end, xử lý lỗi còn lại và đối chiếu tiêu chí nghiệm thu.", Priority = "High", EstimatedHours = 8, RecommendedRole = "QA" },
            ]
        };
    }

    private async Task<bool> CanReadProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId || IsSystemAdmin()) return true;
        return await _memberRepo.GetQueryable().AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct);
    }

    private async Task<bool> CanManageProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId || IsSystemAdmin()) return true;
        var role = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == project.Id && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return role != null && new[] { "owner", "manager", "admin", "projectmanager", "leader" }
            .Any(value => role.Replace(" ", string.Empty).Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSystemAdmin() =>
        new[] { "admin", "superadmin", "systemadmin" }.Contains(
            (_currentUserService.Role ?? string.Empty).Replace(" ", string.Empty),
            StringComparer.OrdinalIgnoreCase);

    private static bool RoleMatches(string memberRole, string requestedRole)
    {
        var member = memberRole.ToLowerInvariant();
        var tokens = requestedRole.ToLowerInvariant().Split(new[] { ' ', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => token.Length >= 2 && member.Contains(token, StringComparison.Ordinal));
    }

    private static string NormalizePriority(string? value) =>
        new[] { "Low", "Medium", "High", "Critical" }
            .FirstOrDefault(item => item.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? "Medium";

    private sealed class RoadmapModel
    {
        public string PhaseName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int DurationDays { get; set; } = 14;
        public List<RoadmapModelTask> Tasks { get; set; } = [];
    }

    private sealed class RoadmapModelTask
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public int EstimatedHours { get; set; } = 4;
        public string RecommendedRole { get; set; } = "Thành viên phù hợp";
    }

    private sealed record ProposalScope(
        Guid ProjectId,
        string PhaseName,
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        DateTimeOffset CreatedAt,
        IReadOnlyList<ErumiTaskProposalDto> Tasks);

    private sealed record AppliedSnapshot(
        Guid ProjectId,
        Guid SprintId,
        List<Guid> TaskIds,
        DateTimeOffset CreatedAt);
}
