using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

/// <summary>
/// Builds source-bound roadmap proposals and writes canonical Sprint/Task data only after approval.
/// </summary>
public class ErumiRoadmapAiService : IErumiRoadmapAiService
{
    private static readonly TimeSpan SnapshotRetention = TimeSpan.FromHours(72);
    private static readonly JsonSerializerOptions RoadmapJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] ManageProjectRoles = ["owner", "manager", "admin", "projectadmin", "projectmanager", "leader", "techlead"];
    private static readonly string[] SystemAdminRoles = ["admin", "superadmin", "systemadmin"];
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Critical"];
    private static readonly char[] RoleSeparators = [' ', '/', '-'];
    private static readonly ConcurrentDictionary<Guid, ProposalScope> Proposals = new();
    private static readonly ConcurrentDictionary<Guid, AppliedSnapshot> Snapshots = new();
    private static readonly ConcurrentDictionary<Guid, byte> ApplyingSnapshots = new();

    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<Sprint> _sprintRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<SystemModulePermission> _systemPermRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IAiGateway _aiGateway;

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

    public async Task<Result<ErumiRoadmapChatResponseDto>> ChatAndProposeRoadmapAsync(
        ErumiRoadmapChatRequestDto dto,
        CancellationToken ct = default)
    {
        RemoveExpiredSnapshots();
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<ErumiRoadmapChatResponseDto>();
        if (string.IsNullOrWhiteSpace(dto.UserMessage))
            return Result.Failure<ErumiRoadmapChatResponseDto>("Hãy mô tả mục tiêu của phương án lộ trình.", 400);

        var permission = await ValidateAiReadPermissionAsync(dto.ProjectId, userId.Value, ct);
        if (!permission.IsSuccess)
            return Result.Failure<ErumiRoadmapChatResponseDto>(permission.Error ?? "Không có quyền truy cập.", permission.StatusCode);
        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound<ErumiRoadmapChatResponseDto>("Không tìm thấy dự án.");

        var members = await _memberRepo.GetQueryable().AsNoTracking().Include(x => x.User)
            .Where(x => x.ProjectId == dto.ProjectId).ToListAsync(ct);
        var currentTasks = await _taskRepo.GetQueryable().AsNoTracking()
            .Where(x => x.ProjectId == dto.ProjectId && x.Status != "Cancelled")
            .OrderBy(x => x.SortOrder).Take(80).ToListAsync(ct);
        var currentSprints = await _sprintRepo.GetQueryable().AsNoTracking()
            .Where(x => x.ProjectId == dto.ProjectId).OrderBy(x => x.StartDate).Take(30).ToListAsync(ct);

        var objective = string.IsNullOrWhiteSpace(dto.ContextSprintName)
            ? dto.UserMessage.Trim()
            : $"Trong Sprint/mốc '{dto.ContextSprintName.Trim()}': {dto.UserMessage.Trim()}";
        var model = await GenerateRoadmapModelAsync(project, objective, currentTasks, currentSprints, userId.Value, ct);
        var proposedTasks = MapProposedTasks(model.Tasks, members);
        if (proposedTasks.Count == 0)
            return Result.Failure<ErumiRoadmapChatResponseDto>("Không lập được task có thể duyệt từ mục tiêu này.", 422);

        var workload = await CalculateWorkloadImpactsAsync(members, proposedTasks, ct);
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
        var end = start.AddDays(Math.Clamp(model.DurationDays, 7, 42));
        var snapshotId = Guid.NewGuid();
        var phase = string.IsNullOrWhiteSpace(model.PhaseName) ? "Giai đoạn đề xuất" : model.PhaseName.Trim();
        var summary = string.IsNullOrWhiteSpace(model.Summary) ? $"Phương án cho mục tiêu: {objective}" : model.Summary.Trim();
        var risks = model.Risks.Where(x => !string.IsNullOrWhiteSpace(x.Description)).Select(x =>
            new ErumiRoadmapRiskDto(
                x.RiskType?.Trim() ?? "PlanningRisk",
                NormalizePriority(x.Severity),
                x.Description.Trim(),
                x.MitigationAdvice?.Trim() ?? "Cần người quản lý kiểm tra trước khi duyệt.",
                x.BlockedItemTitle?.Trim())).ToList();
        var proposal = new ErumiRoadmapDiffProposalDto(
            snapshotId, dto.ProjectId, phase, start, end, proposedTasks, workload, summary,
            Math.Clamp(model.ConfidenceScore, 0.1, 1), risks);
        Proposals[snapshotId] = new ProposalScope(dto.ProjectId, phase, start, end, DateTimeOffset.UtcNow);

        var reply = $"Đã lập phương án cho {project.Name} từ {currentTasks.Count} task và {currentSprints.Count} Sprint hiện có. Bạn có thể chỉnh từng task trước khi xác nhận.";
        return Result.Success(new ErumiRoadmapChatResponseDto(reply, true, proposal));
    }

    public async Task<Result<ErumiRoadmapDiffProposalDto>> ExecuteFastActionAsync(
        ErumiRoadmapActionRequestDto dto,
        CancellationToken ct = default)
    {
        var prompt = BuildFastActionPrompt(dto.ActionType, dto.UserPrompt);
        if (prompt == null)
            return Result.Failure<ErumiRoadmapDiffProposalDto>("Loại thao tác Roadmap không được hỗ trợ.", 400);
        var result = await ChatAndProposeRoadmapAsync(
            new ErumiRoadmapChatRequestDto(dto.ProjectId, prompt, dto.ContextSprintName), ct);
        return result.IsSuccess && result.Data?.Proposal != null
            ? Result.Success(result.Data.Proposal)
            : Result.Failure<ErumiRoadmapDiffProposalDto>(result.Error ?? "Không lập được phương án Roadmap.", result.StatusCode);
    }

    public async Task<Result<ErumiRoadmapSimulationResultDto>> SimulateScenarioAsync(
        ErumiRoadmapActionRequestDto dto,
        CancellationToken ct = default)
    {
        var scenario = string.IsNullOrWhiteSpace(dto.UserPrompt) ? "Mô phỏng Roadmap" : dto.UserPrompt.Trim();
        var proposalResult = await ChatAndProposeRoadmapAsync(
            new ErumiRoadmapChatRequestDto(dto.ProjectId, $"Mô phỏng, không ghi dữ liệu: {scenario}", dto.ContextSprintName), ct);
        if (!proposalResult.IsSuccess || proposalResult.Data?.Proposal == null)
            return Result.Failure<ErumiRoadmapSimulationResultDto>(proposalResult.Error ?? "Không mô phỏng được kịch bản.", proposalResult.StatusCode);

        var proposal = proposalResult.Data.Proposal;
        var totalHours = proposal.ProposedTasks.Sum(x => x.EstimatedHours);
        var availableCount = Math.Max(1, proposal.WorkloadImpacts.Count(x => !x.IsOverloaded));
        var days = (int)Math.Ceiling(totalHours / (availableCount * 6d));
        var overloaded = proposal.WorkloadImpacts.Count(x => x.IsOverloaded);
        var tradeoffs = new List<string>
        {
            $"Phạm vi đề xuất cần thêm khoảng {totalHours} giờ công.",
            $"Ước tính khoảng {days} ngày làm việc với {availableCount} thành viên chưa vượt ngưỡng tải.",
            overloaded > 0
                ? $"Có {overloaded} thành viên vượt ngưỡng; cần đổi người hoặc giảm phạm vi trước khi giao."
                : "Chưa vượt ngưỡng giờ công; vẫn phải đối chiếu lịch và tải đa dự án trước khi giao."
        };
        return Result.Success(new ErumiRoadmapSimulationResultDto(
            scenario, days, totalHours, Math.Round(proposal.ConfidenceScore * 100, 1),
            tradeoffs, proposal.WorkloadImpacts, proposal.Summary));
    }

    public async Task<Result<ErumiRoadmapExecutiveBriefDto>> GenerateExecutiveBriefAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden<ErumiRoadmapExecutiveBriefDto>();
        var permission = await ValidateAiReadPermissionAsync(projectId, userId.Value, ct);
        if (!permission.IsSuccess)
            return Result.Failure<ErumiRoadmapExecutiveBriefDto>(permission.Error ?? "Không có quyền truy cập.", permission.StatusCode);
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound<ErumiRoadmapExecutiveBriefDto>("Không tìm thấy dự án.");

        var sprints = await _sprintRepo.GetQueryable().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.StartDate).ToListAsync(ct);
        var tasks = await _taskRepo.GetQueryable().AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Status != "Cancelled").ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var completed = tasks.Count(IsTaskCompleted);
        var completion = tasks.Count == 0 ? 0 : Math.Round(completed * 100d / tasks.Count, 1);
        var overdue = tasks.Where(x => !IsTaskCompleted(x) && x.DueDate.HasValue && x.DueDate.Value < now).ToList();
        var achievements = sprints.Where(x => x.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"Hoàn thành mốc: {x.Name}").Take(3).ToList();
        if (achievements.Count == 0)
            achievements.Add(completed > 0 ? $"Đã hoàn thành {completed}/{tasks.Count} task." : "Chưa có mốc hoàn thành được ghi nhận.");
        var upcoming = sprints.Where(x => !x.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.EndDate).Select(x => $"{x.Name} — {x.EndDate:dd/MM/yyyy}").Take(3).ToList();
        var risks = overdue.Count > 0
            ? new List<string> { $"Có {overdue.Count} task quá hạn cần xử lý." }
            : new List<string> { "Chưa phát hiện task quá hạn trong dữ liệu hiện tại." };
        var health = overdue.Count > 0 ? "NeedsAttention" : "Healthy";
        var markdown = $"# {project.Name}\n\n- Trạng thái: {health}\n- Hoàn thành: {completion}% ({completed}/{tasks.Count} task)\n- Sprint/mốc: {sprints.Count}\n- Task quá hạn: {overdue.Count}";
        return Result.Success(new ErumiRoadmapExecutiveBriefDto(
            projectId, project.Name, health, completion, achievements, upcoming, risks, markdown, now));
    }

    public async Task<Result> ApproveRoadmapProposalAsync(
        ApproveErumiRoadmapProposalDto dto,
        CancellationToken ct = default)
    {
        RemoveExpiredSnapshots();
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden();
        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound("Không tìm thấy dự án.");
        if (!await CanManageProjectAsync(project, userId.Value, ct)) return Result.Forbidden();
        if (!Proposals.TryGetValue(dto.SnapshotId, out var proposal) || proposal.ProjectId != dto.ProjectId)
            return Result.Failure("Phương án không còn hợp lệ; hãy tạo lại từ dữ liệu mới nhất.", 409);
        if (Snapshots.ContainsKey(dto.SnapshotId) || !ApplyingSnapshots.TryAdd(dto.SnapshotId, 0))
            return Result.Failure("Phương án này đã hoặc đang được áp dụng; không tạo lặp.", 409);

        try
        {
            if (dto.ApprovedTasks.Count is < 1 or > 20 || dto.ApprovedTasks.Any(x => string.IsNullOrWhiteSpace(x.Title)))
                return Result.Failure("Hãy chọn từ 1 đến 20 task hợp lệ.", 400);
            var allowedAssignees = await _memberRepo.GetQueryable().Where(x => x.ProjectId == dto.ProjectId)
                .Select(x => x.UserId).ToHashSetAsync(ct);
            if (dto.ApprovedTasks.Any(x => x.RecommendedAssigneeId.HasValue && !allowedAssignees.Contains(x.RecommendedAssigneeId.Value)))
                return Result.Failure("Người được đề xuất không còn là thành viên dự án.", 409);

            var sprint = new Sprint
            {
                ProjectId = dto.ProjectId,
                Name = proposal.PhaseName,
                Goal = $"Phương án đã duyệt cho {project.Name}",
                StartDate = proposal.StartDate,
                EndDate = proposal.EndDate,
                Status = "Planning"
            };
            await _sprintRepo.AddAsync(sprint, ct);
            var taskIds = new List<Guid>();
            foreach (var source in dto.ApprovedTasks)
            {
                var task = new TaskItem
                {
                    ProjectId = dto.ProjectId,
                    SprintId = sprint.Id,
                    Title = source.Title.Trim(),
                    Description = source.Description?.Trim(),
                    Priority = NormalizePriority(source.Priority),
                    EstimatedHours = Math.Clamp(source.EstimatedHours, 1, 80),
                    AssigneeId = source.RecommendedAssigneeId,
                    ReporterId = userId.Value,
                    Status = "Todo"
                };
                await _taskRepo.AddAsync(task, ct);
                taskIds.Add(task.Id);
            }
            await _unitOfWork.SaveChangesAsync(ct);
            Snapshots[dto.SnapshotId] = new AppliedSnapshot(dto.ProjectId, sprint.Id, taskIds, DateTimeOffset.UtcNow);
            Proposals.TryRemove(dto.SnapshotId, out _);
            await _auditLogService.LogAsync("ApproveErumiRoadmap", nameof(Sprint), sprint.Id.ToString(),
                new { dto.SnapshotId, TaskCount = taskIds.Count, ApprovedBy = userId.Value }, ct);
            return Result.Success();
        }
        finally
        {
            ApplyingSnapshots.TryRemove(dto.SnapshotId, out _);
        }
    }

    public async Task<Result> RollbackRoadmapSnapshotAsync(
        RollbackErumiRoadmapSnapshotDto dto,
        CancellationToken ct = default)
    {
        RemoveExpiredSnapshots();
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Forbidden();
        var project = await _projectRepo.GetByIdAsync(dto.ProjectId, ct);
        if (project == null) return Result.NotFound("Không tìm thấy dự án.");
        if (!await CanManageProjectAsync(project, userId.Value, ct)) return Result.Forbidden();
        if (!Snapshots.TryGetValue(dto.SnapshotId, out var snapshot) || snapshot.ProjectId != dto.ProjectId)
            return Result.Failure("Không tìm thấy snapshot hoặc thời gian rollback đã hết hạn.", 404);

        foreach (var taskId in snapshot.TaskIds)
        {
            var task = await _taskRepo.GetByIdAsync(taskId, ct);
            if (task != null) await _taskRepo.DeleteAsync(task, ct);
        }
        var sprint = await _sprintRepo.GetByIdAsync(snapshot.SprintId, ct);
        if (sprint != null) await _sprintRepo.DeleteAsync(sprint, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        Snapshots.TryRemove(dto.SnapshotId, out _);
        await _auditLogService.LogAsync("RollbackErumiRoadmap", nameof(Sprint), snapshot.SprintId.ToString(),
            new { dto.SnapshotId, RolledBackBy = userId.Value }, ct);
        return Result.Success();
    }

    private async Task<RoadmapModel> GenerateRoadmapModelAsync(
        Project project, string objective, IReadOnlyList<TaskItem> tasks,
        IReadOnlyList<Sprint> sprints, Guid userId, CancellationToken ct)
    {
        var facts = new
        {
            project = new { project.Name, project.Description, project.Status, project.StartDate, project.EndDate },
            objective,
            sprints = sprints.Select(x => new { x.Name, x.Goal, x.Status, x.StartDate, x.EndDate }),
            tasks = tasks.Select(x => new { x.Title, x.Status, x.Priority, x.EstimatedHours, x.DueDate })
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
            SystemPrompt = "Bạn là chuyên gia lập kế hoạch dự án. Chỉ dùng dữ kiện được cấp, không bịa lịch, kỹ năng, capacity hay tích hợp ngoài. Trả JSON thuần.",
            Prompt = $$"""
                Lập một giai đoạn Roadmap có thể chỉnh sửa và duyệt từ dữ liệu sau:
                {{JsonSerializer.Serialize(facts)}}
                Trả đúng JSON dạng:
                {"phaseName":"...","summary":"...","durationDays":14,"confidenceScore":0.9,"risks":[{"riskType":"...","severity":"Low|Medium|High|Critical","description":"...","mitigationAdvice":"...","blockedItemTitle":null}],"tasks":[{"title":"...","description":"...","priority":"Low|Medium|High|Critical","estimatedHours":8,"recommendedRole":"...","dependencyNote":null}]}
                Tạo 3-12 task không trùng task hiện có. Dùng tiếng Việt, nêu rõ đầu ra và tiêu chí nghiệm thu. Không gán đích danh thành viên.
                """
        }, ct);
        if (response.IsSuccess && !response.IsMock)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<RoadmapModel>(StripJsonFence(response.Content), RoadmapJsonOptions);
                if (parsed is { Tasks.Count: > 0 }) return parsed;
            }
            catch (JsonException)
            {
                // Provider formatting must not dead-end the user flow.
            }
        }

        var concise = objective.Length > 100 ? objective[..100].Trim() + "…" : objective;
        return new RoadmapModel
        {
            PhaseName = $"Giai đoạn: {concise}",
            Summary = "Model chưa trả phương án hợp lệ; Qaly dùng khung bám theo mục tiêu để cuộc làm việc không bị dừng.",
            DurationDays = 14,
            ConfidenceScore = 0.55,
            Tasks =
            [
                new() { Title = $"Làm rõ phạm vi và tiêu chí nghiệm thu: {concise}", Description = "Chốt phạm vi, đầu ra, rủi ro và tiêu chí nghiệm thu.", Priority = "High", EstimatedHours = 6, RecommendedRole = "Product" },
                new() { Title = $"Triển khai phạm vi: {concise}", Description = "Thực hiện phạm vi đã duyệt và ghi lại bằng chứng có thể kiểm tra.", Priority = "High", EstimatedHours = 16, RecommendedRole = "Development" },
                new() { Title = $"Kiểm thử và nghiệm thu: {concise}", Description = "Kiểm thử end-to-end và đối chiếu tiêu chí nghiệm thu.", Priority = "High", EstimatedHours = 8, RecommendedRole = "QA" }
            ]
        };
    }

    private async Task<Result> ValidateAiReadPermissionAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var role = _currentUserService.Role ?? "User";
        var permission = await _systemPermRepo.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(x => x.SystemRole == role && x.ModuleKey == "AiHub", ct);
        if (permission is { IsAllowed: false })
            return Result.Failure("Tài khoản của bạn đã bị giới hạn quyền sử dụng AI Hub.", 403);
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return Result.NotFound("Không tìm thấy dự án.");
        return await CanReadProjectAsync(project, userId, ct) ? Result.Success() : Result.Forbidden();
    }

    private async Task<IReadOnlyList<ErumiWorkloadImpactDto>> CalculateWorkloadImpactsAsync(
        IReadOnlyList<ProjectMember> members, IReadOnlyList<ErumiTaskProposalDto> tasks, CancellationToken ct)
    {
        var assigned = await _taskRepo.GetQueryable().AsNoTracking()
            .Where(x => x.AssigneeId != null && x.Status != "Done" && x.Status != "Completed" && x.Status != "Cancelled")
            .GroupBy(x => x.AssigneeId!.Value)
            .Select(x => new { UserId = x.Key, Hours = x.Sum(task => task.EstimatedHours ?? 0) })
            .ToDictionaryAsync(x => x.UserId, x => x.Hours, ct);
        return members.Select(member =>
        {
            var current = assigned.GetValueOrDefault(member.UserId);
            var added = tasks.Where(x => x.RecommendedAssigneeId == member.UserId).Sum(x => x.EstimatedHours);
            var overloaded = current + added > 40;
            return new ErumiWorkloadImpactDto(member.UserId, member.User?.FullName ?? "Thành viên", member.Role,
                current, added, overloaded, overloaded
                    ? "Vượt ngưỡng 40 giờ công đang mở; cần đổi người hoặc giảm phạm vi."
                    : "Chưa vượt ngưỡng giờ công; vẫn cần kiểm tra lịch và tải đa dự án trước khi giao.");
        }).ToList();
    }

    private static List<ErumiTaskProposalDto> MapProposedTasks(
        IEnumerable<RoadmapModelTask> tasks, IReadOnlyList<ProjectMember> members) =>
        tasks.Where(x => !string.IsNullOrWhiteSpace(x.Title)).Take(12).Select(task =>
        {
            var role = string.IsNullOrWhiteSpace(task.RecommendedRole) ? "Thành viên phù hợp" : task.RecommendedRole.Trim();
            var assignee = members.FirstOrDefault(x => RoleMatches(x.Role, role));
            return new ErumiTaskProposalDto(task.Title.Trim(), task.Description?.Trim() ?? string.Empty,
                NormalizePriority(task.Priority), Math.Clamp(task.EstimatedHours, 1, 80), role,
                assignee?.UserId, assignee?.User?.FullName, task.DependencyNote?.Trim());
        }).ToList();

    private async Task<bool> CanReadProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId || IsSystemAdmin()) return true;
        return await _memberRepo.GetQueryable().AnyAsync(x => x.ProjectId == project.Id && x.UserId == userId, ct);
    }

    private async Task<bool> CanManageProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId || IsSystemAdmin()) return true;
        var role = await _memberRepo.GetQueryable().Where(x => x.ProjectId == project.Id && x.UserId == userId)
            .Select(x => x.Role).FirstOrDefaultAsync(ct);
        return role != null && ManageProjectRoles
            .Any(x => role.Replace(" ", string.Empty).Equals(x, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSystemAdmin() => SystemAdminRoles.Contains(
        (_currentUserService.Role ?? string.Empty).Replace(" ", string.Empty), StringComparer.OrdinalIgnoreCase);

    private static string? BuildFastActionPrompt(string actionType, string? custom)
    {
        var suffix = string.IsNullOrWhiteSpace(custom) ? string.Empty : $" Yêu cầu bổ sung: {custom.Trim()}";
        return actionType switch
        {
            ErumiRoadmapActionType.ExpandPhase => "Đề xuất phase mới phù hợp mục tiêu và trạng thái thực của dự án." + suffix,
            ErumiRoadmapActionType.AuditRisks => "Phân tích task nghẽn, dependency, deadline và đề xuất task xử lý rủi ro từ dữ liệu hiện có." + suffix,
            ErumiRoadmapActionType.AutoBalance => "Đề xuất điều chỉnh task để cân bằng tải đa dự án; không coi thời gian trống là capacity đã xác nhận." + suffix,
            ErumiRoadmapActionType.BreakdownWBS => "Bóc tách mục tiêu thành WBS và task có đầu ra, estimate, vai trò và dependency rõ ràng." + suffix,
            ErumiRoadmapActionType.Forecast => "Dự báo tiến độ từ Sprint, task, estimate và deadline; nêu rõ giả định và task giảm rủi ro." + suffix,
            _ => null
        };
    }

    private static string StripJsonFence(string content)
    {
        var json = content.Trim();
        if (!json.StartsWith("```", StringComparison.Ordinal)) return json;
        var firstLine = json.IndexOf('\n');
        var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine ? json[(firstLine + 1)..lastFence].Trim() : json;
    }

    private static bool IsTaskCompleted(TaskItem task) =>
        task.Status.Equals("Done", StringComparison.OrdinalIgnoreCase) || task.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);

    private static bool RoleMatches(string memberRole, string requestedRole)
    {
        var member = memberRole.ToLowerInvariant();
        var tokens = requestedRole.ToLowerInvariant().Split(RoleSeparators, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(x => x.Length >= 2 && member.Contains(x, StringComparison.Ordinal));
    }

    private static string NormalizePriority(string? value) =>
        Priorities.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? "Medium";

    private static void RemoveExpiredSnapshots()
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(SnapshotRetention);
        foreach (var item in Proposals.Where(x => x.Value.CreatedAt < cutoff)) Proposals.TryRemove(item.Key, out _);
        foreach (var item in Snapshots.Where(x => x.Value.CreatedAt < cutoff)) Snapshots.TryRemove(item.Key, out _);
    }

    private sealed class RoadmapModel
    {
        public string PhaseName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int DurationDays { get; set; } = 14;
        public double ConfidenceScore { get; set; } = 0.9;
        public List<RoadmapModelTask> Tasks { get; set; } = [];
        public List<RoadmapModelRisk> Risks { get; set; } = [];
    }

    private sealed class RoadmapModelTask
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public int EstimatedHours { get; set; } = 4;
        public string RecommendedRole { get; set; } = "Thành viên phù hợp";
        public string? DependencyNote { get; set; }
    }

    private sealed class RoadmapModelRisk
    {
        public string? RiskType { get; set; }
        public string? Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? MitigationAdvice { get; set; }
        public string? BlockedItemTitle { get; set; }
    }

    private sealed record ProposalScope(Guid ProjectId, string PhaseName, DateTimeOffset StartDate, DateTimeOffset EndDate, DateTimeOffset CreatedAt);
    private sealed record AppliedSnapshot(Guid ProjectId, Guid SprintId, List<Guid> TaskIds, DateTimeOffset CreatedAt);
}
