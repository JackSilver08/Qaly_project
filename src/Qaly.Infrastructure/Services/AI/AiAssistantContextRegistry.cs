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
    private readonly IAiNativeAuthorizationService _authorization;

    public AiAssistantContextRegistry(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IOptions<AiJobPlatformOptions> options,
        IAiNativeAuthorizationService authorization)
    {
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
        _authorization = authorization;
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
                user => user.Id == userId && user.IsActive && user.Role == ProjectRoleRules.SystemAdmin, ct);
        var systemTier = await ResolveSystemTierAsync(userId, ct);
        if (systemTier == AiNativeSystemTier.Restricted)
        {
            return RestrictedContext();
        }
        var canUseProjectLaunch = _options.ProjectLaunchBriefEnabled &&
            await HasReadableOrganizationAsync(userId, isAdmin, ct);
        var canManageOrganization = canUseProjectLaunch &&
            await HasManageableOrganizationAsync(userId, isAdmin, ct);
        var projectId = ResolveProjectId(request.Context);
        if (!projectId.HasValue &&
            string.Equals(request.Context?.EntityType, "meeting", StringComparison.OrdinalIgnoreCase) &&
            request.Context?.EntityId is Guid meetingEntityId)
        {
            projectId = await ResolveMeetingProjectIdAsync(meetingEntityId, ct);
        }
        if (string.Equals(request.Context?.EntityType, "group", StringComparison.OrdinalIgnoreCase) &&
            request.Context?.EntityId is Guid discoveredGroupId)
        {
            var group = await _db.WorkGroups.AsNoTracking().Include(item => item.Members)
                .SingleOrDefaultAsync(item => item.Id == discoveredGroupId && !item.IsDeleted, ct);
            if (group == null) return Result.NotFound<AiAssistantExecutionContextDto>();
            var role = group.OwnerId == userId
                ? Qaly.Application.Services.Groups.GroupRoleRules.Owner
                : group.Members.FirstOrDefault(item => item.UserId == userId)?.Role;
            if (role == null) return Result.NotFound<AiAssistantExecutionContextDto>();
            var capabilities = AuthorizedCapabilities(false, canUseProjectLaunch, canManageOrganization, systemTier);
            if (_options.NativeDomainActionsEnabled && systemTier == AiNativeSystemTier.Full &&
                Qaly.Application.Services.Groups.GroupRoleRules.CanCreatePoll(role))
                AddCapability(capabilities, AiAssistantContextContract.GroupPollCapability);
            return Result.Success(new AiAssistantExecutionContextDto(capabilities, [], []));
        }
        if (projectId.HasValue)
        {
            var project = await _db.Projects.AsNoTracking()
                .Include(item => item.Organization).ThenInclude(organization => organization!.Members)
                .Include(item => item.Members)
                .SingleOrDefaultAsync(item => item.Id == projectId.Value && !item.IsDeleted, ct);
            if (project == null || !CanReadProject(project, userId, isAdmin))
                return Result.NotFound<AiAssistantExecutionContextDto>();
            var authorization = await ResolveProjectAuthorizationAsync(project, userId, isAdmin, ct);
            return Result.Success(new AiAssistantExecutionContextDto(
                AuthorizedCapabilities(
                    authorization.CanManage,
                    canUseProjectLaunch,
                    canManageOrganization,
                    systemTier), [], []));
        }

        var projects = await _db.Projects.AsNoTracking()
            .Include(item => item.Organization).ThenInclude(organization => organization!.Members)
            .Include(item => item.Members)
            .Where(item => !item.IsDeleted && item.ArchivedAt == null)
            .ToListAsync(ct);
        var authorized = projects.Where(project => CanReadProject(project, userId, isAdmin)).ToList();
        var canManageAny = false;
        foreach (var project in authorized)
        {
            if ((await ResolveProjectAuthorizationAsync(project, userId, isAdmin, ct)).CanManage)
            {
                canManageAny = true;
                break;
            }
        }
        return Result.Success(new AiAssistantExecutionContextDto(
            AuthorizedCapabilities(
                canManageAny,
                canUseProjectLaunch,
                canManageOrganization,
                systemTier), [], []));
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
        if (!projectId.HasValue &&
            string.Equals(request.Context?.EntityType, "meeting", StringComparison.OrdinalIgnoreCase) &&
            request.Context?.EntityId is Guid meetingEntityId)
        {
            projectId = await ResolveMeetingProjectIdAsync(meetingEntityId, ct);
        }
        var isAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role) ||
            await _db.Users.AsNoTracking().AnyAsync(
                user => user.Id == userId && user.IsActive && user.Role == ProjectRoleRules.SystemAdmin,
                ct);
        var systemTier = await ResolveSystemTierAsync(userId, ct);
        if (systemTier == AiNativeSystemTier.Restricted)
        {
            return RestrictedContext();
        }

        if (selectedCapabilityId is AiAssistantContextContract.ProjectLaunchCapability or
            AiAssistantContextContract.ProjectStaffingPlanCapability or
            AiAssistantContextContract.ProjectLaunchExecuteCapability)
        {
            return await ResolveOrganizationLaunchAsync(
                request, userId, isAdmin, selectedCapabilityId, systemTier, ct);
        }

        if (selectedCapabilityId == AiAssistantContextContract.GroupPollCapability &&
            string.Equals(request.Context?.EntityType, "group", StringComparison.OrdinalIgnoreCase) &&
            request.Context?.EntityId is Guid groupId)
        {
            return await ResolveGroupPollAsync(request, groupId, userId, systemTier, ct);
        }

        if ((selectedCapabilityId is AiAssistantContextContract.GroundedReadCapability or
                AiAssistantContextContract.ResearchPlanCapability) &&
            string.Equals(request.Context?.EntityType, "group", StringComparison.OrdinalIgnoreCase) &&
            request.Context?.EntityId is Guid readableGroupId &&
            (!projectId.HasValue || IsExplicitGroupReadRequest(request.Message)))
        {
            return await ResolveGroupReadAsync(readableGroupId, userId, selectedCapabilityId, systemTier, ct);
        }

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

        var projectAuthorization = await ResolveProjectAuthorizationAsync(project, userId, isAdmin, ct);
        var canManage = projectAuthorization.CanManage;
        var capabilities = AuthorizedCapabilities(
            canManage,
            _options.ProjectLaunchBriefEnabled && project.OrganizationId.HasValue,
            systemTier: systemTier);
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
        var systemTier = await ResolveSystemTierAsync(userId, ct);
        if (systemTier == AiNativeSystemTier.Restricted)
        {
            return RestrictedContext();
        }
        var canManageAny = false;
        foreach (var project in authorizedProjects)
        {
            if ((await ResolveProjectAuthorizationAsync(project, userId, isAdmin, ct)).CanManage)
            {
                canManageAny = true;
                break;
            }
        }
        var canManageOrganization = await HasManageableOrganizationAsync(userId, isAdmin, ct);
        var capabilities = AuthorizedCapabilities(
            canManageAny,
            _options.ProjectLaunchBriefEnabled && await HasReadableOrganizationAsync(userId, isAdmin, ct),
            canManageOrganization,
            systemTier);

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

    private List<AiAssistantCapabilityDescriptorDto> AuthorizedCapabilities(
        bool canManageProject,
        bool canUseProjectLaunch,
        bool? canManageOrganization = null,
        AiNativeSystemTier systemTier = AiNativeSystemTier.Full)
    {
        if (systemTier == AiNativeSystemTier.Restricted)
        {
            return [];
        }

        var canManageLaunch = canManageOrganization ?? canManageProject;
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

        if (systemTier == AiNativeSystemTier.SummaryOnly)
        {
            return result;
        }

        if (canManageProject && _options.ActionComposerEnabled && _options.ActionComposerTaskCreateEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.TaskCreateCapability,
                out var taskCreate))
        {
            result.Add(taskCreate);
        }

        if (canManageProject && _options.ActionComposerEnabled)
        {
            AddCapability(result, AiAssistantContextContract.TaskAssignmentScheduleCapability);
        }

        if (canManageProject && _options.NativeDomainActionsEnabled)
        {
            AddCapability(result, AiAssistantContextContract.AcceptanceChecklistCapability);
            AddCapability(result, AiAssistantContextContract.TaskBreakdownCapability);
            AddCapability(result, AiAssistantContextContract.WikiBriefTaskCapability);
            AddCapability(result, AiAssistantContextContract.ProjectDigestCapability);
            AddCapability(result, AiAssistantContextContract.MeetingActionsCapability);
            AddCapability(result, AiAssistantContextContract.RoadmapAdjustCapability);
            AddCapability(result, AiAssistantContextContract.SkillEvidenceCapability);
        }

        if (canUseProjectLaunch &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.ProjectLaunchCapability,
                out var projectLaunch))
        {
            result.Add(projectLaunch);
        }

        if (canUseProjectLaunch && canManageLaunch && _options.ProjectLaunchPlanningEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.ProjectStaffingPlanCapability,
                out var staffingPlan))
        {
            result.Add(staffingPlan);
        }

        if (canUseProjectLaunch && canManageLaunch && _options.ProjectLaunchExecutionEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.ProjectLaunchExecuteCapability,
                out var launchExecute))
        {
            result.Add(launchExecute);
        }

        if (canManageLaunch && _options.ProjectOperationMonitoringEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.ProjectOperationMonitorCapability,
                out var operationMonitor))
        {
            result.Add(operationMonitor);
        }

        if (_options.SafeTestOrchestratorEnabled &&
            AiAssistantCapabilityCatalog.TryGet(
                AiAssistantContextContract.SafeTestRunCapability,
                out var safeTestRun))
        {
            result.Add(safeTestRun);
        }

        return result;
    }

    private static void AddCapability(List<AiAssistantCapabilityDescriptorDto> target, string capabilityId)
    {
        if (AiAssistantCapabilityCatalog.TryGet(capabilityId, out var descriptor)) target.Add(descriptor);
    }

    private async Task<Result<AiAssistantExecutionContextDto>> ResolveGroupPollAsync(
        AiAssistantTurnRequestDto request,
        Guid groupId,
        Guid userId,
        AiNativeSystemTier systemTier,
        CancellationToken ct)
    {
        var group = await _db.WorkGroups.AsNoTracking().Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == groupId && !item.IsDeleted, ct);
        if (group == null) return Result.NotFound<AiAssistantExecutionContextDto>();
        var role = group.OwnerId == userId
            ? Qaly.Application.Services.Groups.GroupRoleRules.Owner
            : group.Members.FirstOrDefault(item => item.UserId == userId)?.Role;
        if (role == null) return Result.NotFound<AiAssistantExecutionContextDto>();

        var capabilities = AuthorizedCapabilities(false, false, systemTier: systemTier);
        if (_options.NativeDomainActionsEnabled && systemTier == AiNativeSystemTier.Full &&
            Qaly.Application.Services.Groups.GroupRoleRules.CanCreatePoll(role))
            AddCapability(capabilities, AiAssistantContextContract.GroupPollCapability);
        if (!capabilities.Any(item => item.CapabilityId == AiAssistantContextContract.GroupPollCapability))
            return Result.Success(new AiAssistantExecutionContextDto(capabilities, [],
                [new AiAssistantSourceDisclosureDto("capability.context", "denied",
                    "Bạn có thể đọc nhóm nhưng không có quyền tạo Poll.", ReasonCode: "capability_not_authorized")]));

        var facts = new Dictionary<string, object?>
        {
            ["group_id"] = group.Id,
            ["name"] = group.Name,
            ["status"] = group.Status,
            ["member_count"] = group.Members.Count,
            ["current_user_role"] = role
        };
        var source = Envelope(AiAssistantContextContract.GroupContextSource, $"/groups/{group.Id}", "group",
            group.Name, group.UpdatedAt ?? group.CreatedAt, "canonical", "group_member", facts, "ef_core_group_context");
        return Result.Success(new AiAssistantExecutionContextDto(capabilities, [source], [ToDisclosure(source)]));
    }

    private async Task<Result<AiAssistantExecutionContextDto>> ResolveGroupReadAsync(
        Guid groupId,
        Guid userId,
        string selectedCapabilityId,
        AiNativeSystemTier systemTier,
        CancellationToken ct)
    {
        var group = await _db.WorkGroups.AsNoTracking().Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == groupId && !item.IsDeleted, ct);
        if (group == null) return Result.NotFound<AiAssistantExecutionContextDto>();
        var role = group.OwnerId == userId
            ? Qaly.Application.Services.Groups.GroupRoleRules.Owner
            : group.Members.FirstOrDefault(item => item.UserId == userId)?.Role;
        if (role == null) return Result.NotFound<AiAssistantExecutionContextDto>();

        var capabilities = AuthorizedCapabilities(false, false, systemTier: systemTier);
        if (!capabilities.Any(item => item.CapabilityId == selectedCapabilityId))
            return Result.Success(new AiAssistantExecutionContextDto(capabilities, [],
                [new AiAssistantSourceDisclosureDto("capability.context", "denied",
                    "Quyền AI hiện tại không cho phép đọc hoặc phân tích Group.", ReasonCode: "capability_not_authorized")]));

        var recentMessages = await _db.GroupMessages.AsNoTracking()
            .Where(item => item.WorkGroupId == groupId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                id = item.Id,
                userId = item.UserId,
                content = item.Content,
                messageType = item.MessageType,
                createdAt = item.CreatedAt,
                editedAt = item.EditedAt,
                isPinned = item.IsPinned
            })
            .ToListAsync(ct);
        recentMessages.Reverse();
        var facts = new Dictionary<string, object?>
        {
            ["group_id"] = group.Id,
            ["name"] = group.Name,
            ["status"] = group.Status,
            ["member_count"] = group.Members.Count,
            ["current_user_role"] = role,
            ["recent_messages"] = recentMessages
        };
        var source = Envelope(AiAssistantContextContract.GroupContextSource, $"/groups/{group.Id}", "group",
            group.Name, group.UpdatedAt ?? group.CreatedAt, "canonical", "group_member", facts,
            "ef_core_authorized_group_message_snapshot");
        return Result.Success(new AiAssistantExecutionContextDto(capabilities, [source], [ToDisclosure(source)]));
    }

    private async Task<Result<AiAssistantExecutionContextDto>> ResolveOrganizationLaunchAsync(
        AiAssistantTurnRequestDto request,
        Guid userId,
        bool isAdmin,
        string selectedCapabilityId,
        AiNativeSystemTier systemTier,
        CancellationToken ct)
    {
        if (!_options.ProjectLaunchBriefEnabled ||
            selectedCapabilityId == AiAssistantContextContract.ProjectStaffingPlanCapability && !_options.ProjectLaunchPlanningEnabled ||
            selectedCapabilityId == AiAssistantContextContract.ProjectLaunchExecuteCapability && !_options.ProjectLaunchExecutionEnabled)
        {
            return Result.Failure<AiAssistantExecutionContextDto>(
                "The requested Project launch stage is disabled.", 503, "project_launch_stage_disabled");
        }

        var organizations = await _db.Organizations.AsNoTracking()
            .Include(item => item.Members)
            .Include(item => item.Projects)
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync(ct);
        var readable = organizations.Where(item => CanReadOrganization(item, userId, isAdmin)).ToList();
        var requestedOrganizationId = request.Context?.OrganizationId ??
            (string.Equals(request.Context?.EntityType, "organization", StringComparison.OrdinalIgnoreCase)
                ? request.Context?.EntityId
                : null);
        Organization? organization;
        if (requestedOrganizationId.HasValue)
        {
            organization = readable.SingleOrDefault(item => item.Id == requestedOrganizationId.Value);
            if (organization == null) return Result.NotFound<AiAssistantExecutionContextDto>();
        }
        else
        {
            organization = readable.Count == 1 ? readable[0] : null;
        }

        var canManageOrganization = organization != null
            ? CanManageOrganization(organization, userId, isAdmin)
            : readable.Any(item => CanManageOrganization(item, userId, isAdmin));
        var capabilities = AuthorizedCapabilities(
            canManageProject: false,
            canUseProjectLaunch: readable.Count > 0,
            canManageOrganization,
            systemTier);
        if (organization == null)
        {
            var reason = readable.Count == 0 ? "organization_not_authorized" : "organization_scope_required";
            return Result.Success(new AiAssistantExecutionContextDto(
                capabilities,
                [],
                [new AiAssistantSourceDisclosureDto(
                    "organization.scope",
                    readable.Count == 0 ? "denied" : "required",
                    readable.Count == 0
                        ? "Không có tổ chức được phép đọc cho yêu cầu này."
                        : "Hãy chọn tổ chức áp dụng Rulebook trước khi hoàn tất Launch Brief.",
                    ReasonCode: reason)]));
        }

        var summary = await BuildOrganizationSummarySourceAsync(organization, ct);
        var rulebook = await BuildOrganizationRulebookSourceAsync(organization, ct);
        var sources = new List<AiAssistantContextSourceEnvelopeDto> { summary, rulebook };
        var visibleProjects = organization.Projects
            .Where(item => !item.IsDeleted && item.ArchivedAt == null && CanReadProject(item, userId, isAdmin))
            .ToList();
        if (visibleProjects.Count > 0)
        {
            sources.Add(BuildWorkspaceProjectsSource(visibleProjects));
        }

        return Result.Success(new AiAssistantExecutionContextDto(
            capabilities,
            sources,
            sources.Select(ToDisclosure).ToArray()));
    }

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildOrganizationSummarySourceAsync(
        Organization organization,
        CancellationToken ct)
    {
        var activeMembers = organization.Members.Count;
        var activeProjects = organization.Projects.Count(item => !item.IsDeleted && item.ArchivedAt == null);
        var skills = await _db.OrganizationSkills.AsNoTracking()
            .Where(item => item.OrganizationId == organization.Id && item.IsActive)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.Name, item.Description, item.UpdatedAt, item.CreatedAt })
            .Take(MaxSkillFacts + 1)
            .ToListAsync(ct);
        var facts = new Dictionary<string, object?>
        {
            ["organizationId"] = organization.Id,
            ["name"] = organization.Name,
            ["code"] = organization.Code,
            ["activeMemberCount"] = activeMembers,
            ["activeProjectCount"] = activeProjects,
            ["skills"] = skills.Take(MaxSkillFacts).ToArray()
        };
        return BuildOrganizationEnvelope(
            AiAssistantContextContract.OrganizationSummarySource,
            organization,
            "organization",
            organization.Name,
            skills.Select(item => item.UpdatedAt ?? item.CreatedAt)
                .Append(organization.UpdatedAt ?? organization.CreatedAt).Max(),
            facts,
            skills.Count > MaxSkillFacts ? ["context_limit_applied"] : []);
    }

    private async Task<AiAssistantContextSourceEnvelopeDto> BuildOrganizationRulebookSourceAsync(
        Organization organization,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var effective = await _db.OrganizationWorkRuleSets.AsNoTracking()
            .Where(item => item.OrganizationId == organization.Id && item.Status == "active" &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= now) &&
                (!item.EffectiveUntil.HasValue || item.EffectiveUntil > now))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(ct);
        var facts = new Dictionary<string, object?>
        {
            ["organizationId"] = organization.Id,
            ["status"] = effective == null ? "policy_missing" : "effective",
            ["ruleSetId"] = effective?.Id,
            ["version"] = effective?.Version,
            ["effectiveFrom"] = effective?.EffectiveFrom,
            ["effectiveUntil"] = effective?.EffectiveUntil,
            ["rules"] = effective == null
                ? Array.Empty<object>()
                : JsonSerializer.Deserialize<object[]>(effective.RulesJson, JsonOptions) ?? Array.Empty<object>()
        };
        return BuildOrganizationEnvelope(
            AiAssistantContextContract.OrganizationRulebookSource,
            organization,
            "organization_rulebook",
            effective == null ? "Organization Rulebook chưa được kích hoạt" : $"Organization Rulebook v{effective.Version}",
            effective?.UpdatedAt ?? effective?.CreatedAt ?? organization.UpdatedAt ?? organization.CreatedAt,
            facts,
            effective == null ? ["policy_missing"] : []);
    }

    private async Task<bool> HasReadableOrganizationAsync(Guid userId, bool isAdmin, CancellationToken ct)
        => isAdmin || await _db.Organizations.AsNoTracking().AnyAsync(
            item => item.IsActive && (item.OwnerId == userId || item.Members.Any(member => member.UserId == userId)), ct);

    private async Task<bool> HasManageableOrganizationAsync(Guid userId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        var organizations = await _db.Organizations.AsNoTracking()
            .Include(item => item.Members)
            .Where(item => item.IsActive &&
                (item.OwnerId == userId || item.Members.Any(member => member.UserId == userId)))
            .ToListAsync(ct);
        return organizations.Any(item => CanManageOrganization(item, userId, isAdmin: false));
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
            AiAssistantContextContract.WikiPageSource => await BuildWikiPageSourceAsync(project, context, userId, isAdmin, ct),
            _ => null
        };

    private async Task<AiAssistantContextSourceEnvelopeDto?> BuildWikiPageSourceAsync(
        Project project,
        AiAssistantClientContextDto? context,
        Guid userId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (!string.Equals(context?.EntityType, "wiki", StringComparison.OrdinalIgnoreCase) || context?.EntityId is not Guid wikiId)
            return null;
        var page = await _db.WikiPages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == wikiId && item.ProjectId == project.Id, ct);
        if (page == null) return null;
        if (!isAdmin && page.Visibility == "private" && page.AuthorId != userId) return null;
        var sections = page.Content.Split('\n')
            .Select((line, index) => new { line = line.Trim(), index })
            .Where(item => item.line.StartsWith('#'))
            .Take(30)
            .Select(item => new { heading = item.line.TrimStart('#', ' '), line = item.index + 1 })
            .ToArray();
        var facts = new Dictionary<string, object?>
        {
            ["wiki_id"] = page.Id,
            ["title"] = page.Title,
            ["visibility"] = page.Visibility,
            ["sections"] = sections,
            ["content"] = page.Content.Length <= 12000 ? page.Content : page.Content[..12000]
        };
        return Envelope(AiAssistantContextContract.WikiPageSource,
            $"/projects/{project.Id}/wiki/{page.Id}", "wiki_page", page.Title, page.UpdatedAt,
            "canonical", page.Visibility, facts, "ef_core_wiki_section_snapshot");
    }

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
            .ThenBy(item => item.UserId)
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
                .ThenBy(item => item.Id)
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

    private static AiAssistantContextSourceEnvelopeDto Envelope(
        string sourceId,
        string sourceRef,
        string sourceType,
        string title,
        DateTimeOffset freshness,
        string trustClass,
        string privacyClass,
        IReadOnlyDictionary<string, object?> facts,
        string retrievalMethod)
    {
        var hash = ComputeHash(facts);
        return new AiAssistantContextSourceEnvelopeDto(sourceId, sourceRef, sourceType, title, freshness,
            trustClass, privacyClass, hash, facts, [], retrievalMethod);
    }

    private static AiAssistantContextSourceEnvelopeDto BuildOrganizationEnvelope(
        string sourceId,
        Organization organization,
        string sourceType,
        string title,
        DateTimeOffset freshness,
        IReadOnlyDictionary<string, object?> facts,
        IReadOnlyList<string> redactions)
    {
        var hash = ComputeHash(facts);
        return new AiAssistantContextSourceEnvelopeDto(
            sourceId,
            $"qaly://organization/{organization.Id:D}/{sourceId.Replace('.', '/')}@{hash[7..19]}",
            sourceType,
            title,
            freshness,
            "qaly_domain_record",
            "organization_private",
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
    {
        if (isAdmin) return true;
        if (project.OrganizationId.HasValue)
        {
            if (project.Organization == null || !project.Organization.IsActive) return false;
            if (project.Organization.OwnerId == userId) return true;
            var organizationRole = project.Organization.Members
                .Where(member => member.UserId == userId)
                .Select(member => member.Role)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(organizationRole)) return false;
            if (OrganizationRoleRules.CanManageOrganization(organizationRole)) return true;
        }
        return project.OwnerId == userId || project.Members.Any(member => member.UserId == userId);
    }

    private static bool CanReadOrganization(Organization organization, Guid userId, bool isAdmin)
        => isAdmin || organization.OwnerId == userId ||
           organization.Members.Any(member => member.UserId == userId);

    private static bool CanManageOrganization(Organization organization, Guid userId, bool isAdmin)
        => isAdmin || organization.OwnerId == userId ||
           organization.Members.Any(member => member.UserId == userId &&
               OrganizationRoleRules.CanManageOrganization(member.Role));

    private Task<AiNativeSystemTier> ResolveSystemTierAsync(Guid userId, CancellationToken ct)
        => _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct);

    private Task<AiNativeProjectAuthorization> ResolveProjectAuthorizationAsync(
        Project project,
        Guid userId,
        bool isAdmin,
        CancellationToken ct)
        => _authorization.ResolveProjectAsync(project, userId, isAdmin, ct);

    private static Result<AiAssistantExecutionContextDto> RestrictedContext()
        => Result.Success(new AiAssistantExecutionContextDto(
            [],
            [],
            [new AiAssistantSourceDisclosureDto(
                "capability.context",
                "denied",
                "Quyền sử dụng Trợ lý AI đã bị giới hạn bởi quản trị viên hệ thống.",
                ReasonCode: "ai_hub_restricted")]));

    private static Guid? ResolveProjectId(AiAssistantClientContextDto? context)
        => context?.ProjectId ??
           (string.Equals(context?.EntityType, "project", StringComparison.OrdinalIgnoreCase)
               ? context?.EntityId
               : null);

    private Task<Guid?> ResolveMeetingProjectIdAsync(Guid meetingEntityId, CancellationToken ct)
        => _db.MeetingImports.AsNoTracking()
            .Where(item => item.Id == meetingEntityId || item.SourceId == meetingEntityId.ToString())
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => (Guid?)item.ProjectId)
            .FirstOrDefaultAsync(ct);

    private static bool IsExplicitGroupReadRequest(string message)
    {
        var hasGroupTarget = message.Contains("group", StringComparison.OrdinalIgnoreCase) ||
                             message.Contains("nhóm", StringComparison.OrdinalIgnoreCase);
        var hasProjectTarget = message.Contains("project", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("dự án", StringComparison.OrdinalIgnoreCase);
        return hasGroupTarget && !hasProjectTarget;
    }

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
            : capabilityId == AiAssistantContextContract.WikiBriefTaskCapability
                ? new List<string> { AiAssistantContextContract.WikiPageSource }
            : capabilityId is AiAssistantContextContract.AcceptanceChecklistCapability or AiAssistantContextContract.TaskBreakdownCapability
                ? new List<string> { AiAssistantContextContract.TaskDetailSource, AiAssistantContextContract.ProjectTasksSource }
            : capabilityId == AiAssistantContextContract.ProjectDigestCapability
                ? new List<string> { AiAssistantContextContract.ProjectSummarySource, AiAssistantContextContract.ProjectTasksSource }
            : capabilityId == AiAssistantContextContract.MeetingActionsCapability
                ? new List<string> { AiAssistantContextContract.ProjectTasksSource }
            : capabilityId == AiAssistantContextContract.RoadmapAdjustCapability
                ? new List<string> { AiAssistantContextContract.ProjectSummarySource, AiAssistantContextContract.ProjectTasksSource, AiAssistantContextContract.ProjectWorkloadSource }
            : capabilityId == AiAssistantContextContract.SkillEvidenceCapability
                ? new List<string> { AiAssistantContextContract.TaskDetailSource, AiAssistantContextContract.ProjectSkillsSource }
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
        if (string.Equals(context?.EntityType, "wiki", StringComparison.OrdinalIgnoreCase) &&
            context?.EntityId.HasValue == true)
        {
            sourceIds.Add(AiAssistantContextContract.WikiPageSource);
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
