using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class ProjectLaunchOrchestratorService
{
    private async Task<StaffingBuildResult> BuildStaffingScenariosAsync(
        Organization organization,
        ProjectLaunchBriefDto brief,
        IReadOnlyList<OrganizationWorkRuleDto> rules,
        ProjectLaunchModelOutputDto modelPlan,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string sourceVersionHash,
        CancellationToken ct)
    {
        var memberRows = await _db.OrganizationMembers.AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.OrganizationId == organization.Id && item.User.IsActive)
            .OrderBy(item => item.UserId)
            .ToListAsync(ct);
        if (memberRows.All(item => item.UserId != organization.OwnerId))
        {
            var owner = await _db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == organization.OwnerId && item.IsActive, ct);
            if (owner != null)
                memberRows.Add(new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner, User = owner });
        }
        var userIds = memberRows.Select(item => item.UserId).Distinct().ToArray();
        var profiles = await _db.OrganizationMemberCapacityProfiles.AsNoTracking()
            .Include(item => item.AvailabilityWindows)
            .Where(item => item.OrganizationId == organization.Id && userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, ct);
        var activeProjects = await _db.ProjectMembers.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId) && item.Project.OrganizationId == organization.Id &&
                !item.Project.IsDeleted && item.Project.Status != "Archived")
            .GroupBy(item => item.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Select(item => item.ProjectId).Distinct().Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, ct);
        var commitments = await _db.TaskItems.AsNoTracking()
            .Where(item => item.AssigneeId.HasValue && userIds.Contains(item.AssigneeId.Value) &&
                item.Project.OrganizationId == organization.Id && !item.Project.IsDeleted && !item.IsDeleted &&
                item.Status != "Done" && item.Status != "Completed" &&
                (!item.StartDate.HasValue || item.StartDate < windowEnd) && (!item.DueDate.HasValue || item.DueDate >= windowStart))
            .GroupBy(item => item.AssigneeId!.Value)
            .Select(group => new { UserId = group.Key, Hours = group.Sum(item => (decimal)(item.EstimatedHours ?? 8)) })
            .ToDictionaryAsync(item => item.UserId, item => item.Hours, ct);
        var evidenceRows = await _db.TaskCompletionAttributions.AsNoTracking()
            .Include(item => item.TaskItem).ThenInclude(item => item.SkillRequirements).ThenInclude(item => item.OrganizationSkill)
            .Where(item => userIds.Contains(item.ContributorUserId) && item.Status == TaskCompletionAttribution.Confirmed &&
                item.TaskItem.Project.OrganizationId == organization.Id && !item.TaskItem.IsDeleted)
            .ToListAsync(ct);
        var evidenceByUser = evidenceRows
            .GroupBy(item => item.ContributorUserId)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(item => item.TaskItem.SkillRequirements)
                    .GroupBy(item => Normalize(item.OrganizationSkill.Name), StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Count(), StringComparer.Ordinal));

        var focusReservePercent = NumericRule(rules, "focus_reserve_percent", 15m);
        var maxUtilizationPercent = NumericRule(rules, "max_utilization_percent", 85m);
        var maxConcurrentProjects = (int)NumericRule(rules, "max_active_projects", 3m);
        var managerRoleRule = rules.FirstOrDefault(item => item.Enabled && item.RuleKey == "manager_roles");
        var durationDays = Math.Max(1m, (decimal)(windowEnd - windowStart).TotalDays);
        var weeks = Math.Max(1m, decimal.Ceiling(durationDays / 7m));
        var candidates = new List<CandidateFacts>();
        foreach (var member in memberRows.GroupBy(item => item.UserId).Select(group => group.First()))
        {
            profiles.TryGetValue(member.UserId, out var profile);
            var weekly = profile?.WeeklyCapacityHours ?? 0m;
            var windowCapacity = weekly * weeks;
            var unavailable = profile == null ? 0m : CalculateAvailabilityReduction(profile, windowStart, windowEnd, weekly);
            var committed = commitments.GetValueOrDefault(member.UserId);
            var reserve = Math.Round(windowCapacity * focusReservePercent / 100m, 2);
            var available = Math.Max(0m, windowCapacity - unavailable - committed - reserve);
            var activeCount = activeProjects.GetValueOrDefault(member.UserId);
            var orgRole = organization.OwnerId == member.UserId ? OrganizationRoleRules.Owner : OrganizationRoleRules.Normalize(member.Role);
            var managerEligible = managerRoleRule?.Values is { Count: > 0 }
                ? managerRoleRule.Values.Any(role => string.Equals(OrganizationRoleRules.Normalize(role), orgRole, StringComparison.Ordinal))
                : organization.OwnerId == member.UserId || OrganizationRoleRules.CanManageOrganization(orgRole);
            var hardRejects = new List<string>();
            if (profile == null) hardRejects.Add("missing_capacity_profile");
            if (activeCount >= maxConcurrentProjects) hardRejects.Add("max_concurrent_projects_reached");
            if (available <= 0) hardRejects.Add("no_effective_capacity");
            if (profile?.AvailabilityWindows.Any(item => item.Kind == MemberAvailabilityWindow.Unavailable &&
                    item.StartsAt <= windowStart && item.EndsAt >= windowEnd) == true)
                hardRejects.Add("unavailable_for_full_window");
            var evidence = evidenceByUser.GetValueOrDefault(member.UserId) ?? new Dictionary<string, int>(StringComparer.Ordinal);
            var confidence = evidence.Count == 0 ? 0m : Math.Min(0.95m, 0.45m + evidence.Values.Sum() * 0.08m);
            candidates.Add(new CandidateFacts(
                member.UserId,
                member.User.FullName,
                orgRole,
                managerEligible,
                hardRejects.Count == 0,
                hardRejects,
                evidence,
                confidence,
                weekly,
                windowCapacity,
                committed,
                reserve,
                available,
                activeCount,
                profile?.TimeZoneId ?? "unknown",
                profile == null ? "missing" : "declared"));
        }

        var requiredSkillNames = modelPlan.Sprints.SelectMany(item => item.Tasks)
            .SelectMany(item => item.RequiredSkillNames)
            .Select(Normalize)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var catalog = await _db.OrganizationSkills.AsNoTracking()
            .Where(item => item.OrganizationId == organization.Id && item.IsActive)
            .ToDictionaryAsync(item => Normalize(item.Name), item => item.Name, StringComparer.Ordinal, ct);
        var unknownCatalogSkills = requiredSkillNames.Where(item => !catalog.ContainsKey(item)).ToArray();
        var knownRequiredSkills = requiredSkillNames.Where(catalog.ContainsKey).ToArray();
        var totalHours = modelPlan.Sprints.SelectMany(item => item.Tasks).Sum(item => item.EstimatedHours);
        var managerOptions = candidates.Where(item => item.ManagerEligible && item.StaffingEligible)
            .OrderByDescending(item => item.AvailableHours)
            .ThenByDescending(item => item.EvidenceConfidence)
            .ThenBy(item => item.ActiveProjectCount)
            .ThenBy(item => item.UserId)
            .Take(2)
            .ToArray();
        var scenarios = new List<ProjectStaffingScenarioDto>();
        if (managerOptions.Length == 0)
        {
            scenarios.Add(BuildScenario(null, "balanced", "Cân bằng", candidates, knownRequiredSkills,
                unknownCatalogSkills, catalog, totalHours, maxUtilizationPercent, maxConcurrentProjects,
                focusReservePercent, sourceVersionHash, brief.SourceRefs));
        }
        else
        {
            for (var index = 0; index < managerOptions.Length; index++)
            {
                var manager = managerOptions[index];
                scenarios.Add(BuildScenario(
                    manager,
                    index == 0 ? "balanced" : $"manager-alternative-{index}",
                    index == 0 ? "Cân bằng" : $"Phương án manager thay thế: {manager.DisplayName}",
                    candidates,
                    knownRequiredSkills,
                    unknownCatalogSkills,
                    catalog,
                    totalHours,
                    maxUtilizationPercent,
                    maxConcurrentProjects,
                    focusReservePercent,
                    sourceVersionHash,
                    brief.SourceRefs));
            }
        }
        var warnings = new List<string>();
        if (candidates.Any(item => item.CapacityState == "missing"))
            warnings.Add("Thành viên thiếu capacity profile bị loại khỏi phương án khả thi; Qaly không đổi missing capacity thành thời gian trống.");
        if (commitments.Count > 0)
            warnings.Add("Cross-Project load được dùng dưới dạng tổng hợp; tên Task/Project riêng tư không được đưa vào staffing artifact.");
        if (unknownCatalogSkills.Length > 0)
            warnings.Add("Kỹ năng model đề xuất nhưng không có trong Organization catalog được giữ là skill gap, không được tự tạo thành evidence.");
        return new StaffingBuildResult(scenarios, warnings);
    }

    private static ProjectStaffingScenarioDto BuildScenario(
        CandidateFacts? manager,
        string scenarioId,
        string title,
        IReadOnlyList<CandidateFacts> candidates,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<string> unknownCatalogSkills,
        IReadOnlyDictionary<string, string> catalog,
        int totalHours,
        decimal maxUtilizationPercent,
        int maxConcurrentProjects,
        decimal focusReservePercent,
        string sourceVersionHash,
        IReadOnlyList<string> briefSourceRefs)
    {
        var selected = new Dictionary<Guid, CandidateFacts>();
        if (manager != null) selected[manager.UserId] = manager;
        var missing = new List<string>(unknownCatalogSkills.Select(item => catalog.GetValueOrDefault(item, item)));
        foreach (var skill in requiredSkills)
        {
            var candidate = candidates.Where(item => item.StaffingEligible && item.Evidence.ContainsKey(skill))
                .OrderByDescending(item => item.Evidence[skill])
                .ThenByDescending(item => item.AvailableHours)
                .ThenBy(item => item.ActiveProjectCount)
                .ThenBy(item => item.UserId)
                .FirstOrDefault();
            if (candidate == null) missing.Add(catalog.GetValueOrDefault(skill, skill));
            else selected[candidate.UserId] = candidate;
        }
        if (selected.Count == 1 && totalHours > selected.Values.Sum(item => item.AvailableHours))
        {
            var additional = candidates.Where(item => item.StaffingEligible && !selected.ContainsKey(item.UserId))
                .OrderByDescending(item => item.AvailableHours).ThenBy(item => item.ActiveProjectCount).FirstOrDefault();
            if (additional != null) selected[additional.UserId] = additional;
        }

        var totalAvailable = selected.Values.Sum(item => item.AvailableHours);
        var members = selected.Values.Select(item =>
        {
            var share = totalAvailable <= 0 ? 0 : Math.Min(item.AvailableHours, Math.Round(totalHours * item.AvailableHours / totalAvailable, 2));
            var covered = requiredSkills.Where(item.Evidence.ContainsKey).Select(skill => catalog.GetValueOrDefault(skill, skill)).ToArray();
            var loadAfter = item.WindowCapacityHours <= 0 ? 100m : Math.Round((item.ExistingCommittedHours + share) * 100m / item.WindowCapacityHours, 2);
            return new ProjectStaffingMemberDto(
                item.UserId,
                item.DisplayName,
                manager?.UserId == item.UserId ? ProjectRoleRules.Manager : ResolveDeliveryRole(covered),
                share,
                covered,
                requiredSkills.Where(skill => !item.Evidence.ContainsKey(skill)).Select(skill => catalog.GetValueOrDefault(skill, skill)).ToArray(),
                loadAfter,
                ["active_organization_membership", "declared_capacity", covered.Length > 0 ? "confirmed_skill_evidence" : "capacity_support"]);
        }).ToArray();
        var overUtilized = members.Where(item => item.LoadAfterPercent > maxUtilizationPercent).ToArray();
        var blocking = new List<string>();
        if (manager == null) blocking.Add("Không có manager đáp ứng role eligibility, capacity và concurrent-Project policy.");
        if (missing.Count > 0) blocking.Add($"Thiếu skill evidence đã xác nhận: {string.Join(", ", missing.Distinct(StringComparer.OrdinalIgnoreCase))}.");
        if (totalAvailable < totalHours) blocking.Add($"Effective capacity thiếu {Math.Ceiling(totalHours - totalAvailable)} giờ trong timebox.");
        if (overUtilized.Length > 0) blocking.Add("Phương án vượt max utilization của Rulebook.");
        if (members.Length > 1 && members.All(item => item.ProposedRole == ProjectRoleRules.Manager))
            blocking.Add("Chưa có delivery-role coverage tách biệt với manager.");
        var feasible = blocking.Count == 0;
        var score = feasible
            ? Math.Max(0m, 100m - members.Average(item => item.LoadAfterPercent) * 0.35m - (decimal)members.Average(item => item.MissingSkills.Count) * 10m)
            : 0m;
        var sourceRefs = briefSourceRefs.Concat([
                $"qaly://organization/staffing-capacity@{sourceVersionHash[..12]}",
                $"qaly://organization/member-skill-aggregate@{sourceVersionHash[..12]}"
            ])
            .Distinct(StringComparer.Ordinal).ToArray();
        var decisions = new[]
        {
            Decision("manager_role_eligibility", manager == null ? "block" : "pass", manager == null ? "Không có manager vượt hard gates." : $"{manager.DisplayName} có Organization role hợp lệ.", sourceVersionHash),
            Decision("max_active_projects", candidates.Any(item => item.HardRejects.Contains("max_concurrent_projects_reached")) ? "warning" : "pass", $"Giới hạn áp dụng: {maxConcurrentProjects} active Projects.", sourceVersionHash),
            Decision("max_utilization_percent", overUtilized.Length > 0 ? "block" : "pass", $"Không vượt ngưỡng {maxUtilizationPercent}% sau phân bổ.", sourceVersionHash),
            Decision("focus_reserve_percent", "pass", $"Đã giữ {focusReservePercent}% focus/context-switch reserve trước khi tính available hours.", sourceVersionHash),
            Decision("required_skill_coverage", missing.Count > 0 ? "block" : "pass", missing.Count > 0 ? "Thiếu evidence cho một hoặc nhiều kỹ năng." : "Các kỹ năng có Organization catalog và evidence đã xác nhận.", sourceVersionHash),
            Decision("fairness_load_distribution", feasible ? "pass" : "warning", "Xếp hạng ưu tiên effective capacity, evidence, ít active Projects và ID ổn định; không dùng personality/performance inference.", sourceVersionHash)
        };
        var managerCandidates = candidates.Select(item => ToCandidateDto(
            item,
            members.FirstOrDefault(member => member.UserId == item.UserId)?.ProposedHours ?? 0m)).ToArray();
        return new(
            scenarioId,
            title,
            "Phương án server quyết định theo hard gates trước, sau đó mới xếp hạng soft trade-off.",
            feasible,
            Math.Round(score, 2),
            manager?.UserId,
            manager?.DisplayName,
            members,
            managerCandidates,
            missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            blocking,
            feasible ? [] : ["Deadline hoặc team composition hiện chưa khả thi; cần sửa input/rule/capacity trước khi confirm."],
            ["Estimate model là proposal và đã được kiểm capacity theo tổng giờ."],
            decisions,
            sourceRefs,
            AiProjectOrchestrationContract.ScoringVersion);
    }

    private static OrganizationWorkRuleDecisionDto Decision(string key, string result, string explanation, string sourceHash)
        => new(
            AiProjectLaunchContract.RuleDecisionSchemaId,
            null,
            null,
            key,
            result,
            result == "block" ? "block" : "warn",
            explanation,
            new Dictionary<string, object?> { ["sourceVersionHash"] = sourceHash },
            result != "pass",
            DateTimeOffset.UtcNow.ToString("O"));

    private static ProjectStaffingCandidateDto ToCandidateDto(CandidateFacts item, decimal proposedHours)
    {
        var loadAfter = item.WindowCapacityHours <= 0
            ? 100m
            : Math.Round((item.ExistingCommittedHours + proposedHours) * 100m / item.WindowCapacityHours, 2);
        return new(
            item.UserId,
            item.DisplayName,
            item.OrganizationRole,
            item.ManagerEligible,
            item.StaffingEligible,
            item.HardRejects,
            item.Evidence.Keys.ToArray(),
            item.EvidenceConfidence,
            item.WeeklyCapacityHours,
            item.WindowCapacityHours,
            item.ExistingCommittedHours,
            item.FocusReserveHours,
            item.AvailableHours,
            proposedHours,
            loadAfter,
            item.ActiveProjectCount,
            item.TimeZoneId,
            item.CapacityState,
            [$"qaly://organization/member-capacity/{item.UserId}", $"qaly://organization/member-skill-aggregate/{item.UserId}"]);
    }

    private static decimal CalculateAvailabilityReduction(
        OrganizationMemberCapacityProfile profile,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        decimal weeklyCapacity)
    {
        decimal reduction = 0;
        foreach (var window in profile.AvailabilityWindows.Where(item => item.StartsAt < windowEnd && item.EndsAt > windowStart))
        {
            var start = window.StartsAt > windowStart ? window.StartsAt : windowStart;
            var end = window.EndsAt < windowEnd ? window.EndsAt : windowEnd;
            var days = Math.Max(0m, (decimal)(end - start).TotalDays);
            var nominal = weeklyCapacity * days / 7m;
            reduction += window.Kind == MemberAvailabilityWindow.Unavailable
                ? nominal
                : Math.Max(0m, nominal - (window.AvailableHours ?? 0m));
        }
        return Math.Round(reduction, 2);
    }

    private static decimal NumericRule(IReadOnlyList<OrganizationWorkRuleDto> rules, string key, decimal fallback)
        => rules.FirstOrDefault(item => item.Enabled && item.RuleKey == key)?.NumericValue is decimal value
            ? value
            : fallback;

    private static string ResolveDeliveryRole(IReadOnlyList<string> coveredSkills)
    {
        var normalized = string.Join(' ', coveredSkills.Select(Normalize));
        if (normalized.Contains("test", StringComparison.Ordinal) || normalized.Contains("qa", StringComparison.Ordinal)) return ProjectRoleRules.Tester;
        if (normalized.Contains("review", StringComparison.Ordinal) || normalized.Contains("security", StringComparison.Ordinal)) return ProjectRoleRules.Reviewer;
        return ProjectRoleRules.Developer;
    }

    private sealed record CandidateFacts(
        Guid UserId,
        string DisplayName,
        string OrganizationRole,
        bool ManagerEligible,
        bool StaffingEligible,
        IReadOnlyList<string> HardRejects,
        IReadOnlyDictionary<string, int> Evidence,
        decimal EvidenceConfidence,
        decimal WeeklyCapacityHours,
        decimal WindowCapacityHours,
        decimal ExistingCommittedHours,
        decimal FocusReserveHours,
        decimal AvailableHours,
        int ActiveProjectCount,
        string TimeZoneId,
        string CapacityState);
}
