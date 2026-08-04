using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiAssistantContextRegistry : IAiAssistantContextRegistry
{
    private const int MaxWorkspaceProjects = 25;
    private const int MaxTaskFacts = 50;
    private const int MaxMemberFacts = 100;
    private const int MaxSkillFacts = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AiJobPlatformOptions _options;

    public AiAssistantContextRegistry(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IOptions<AiJobPlatformOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<Result<AiAssistantExecutionContextDto>> DiscoverAsync(
        AiAssistantTurnRequestDto request,
        CancellationToken ct = default)
    {
        if (!_options.AssistantContextRegistryEnabled)
            return Result.Failure<AiAssistantExecutionContextDto>(
                "Assistant context registry is disabled.", 503, "assistant_context_registry_disabled");
        if (_currentUser.UserId is not Guid userId)
            return Result.Forbidden<AiAssistantExecutionContextDto>();

        var requestedCapabilityId = NormalizeOptionalId(request.RequestedCapabilityId);
        if (requestedCapabilityId != null && !AiAssistantCapabilityCatalog.TryGet(requestedCapabilityId, out _))
            return Invalid("Unknown assistant capability.", "assistant_capability_unknown");
        var requestedSourceIds = NormalizeSourceIds(request.RequestedSourceIds);
        if (requestedSourceIds.Any(sourceId => !AiAssistantContextContract.KnownSourceIds.Contains(sourceId)))
            return Invalid("Unknown assistant context source.", "assistant_source_unknown");

        var isAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role) ||
            await _db.Users.AsNoTracking().AnyAsync(
                user => user.Id == userId && user.Role == ProjectRoleRules.SystemAdmin, ct);
        var projectId = ResolveProjectId(request.Context);
        if (projectId.HasValue)
        {
            var project = await _db.Projects.AsNoTracking()
                .Include(item => item.Organization).ThenInclude(organization => organization!.Members)
                .Include(item => item.Members)
                .SingleOrDefaultAsync(item => item.Id == projectId.Value && !item.IsDeleted, ct);
            if (project == null || !CanReadProject(project, userId, isAdmin))
                return Result.NotFound<AiAssistantExecutionContextDto>();
            return Result.Success(new AiAssistantExecutionContextDto(
                AuthorizedCapabilities(CanManageProject(project, userId, isAdmin)), [], []));
        }

        var projects = await _db.Projects.AsNoTracking()
            .Include(item => item.Organization).ThenInclude(organization => organization!.Members)
            .Include(item => item.Members)
            .Where(item => !item.IsDeleted && item.ArchivedAt == null)
            .ToListAsync(ct);
        var authorized = projects.Where(project => CanReadProject(project, userId, isAdmin)).ToList();
        return Result.Success(new AiAssistantExecutionContextDto(
            AuthorizedCapabilities(authorized.Any(project => CanManageProject(project, userId, isAdmin))), [], []));
    }

    public async Task<Result<AiAssistantExecutionContextDto>> ResolveAsync(
        AiAssistantTurnRequestDto request,
        CancellationToken ct = default)
    {
        if (!_options.AssistantContextRegistryEnabled)
        {
            return Result.Failure<AiAssistantExecutionContextDto>(
                "Assistant context registry is disabled.",
                503,
                "assistant_context_registry_disabled");
        }

        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Forbidden<AiAssistantExecutionContextDto>();
        }

        var inferredCapabilityId = AiAssistantCapabilityIntentClassifier.Infer(request.Message);
        var requestedCapabilityId = NormalizeOptionalId(request.RequestedCapabilityId);
        if (requestedCapabilityId != null &&
            !AiAssistantCapabilityCatalog.TryGet(requestedCapabilityId, out _))
        {
            return Invalid("Unknown assistant capability.", "assistant_capability_unknown");
        }

        var selectedCapabilityId = requestedCapabilityId ?? inferredCapabilityId;

        var requestedSourceIds = NormalizeSourceIds(request.RequestedSourceIds);
        var unknownSource = requestedSourceIds.FirstOrDefault(
            sourceId => !AiAssistantContextContract.KnownSourceIds.Contains(sourceId));
        if (unknownSource != null)
        {
            return Invalid("Unknown assistant context source.", "assistant_source_unknown");
        }

        var projectId = ResolveProjectId(request.Context);
        var isAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role) ||
            await _db.Users.AsNoTracking().AnyAsync(
                user => user.Id == userId && user.Role == ProjectRoleRules.SystemAdmin,
                ct);

        if (!projectId.HasValue)
        {
            return await ResolveWorkspaceAsync(
                request,
                userId,
                isAdmin,
                selectedCapabilityId,
                requestedSourceIds,
                ct);
        }

        var project = await _db.Projects
            .AsNoTracking()
            .Include(item => item.Organization)
                .ThenInclude(organization => organization!.Members)
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == projectId.Value && !item.IsDeleted, ct);
        if (project == null || !CanReadProject(project, userId, isAdmin))
        {
            return Result.NotFound<AiAssistantExecutionContextDto>();
        }

        var canManage = CanManageProject(project, userId, isAdmin);
        var capabilities = AuthorizedCapabilities(canManage);
        if (!capabilities.Any(item => item.CapabilityId == selectedCapabilityId))
        {
            return Result.Success(new AiAssistantExecutionContextDto(
                capabilities,
                [],
                [new AiAssistantSourceDisclosureDto(
                    "capability.context",
                    "denied",
                    "Khả năng này chưa được cấp quyền trong ngữ cảnh hiện tại",
                    ReasonCode: "capability_not_authorized")]));
        }

        var sourceIds = SelectProjectSources(
            selectedCapabilityId,
            request.Context,
            requestedSourceIds);
        var sources = new List<AiAssistantContextSourceEnvelopeDto>();
        var disclosures = new List<AiAssistantSourceDisclosureDto>();

        foreach (var sourceId in sourceIds)
        {
            var source = await MaterializeProjectSourceAsync(
                sourceId,
                project,
                request.Context,
                userId,
                isAdmin,
                canManage,
                ct);
            if (source == null)
            {
                disclosures.Add(new AiAssistantSourceDisclosureDto(
                    sourceId,
                    "denied",
                    "Nguồn hạn chế đã bị loại trước khi gọi AI",
                    ReasonCode: "source_not_visible"));
                continue;
            }

            sources.Add(source);
            disclosures.Add(ToDisclosure(source));
        }

        foreach (var requestedSourceId in requestedSourceIds.Except(sourceIds, StringComparer.Ordinal))
        {
            disclosures.Add(new AiAssistantSourceDisclosureDto(
                requestedSourceId,
                "skipped",
                "Nguồn không áp dụng cho capability hoặc ngữ cảnh hiện tại",
                ReasonCode: "source_not_applicable"));
        }

        return Result.Success(new AiAssistantExecutionContextDto(capabilities, sources, disclosures));
    }

    private async Task<Result<AiAssistantExecutionContextDto>> ResolveWorkspaceAsync(
        AiAssistantTurnRequestDto request,
        Guid userId,
        bool isAdmin,
        string selectedCapabilityId,
        IReadOnlyList<string> requestedSourceIds,
        CancellationToken ct)
    {
        var projects = await _db.Projects
            .AsNoTracking()
            .Include(item => item.Organization)
                .ThenInclude(organization => organization!.Members)
            .Include(item => item.Members)
            .Where(item => !item.IsDeleted && item.ArchivedAt == null)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ToListAsync(ct);
        var authorizedProjects = projects
            .Where(project => CanReadProject(project, userId, isAdmin))
            .ToList();
        var canManageAny = authorizedProjects.Any(project => CanManageProject(project, userId, isAdmin));
        var capabilities = AuthorizedCapabilities(canManageAny);

        if (!capabilities.Any(item => item.CapabilityId == selectedCapabilityId))
        {
            return Result.Success(new AiAssistantExecutionContextDto(
                capabilities,
                [],
                [new AiAssistantSourceDisclosureDto(
                    "capability.context",
                    "denied",
                    "Không có dự án được cấp quyền phù hợp cho khả năng này",
                    ReasonCode: "capability_not_authorized")]));
        }

        var source = BuildWorkspaceProjectsSource(authorizedProjects);
        var disclosures = new List<AiAssistantSourceDisclosureDto> { ToDisclosure(source) };
        foreach (var requestedSourceId in requestedSourceIds.Where(
                     sourceId => sourceId != AiAssistantContextContract.WorkspaceProjectsSource))
        {
            disclosures.Add(new AiAssistantSourceDisclosureDto(
                requestedSourceId,
                "skipped",
                "Nguồn cần một dự án cụ thể và chưa được đọc",
                ReasonCode: "project_context_required"));
        }

        return Result.Success(new AiAssistantExecutionContextDto(capabilities, [source], disclosures));
    }

    private List<AiAssistantCapabilityDescriptorDto> AuthorizedCapabilities(bool canManage)
    {
        var result = new List<AiAssistantCapabilityDescriptorDto>();
        if (AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.GroundedReadCapability,
                out var groundedRead))
        {
            result.Add(groundedRead);
        }

        if (_options.AssistantResearchPlanEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.ResearchPlanCapability,
                out var researchPlan))
        {
            result.Add(researchPlan);
        }

        if (canManage && _options.ActionComposerEnabled && _options.ActionComposerTaskCreateEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.TaskCreateCapability,
                out var taskCreate))
        {
            result.Add(taskCreate);
        }

        return result;
    }

    private async Task<AiAssistantContextSourceEnvelopeDto?> MaterializeProjectSourceAsync(
        string sourceId,
        Project project,
        AiAssistantClientContextDto? context,
        Guid userId,
        bool isAdmin,
        bool canManage,
        CancellationToken ct)
        => sourceId switch
        {
            AiAssistantContextContract.ProjectSummarySource => await BuildProjectSummarySourceAsync(project, userId, isAdmin, ct),
            AiAssistantContextContract.ProjectTasksSource => await BuildProjectTasksSourceAsync(project, userId, isAdmin, ct),
            AiAssistantContextContract.ProjectWorkloadSource => await BuildProjectWorkloadSourceAsync(project, userId, isAdmin, ct),
            AiAssistantContextContract.ProjectMembersSource when canManage => await BuildProjectMembersSourceAsync(project, ct),
            AiAssistantContextContract.ProjectSkillsSource when canManage => await BuildProjectSkillsSourceAsync(project, ct),
            AiAssistantContextContract.TaskDetailSource => await BuildTaskDetailSourceAsync(project, context, userId, isAdmin, ct),
            _ => null
        };

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildProjectSummarySourceAsync(
        Project project,
        Guid userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var tasks = await VisibleTasks(project, userId, isAdmin)
            .Select(item => new { item.Status, item.DueDate, item.UpdatedAt })
            .ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var done = tasks.Count(item => string.Equals(item.Status, "Done", StringComparison.OrdinalIgnoreCase));
        var overdue = tasks.Count(item => item.DueDate.HasValue && item.DueDate < now &&
            !string.Equals(item.Status, "Done", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        var freshness = tasks.Select(item => item.UpdatedAt).Where(value => value.HasValue)
            .Append(project.UpdatedAt ?? project.CreatedAt)
            .Max() ?? project.CreatedAt;
        var facts = new Dictionary<string, object?>
        {
            ["projectId"] = project.Id,
            ["name"] = project.Name,
            ["code"] = project.Code,
            ["status"] = project.Status,
            ["startDate"] = project.StartDate,
            ["endDate"] = project.EndDate,
            ["taskCount"] = tasks.Count,
            ["doneTaskCount"] = done,
            ["overdueTaskCount"] = overdue,
            ["progressPercent"] = tasks.Count == 0 ? 0 : (int)Math.Round(done * 100d / tasks.Count)
        };
        return BuildEnvelope(
            AiAssistantContextContract.ProjectSummarySource,
            project,
            "project",
            project.Name,
            freshness,
            facts,
            []);
    }

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildProjectTasksSourceAsync(
        Project project,
        Guid userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var allTaskCount = await _db.TaskItems.AsNoTracking()
            .CountAsync(item => item.ProjectId == project.Id && !item.IsDeleted, ct);
        var visibleTasks = await VisibleTasks(project, userId, isAdmin)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new
            {
                item.Id,
                item.Number,
                item.Title,
                item.Status,
                item.Priority,
                item.DueDate,
                item.EstimatedHours,
                item.ActualHours,
                item.AssigneeId,
                item.UpdatedAt,
                item.CreatedAt
            })
            .ToListAsync(ct);
        var selected = visibleTasks.Take(MaxTaskFacts).ToList();
        var freshness = visibleTasks.Select(item => item.UpdatedAt ?? item.CreatedAt)
            .Append(project.UpdatedAt ?? project.CreatedAt)
            .Max();
        var redactions = new List<string>();
        if (allTaskCount > visibleTasks.Count) redactions.Add("restricted_records_excluded");
        if (visibleTasks.Count > MaxTaskFacts) redactions.Add("context_limit_applied");
        var facts = new Dictionary<string, object?>
        {
            ["visibleTaskCount"] = visibleTasks.Count,
            ["statusCounts"] = visibleTasks.GroupBy(item => item.Status)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            ["tasks"] = selected
        };
        return BuildEnvelope(
            AiAssistantContextContract.ProjectTasksSource,
            project,
            "task_collection",
            "Danh sách task được phép đọc",
            freshness,
            facts,
            redactions);
    }

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildProjectWorkloadSourceAsync(
        Project project,
        Guid userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var visibleTasks = await VisibleTasks(project, userId, isAdmin)
            .Select(item => new
            {
                item.AssigneeId,
                item.Status,
                item.EstimatedHours,
                item.ActualHours,
                item.UpdatedAt,
                item.CreatedAt
            })
            .ToListAsync(ct);
        var members = await _db.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == project.Id && item.User.IsActive)
            .Select(item => new { item.UserId, item.User.FullName })
            .ToListAsync(ct);
        var workload = members.Select(member =>
        {
            var assigned = visibleTasks.Where(task => task.AssigneeId == member.UserId).ToList();
            return new
            {
                member.UserId,
                member.FullName,
                taskCount = assigned.Count,
                openTaskCount = assigned.Count(task => !string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase)),
                estimatedHours = assigned.Sum(task => task.EstimatedHours ?? 0),
                actualHours = assigned.Sum(task => task.ActualHours ?? 0)
            };
        }).ToList();
        var freshness = visibleTasks.Select(item => item.UpdatedAt ?? item.CreatedAt)
            .Append(project.UpdatedAt ?? project.CreatedAt)
            .Max();
        return BuildEnvelope(
            AiAssistantContextContract.ProjectWorkloadSource,
            project,
            "workload",
            "Workload dự án",
            freshness,
            new Dictionary<string, object?> { ["members"] = workload },
            []);
    }

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildProjectMembersSourceAsync(
        Project project,
        CancellationToken ct)
    {
        var members = await _db.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == project.Id && item.User.IsActive)
            .OrderBy(item => item.User.FullName)
            .Select(item => new
            {
                item.UserId,
                item.User.FullName,
                item.Role,
                item.JoinedAt,
                item.UpdatedAt
            })
            .Take(MaxMemberFacts + 1)
            .ToListAsync(ct);
        var redactions = members.Count > MaxMemberFacts ? new[] { "context_limit_applied" } : [];
        var selected = members.Take(MaxMemberFacts).ToList();
        var freshness = selected.Select(item => item.UpdatedAt ?? item.JoinedAt)
            .Append(project.UpdatedAt ?? project.CreatedAt)
            .Max();
        return BuildEnvelope(
            AiAssistantContextContract.ProjectMembersSource,
            project,
            "project_members",
            "Thành viên dự án",
            freshness,
            new Dictionary<string, object?> { ["members"] = selected },
            redactions);
    }

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildProjectSkillsSourceAsync(
        Project project,
        CancellationToken ct)
    {
        var skills = project.OrganizationId.HasValue
            ? await _db.OrganizationSkills.AsNoTracking()
                .Where(item => item.OrganizationId == project.OrganizationId.Value && item.IsActive)
                .OrderBy(item => item.Name)
                .Select(item => new { item.Id, item.Name, item.Description, item.UpdatedAt, item.CreatedAt })
                .Take(MaxSkillFacts + 1)
                .ToListAsync(ct)
            : [];
        var redactions = skills.Count > MaxSkillFacts ? new[] { "context_limit_applied" } : [];
        var selected = skills.Take(MaxSkillFacts).ToList();
        var freshness = selected.Select(item => item.UpdatedAt ?? item.CreatedAt)
            .Append(project.UpdatedAt ?? project.CreatedAt)
            .Max();
        return BuildEnvelope(
            AiAssistantContextContract.ProjectSkillsSource,
            project,
            "skill_catalog",
            "Danh mục kỹ năng được phép dùng",
            freshness,
            new Dictionary<string, object?> { ["skills"] = selected },
            redactions);
    }

    private async Task<AiAssistantContextSourceEnvelopeDto?> BuildTaskDetailSourceAsync(
        Project project,
        AiAssistantClientContextDto? context,
        Guid userId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (context?.EntityId is not Guid taskId ||
            !string.Equals(context.EntityType, "task", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var task = await VisibleTasks(project, userId, isAdmin)
            .Include(item => item.Assignees)
            .SingleOrDefaultAsync(item => item.Id == taskId, ct);
        if (task == null) return null;
        var version = task.RowVersion.Length > 0
            ? Convert.ToBase64String(task.RowVersion)
            : (task.UpdatedAt ?? task.CreatedAt).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var facts = new Dictionary<string, object?>
        {
            ["taskId"] = task.Id,
            ["taskKey"] = project.Code.Length > 0 && task.Number > 0 ? $"{project.Code}-{task.Number}" : null,
            ["title"] = task.Title,
            ["description"] = task.Description,
            ["status"] = task.Status,
            ["priority"] = task.Priority,
            ["startDate"] = task.StartDate,
            ["dueDate"] = task.DueDate,
            ["estimatedHours"] = task.EstimatedHours,
            ["actualHours"] = task.ActualHours,
            ["assigneeId"] = task.AssigneeId,
            ["assigneeIds"] = task.Assignees.Select(item => item.UserId).ToArray(),
            ["sourceVersion"] = version
        };
        return BuildEnvelope(
            AiAssistantContextContract.TaskDetailSource,
            project,
            "task",
            task.Title,
            task.UpdatedAt ?? task.CreatedAt,
            facts,
            [],
            $"tasks/{task.Id:D}");
    }

    private IQueryable<TaskItem> VisibleTasks(Project project, Guid userId, bool isAdmin)
    {
        var query = _db.TaskItems.AsNoTracking()
            .Where(item => item.ProjectId == project.Id && !item.IsDeleted);
        if (isAdmin || project.OwnerId == userId) return query;
        return query.Where(item =>
            !item.IsPrivate ||
            item.ReporterId == userId ||
            item.AssigneeId == userId ||
            item.Assignees.Any(assignment => assignment.UserId == userId));
    }

    private static AiAssistantContextSourceEnvelopeDto BuildWorkspaceProjectsSource(List<Project> projects)
    {
        var selected = projects.Take(MaxWorkspaceProjects).Select(project => new
        {
            projectId = project.Id,
            project.Name,
            project.Code,
            project.Status,
            project.StartDate,
            project.EndDate,
            project.UpdatedAt
        }).ToList();
        var freshness = projects.Select(project => project.UpdatedAt ?? project.CreatedAt)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max();
        var redactions = projects.Count > MaxWorkspaceProjects ? new[] { "context_limit_applied" } : [];
        var facts = new Dictionary<string, object?>
        {
            ["authorizedProjectCount"] = projects.Count,
            ["projects"] = selected
        };
        var hash = ComputeHash(facts);
        return new AiAssistantContextSourceEnvelopeDto(
            AiAssistantContextContract.WorkspaceProjectsSource,
            $"qaly://workspace/projects@{hash[7..19]}",
            "project_collection",
            "Các dự án được phép đọc",
            freshness,
            "qaly_domain_record",
            "workspace_private",
            hash,
            facts,
            redactions,
            "deterministic");
    }

    private static AiAssistantContextSourceEnvelopeDto BuildEnvelope(
        string sourceId,
        Project project,
        string sourceType,
        string title,
        DateTimeOffset freshness,
        IReadOnlyDictionary<string, object?> facts,
        IReadOnlyList<string> redactions,
        string? path = null)
    {
        var hash = ComputeHash(facts);
        var sourcePath = path ?? sourceId.Replace('.', '/');
        return new AiAssistantContextSourceEnvelopeDto(
            sourceId,
            $"qaly://project/{project.Id:D}/{sourcePath}@{hash[7..19]}",
            sourceType,
            title,
            freshness,
            "qaly_domain_record",
            "project_private",
            hash,
            facts,
            redactions,
            "deterministic");
    }

    private static string ComputeHash(IReadOnlyDictionary<string, object?> facts)
    {
        var json = JsonSerializer.Serialize(facts, JsonOptions);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static AiAssistantSourceDisclosureDto ToDisclosure(AiAssistantContextSourceEnvelopeDto source)
        => new(
            source.SourceId,
            "read",
            source.Redactions.Count == 0
                ? "Đã đọc nguồn Qaly đã kiểm quyền"
                : "Đã đọc sau khi loại dữ liệu hạn chế hoặc vượt giới hạn ngữ cảnh",
            source.SourceRef,
            source.Redactions.Count == 0 ? null : "source_redacted");

    private static bool CanReadProject(Project project, Guid userId, bool isAdmin)
        => isAdmin || project.OwnerId == userId ||
           project.Members.Any(member => member.UserId == userId) ||
           project.Organization?.OwnerId == userId ||
           project.Organization?.Members.Any(member => member.UserId == userId) == true;

    private static bool CanManageProject(Project project, Guid userId, bool isAdmin)
        => isAdmin || project.OwnerId == userId ||
           project.Members.Any(member => member.UserId == userId && ProjectRoleRules.CanManageProject(member.Role)) ||
           project.Organization?.OwnerId == userId ||
           project.Organization?.Members.Any(member =>
               member.UserId == userId &&
               (string.Equals(member.Role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(member.Role, "Admin", StringComparison.OrdinalIgnoreCase))) == true;

    private static Guid? ResolveProjectId(AiAssistantClientContextDto? context)
        => context?.ProjectId ??
           (string.Equals(context?.EntityType, "project", StringComparison.OrdinalIgnoreCase)
               ? context?.EntityId
               : null);

    private static List<string> SelectProjectSources(
        string capabilityId,
        AiAssistantClientContextDto? context,
        IReadOnlyList<string> requestedSourceIds)
    {
        var sourceIds = capabilityId == AiAssistantContextContract.TaskCreateCapability
            ? new List<string>
            {
                AiAssistantContextContract.ProjectSummarySource,
                AiAssistantContextContract.ProjectMembersSource,
                AiAssistantContextContract.ProjectSkillsSource,
                AiAssistantContextContract.ProjectWorkloadSource
            }
            : new List<string>
            {
                AiAssistantContextContract.ProjectSummarySource,
                AiAssistantContextContract.ProjectTasksSource,
                AiAssistantContextContract.ProjectWorkloadSource
            };
        if (string.Equals(context?.EntityType, "task", StringComparison.OrdinalIgnoreCase) &&
            context?.EntityId.HasValue == true)
        {
            sourceIds.Add(AiAssistantContextContract.TaskDetailSource);
        }

        foreach (var requestedSourceId in requestedSourceIds)
        {
            if (!sourceIds.Contains(requestedSourceId, StringComparer.Ordinal) &&
                requestedSourceId != AiAssistantContextContract.WorkspaceProjectsSource)
            {
                sourceIds.Add(requestedSourceId);
            }
        }

        return sourceIds.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> NormalizeSourceIds(IReadOnlyList<string>? sourceIds)
        => sourceIds?
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Select(sourceId => sourceId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

    private static string? NormalizeOptionalId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<AiAssistantExecutionContextDto> Invalid(string message, string errorCode)
        => Result.Failure<AiAssistantExecutionContextDto>(message, 400, errorCode);
}
