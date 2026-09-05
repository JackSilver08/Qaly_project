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
            .Where(item => userIds.Contains(item.UserId) &&
                !item.Project.IsDeleted && item.Project.Status != "Archived")
            .GroupBy(item => item.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Select(item => item.ProjectId).Distinct().Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, ct);
        var commitmentRows = await _db.TaskAssignments.AsNoTracking()
            .Where(assignment => userIds.Contains(assignment.UserId) &&
                !assignment.TaskItem.Project.IsDeleted && !assignment.TaskItem.IsDeleted &&
                assignment.TaskItem.Status != "Done" && assignment.TaskItem.Status != "Completed" &&
                (!assignment.TaskItem.StartDate.HasValue || assignment.TaskItem.StartDate < windowEnd) &&
                (!assignment.TaskItem.DueDate.HasValue || assignment.TaskItem.DueDate >= windowStart))
            .OrderBy(assignment => assignment.UserId)
            .ThenBy(assignment => assignment.TaskItem.StartDate)
            .ThenBy(assignment => assignment.TaskItem.DueDate)
            .ThenBy(assignment => assignment.TaskItemId)
            .Select(assignment => new CommitmentFacts(
                assignment.UserId,
                assignment.TaskItem.StartDate,
                assignment.TaskItem.DueDate,
                assignment.TaskItem.EstimatedHours ?? 8))
            .ToListAsync(ct);
        var commitments = commitmentRows
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(item => (decimal)item.EstimatedHours));
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
        var professionalProfileRows = await _db.OrganizationMemberProfessionalProfiles.AsNoTracking()
            .Include(item => item.ProfessionalProfileDefinition)
            .Where(item => item.OrganizationId == organization.Id
                && userIds.Contains(item.UserId)
                && item.VerificationStatus == OrganizationMemberProfessionalProfile.Verified
                && item.ProfessionalProfileDefinition.IsActive
                && item.EffectiveFrom < windowEnd
                && (!item.EffectiveTo.HasValue || item.EffectiveTo > windowStart))
            .OrderBy(item => item.UserId)
            .ThenBy(item => item.ProfessionalProfileDefinition.Key)
            .ToListAsync(ct);
        var professionalProfilesByUser = professionalProfileRows
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ProfessionalProfileDefinition.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));

        var focusReservePercent = NumericRule(rules, "focus_reserve_percent", 15m);
        var maxUtilizationPercent = NumericRule(rules, "max_utilization_percent", 85m);
        var reviewerCoordinationOverheadPercent = NumericRule(rules, "reviewer_coordination_overhead_percent", 10m);
        var maxConcurrentProjects = (int)NumericRule(rules, "max_active_projects", 3m);
        var managerRoleRule = rules.FirstOrDefault(item => item.Enabled && item.RuleKey == "manager_roles");
        var defaultManagerProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "product-project-manager",
            "scrum-master-agile-coach"
        };
        var managerRuleValues = managerRoleRule?.Values ?? [];
        var configuredManagerProfiles = managerRuleValues.Count > 0
            ? managerRuleValues.Select(ProfessionalProfileCatalog.ToKey)
                .Where(value => value.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : defaultManagerProfiles;
        var legacyManagerAccessRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            OrganizationRoleRules.Owner,
            OrganizationRoleRules.OrganizationAdmin,
            "Admin",
            "Manager"
        };
        var candidates = new List<CandidateFacts>();
        foreach (var member in memberRows.GroupBy(item => item.UserId).Select(group => group.First()))
        {
            profiles.TryGetValue(member.UserId, out var profile);
            var weekly = profile?.WeeklyCapacityHours ?? 0m;
            var weeklyCapacity = BuildWeeklyCapacityFacts(
                profile,
                commitmentRows.Where(item => item.UserId == member.UserId).ToArray(),
                windowStart,
                windowEnd,
                focusReservePercent);
            var windowCapacity = weeklyCapacity.Sum(item => item.DeclaredCapacityHours);
            var committed = weeklyCapacity.Sum(item => item.ExistingCommittedHours);
            var reserve = weeklyCapacity.Sum(item => item.FocusReserveHours);
            var available = weeklyCapacity.Sum(item => item.EffectiveAvailableHours);
            var activeCount = activeProjects.GetValueOrDefault(member.UserId);
            var orgRole = organization.OwnerId == member.UserId ? OrganizationRoleRules.Owner : OrganizationRoleRules.Normalize(member.Role);
            var professionalProfiles = professionalProfilesByUser.GetValueOrDefault(member.UserId)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var managerProfileEligible = professionalProfiles.Overlaps(configuredManagerProfiles);
            // Backward-compatible work rules may still name access roles. They are explicit policy,
            // never inferred from the person's profile and never modify authorization.
            var explicitLegacyRoleEligible = managerRuleValues.Any(role =>
                legacyManagerAccessRoles.Contains(role.Trim())
                && string.Equals(OrganizationRoleRules.Normalize(role), orgRole, StringComparison.Ordinal));
            var managerEligible = managerProfileEligible || explicitLegacyRoleEligible;
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
                professionalProfiles,
                managerProfileEligible,
                confidence,
                weekly,
                windowCapacity,
                committed,
                reserve,
                available,
                activeCount,
                profile?.TimeZoneId ?? "unknown",
                profile == null ? "missing" : "declared",
                weeklyCapacity));
        }

        var requiredSkillNames = modelPlan.Sprints.SelectMany(item => item.Tasks)
            .SelectMany(item => item.RequiredSkillNames)
            .Concat(brief.Features?.Where(IsInScopeFeature).SelectMany(item => item.RequiredSkillNames) ?? [])
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
            .OrderByDescending(item => item.ManagerProfileEligible)
            .ThenByDescending(item => item.AvailableHours)
            .ThenByDescending(item => item.EvidenceConfidence)
            .ThenBy(item => item.ActiveProjectCount)
            .ThenBy(item => item.UserId)
            .Take(3)
            .ToArray();
        var eligibleCount = candidates.Count(item => item.StaffingEligible);
        var averageAvailable = candidates.Where(item => item.StaffingEligible)
            .Select(item => item.AvailableHours)
            .DefaultIfEmpty(1m)
            .Average();
        var balancedTeamSize = Math.Clamp(
            (int)Math.Ceiling(totalHours / Math.Max(1m, averageAvailable)),
            Math.Min(2, Math.Max(1, eligibleCount)),
            Math.Max(1, eligibleCount));
        var leanTeamSize = Math.Min(Math.Max(1, eligibleCount), Math.Max(1, balancedTeamSize - 1));
        var acceleratedTeamSize = Math.Min(Math.Max(1, eligibleCount), balancedTeamSize + 1);
        var scenarios = new List<ProjectStaffingScenarioDto>();
        var primaryManager = managerOptions.FirstOrDefault();
        var variants = new[]
        {
            (Id: "lean", Title: "Gọn nhẹ", Size: leanTeamSize),
            (Id: "balanced", Title: "Cân bằng", Size: balancedTeamSize),
            (Id: "accelerated", Title: "Tăng tốc", Size: acceleratedTeamSize)
        };
        foreach (var variant in variants.DistinctBy(item => (item.Id, item.Size)))
            scenarios.Add(BuildScenario(primaryManager, variant.Id, variant.Title, variant.Size, candidates,
                knownRequiredSkills, unknownCatalogSkills, catalog, totalHours, maxUtilizationPercent,
                maxConcurrentProjects, focusReservePercent, reviewerCoordinationOverheadPercent,
                sourceVersionHash, brief.SourceRefs));
        var warnings = new List<string>();
        if (candidates.Any(item => item.CapacityState == "missing"))
            warnings.Add("Thành viên thiếu capacity profile bị loại khỏi phương án khả thi; Qaly không đổi missing capacity thành thời gian trống.");
        if (commitments.Count > 0)
            warnings.Add("Cross-Project load được phân bổ theo từng tuần; tên Task/Project riêng tư không được đưa vào staffing artifact.");
        warnings.Add($"Capacity đã giữ {reviewerCoordinationOverheadPercent:0.#}% cho review và coordination; đây là giờ nội bộ, không phải external calendar.");
        if (candidates.All(item => !item.ManagerProfileEligible))
            warnings.Add("Chưa có professional profile quản lý dự án đã xác minh; access role không được tự coi là năng lực nghề nghiệp trừ khi Rulebook ghi rõ tương thích legacy.");
        if (unknownCatalogSkills.Length > 0)
            warnings.Add("Kỹ năng model đề xuất nhưng không có trong Organization catalog được giữ là skill gap, không được tự tạo thành evidence.");
        return new StaffingBuildResult(scenarios, warnings);
    }

    private static ProjectStaffingScenarioDto BuildScenario(
        CandidateFacts? manager,
        string scenarioId,
        string title,
        int desiredTeamSize,
        IReadOnlyList<CandidateFacts> candidates,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<string> unknownCatalogSkills,
        IReadOnlyDictionary<string, string> catalog,
        int totalHours,
        decimal maxUtilizationPercent,
        int maxConcurrentProjects,
        decimal focusReservePercent,
        decimal reviewerCoordinationOverheadPercent,
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
        while (selected.Count < desiredTeamSize)
        {
            var additional = candidates.Where(item => item.StaffingEligible && !selected.ContainsKey(item.UserId))
                .OrderByDescending(item => item.AvailableHours).ThenBy(item => item.ActiveProjectCount).FirstOrDefault();
            if (additional == null) break;
            selected[additional.UserId] = additional;
        }

        var totalAvailable = selected.Values.Sum(item => item.AvailableHours);
        var coordinationOverhead = Math.Round(totalHours * reviewerCoordinationOverheadPercent / 100m, 2);
        var members = selected.Values.Select(item =>
        {
            var share = totalAvailable <= 0 ? 0 : Math.Min(item.AvailableHours, Math.Round(totalHours * item.AvailableHours / totalAvailable, 2));
            var overheadShare = manager?.UserId == item.UserId ? coordinationOverhead : 0m;
            var covered = requiredSkills.Where(item.Evidence.ContainsKey).Select(skill => catalog.GetValueOrDefault(skill, skill)).ToArray();
            var weeklyAllocation = BuildProportionalWeeklyAllocation(item.WeeklyCapacity, share, overheadShare);
            var loadAfter = weeklyAllocation.Select(week => week.LoadAfterPercent).DefaultIfEmpty(100m).Max();
            return new ProjectStaffingMemberDto(
                item.UserId,
                item.DisplayName,
                manager?.UserId == item.UserId ? ProjectRoleRules.Manager : ResolveDeliveryRole(covered),
                share,
                covered,
                requiredSkills.Where(skill => !item.Evidence.ContainsKey(skill)).Select(skill => catalog.GetValueOrDefault(skill, skill)).ToArray(),
                loadAfter,
                ["active_organization_membership", "declared_weekly_capacity", covered.Length > 0 ? "confirmed_skill_evidence" : "capacity_support",
                    manager?.UserId == item.UserId && item.ManagerProfileEligible ? "verified_professional_profile" : "delivery_fit_only"],
                overheadShare,
                weeklyAllocation);
        }).ToArray();
        var blocking = new List<string>();
        if (manager == null) blocking.Add("Không có manager đáp ứng role eligibility, capacity và concurrent-Project policy.");
        if (missing.Count > 0) blocking.Add($"Thiếu skill evidence đã xác nhận: {string.Join(", ", missing.Distinct(StringComparer.OrdinalIgnoreCase))}.");
        if (totalAvailable < totalHours + coordinationOverhead)
            blocking.Add($"Effective capacity thiếu {Math.Ceiling(totalHours + coordinationOverhead - totalAvailable)} giờ sau khi tính review/coordination overhead.");
        if (members.Length > 1 && members.All(item => item.ProposedRole == ProjectRoleRules.Manager))
            blocking.Add("Chưa có delivery-role coverage tách biệt với manager.");
        var feasible = blocking.Count == 0;
        var score = feasible
            ? Math.Max(0m, 100m - members.Average(item => item.LoadAfterPercent) * 0.35m - (decimal)members.Average(item => item.MissingSkills.Count) * 10m)
            : 0m;
        var sourceRefs = briefSourceRefs.Concat([
                $"qaly://organization/staffing-capacity@{sourceVersionHash[..12]}",
                $"qaly://organization/member-skill-aggregate@{sourceVersionHash[..12]}",
                $"qaly://organization/member-professional-profile@{sourceVersionHash[..12]}"
            ])
            .Distinct(StringComparer.Ordinal).ToArray();
        var decisions = new[]
        {
            Decision("manager_professional_eligibility", manager == null ? "block" : "pass", manager == null ? "Không có manager vượt professional-profile/policy và capacity hard gates." : manager.ManagerProfileEligible ? $"{manager.DisplayName} có professional profile quản lý đã xác minh." : $"{manager.DisplayName} được phép bởi manager_roles legacy trong Rulebook; access role tự thân không phải bằng chứng nghề nghiệp.", sourceVersionHash),
            Decision("max_active_projects", candidates.Any(item => item.HardRejects.Contains("max_concurrent_projects_reached")) ? "warning" : "pass", $"Giới hạn áp dụng: {maxConcurrentProjects} active Projects.", sourceVersionHash),
            Decision("max_utilization_percent", "warning", $"Ngưỡng {maxUtilizationPercent}% sẽ được kiểm tra lại theo lịch Sprint/Task chính xác.", sourceVersionHash),
            Decision("focus_reserve_percent", "pass", $"Đã giữ {focusReservePercent}% focus/context-switch reserve trước khi tính available hours.", sourceVersionHash),
            Decision("reviewer_coordination_overhead_percent", "warning", $"Đã dự trù {reviewerCoordinationOverheadPercent}% ({coordinationOverhead:0.##} giờ); reviewer thực tế sẽ được kiểm tra theo Task.", sourceVersionHash),
            Decision("time_phased_weekly_capacity", "warning", "Capacity sẽ được đối chiếu theo lịch Sprint/Task; external calendar không được giả lập.", sourceVersionHash),
            Decision("required_skill_coverage", missing.Count > 0 ? "block" : "pass", missing.Count > 0 ? "Thiếu evidence cho một hoặc nhiều kỹ năng." : "Các kỹ năng có Organization catalog và evidence đã xác nhận.", sourceVersionHash),
            Decision("fairness_load_distribution", feasible ? "pass" : "warning", "Xếp hạng ưu tiên effective capacity, evidence, ít active Projects và ID ổn định; không dùng personality/performance inference.", sourceVersionHash)
        };
        var managerCandidates = candidates.Select(item => ToCandidateDto(
            item,
            members.FirstOrDefault(member => member.UserId == item.UserId)?.ProposedHours ?? 0m)).ToArray();
        return new(
            scenarioId,
            title,
            $"Quy mô đề xuất {members.Length} người; kiểm tra hard constraints trước rồi mới cân bằng kỹ năng, tải và thời hạn.",
            feasible,
            Math.Round(score, 2),
            manager?.UserId,
            manager?.DisplayName,
            members,
            managerCandidates,
            missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            blocking,
            feasible ? [] : ["Deadline hoặc team composition hiện chưa khả thi; cần sửa input/rule/capacity trước khi confirm."],
            ["Estimate model là proposal; capacity được kiểm theo từng tuần từ dữ liệu Qaly khai báo nội bộ, không suy diễn external calendar."],
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
            [$"qaly://organization/member-capacity/{item.UserId}", $"qaly://organization/member-skill-aggregate/{item.UserId}", $"qaly://organization/member-professional-profile/{item.UserId}"],
            item.WeeklyCapacity.Select(ToWeeklyCapacityDto).ToArray(),
            item.ProfessionalProfiles.OrderBy(profile => profile, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static ProjectWeeklyCapacityDto[] BuildProportionalWeeklyAllocation(
        IReadOnlyList<WeeklyCapacityFacts> weeklyCapacity,
        decimal proposedDeliveryHours,
        decimal reviewerCoordinationHours)
    {
        var totalEffective = weeklyCapacity.Sum(item => item.EffectiveAvailableHours);
        return weeklyCapacity.Select(item =>
        {
            var delivery = totalEffective <= 0m
                ? 0m
                : Math.Round(proposedDeliveryHours * item.EffectiveAvailableHours / totalEffective, 2);
            var overhead = totalEffective <= 0m
                ? 0m
                : Math.Round(reviewerCoordinationHours * item.EffectiveAvailableHours / totalEffective, 2);
            var denominator = Math.Max(0m, item.DeclaredCapacityHours - item.AvailabilityReductionHours);
            var load = denominator <= 0m
                ? 100m
                : Math.Round((item.ExistingCommittedHours + delivery + overhead) * 100m / denominator, 2);
            return ToWeeklyCapacityDto(item, delivery, overhead, load);
        }).ToArray();
    }

    private static List<WeeklyCapacityFacts> BuildWeeklyCapacityFacts(
        OrganizationMemberCapacityProfile? profile,
        IReadOnlyList<CommitmentFacts> commitments,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        decimal focusReservePercent)
    {
        var result = new List<WeeklyCapacityFacts>();
        var cursor = windowStart;
        var weeklyCapacity = profile?.WeeklyCapacityHours ?? 0m;
        var weekNumber = 1;
        while (cursor < windowEnd)
        {
            var end = cursor.AddDays(7) < windowEnd ? cursor.AddDays(7) : windowEnd;
            var days = Math.Max(0m, (decimal)(end - cursor).TotalDays);
            var declared = Math.Round(weeklyCapacity * days / 7m, 2);
            var unavailable = profile == null
                ? 0m
                : CalculateAvailabilityReduction(profile, cursor, end, weeklyCapacity);
            var committed = Math.Round(commitments.Sum(item => AllocateCommitmentHours(item, cursor, end, windowStart, windowEnd)), 2);
            var reserve = Math.Round(declared * focusReservePercent / 100m, 2);
            var effective = Math.Max(0m, declared - unavailable - committed - reserve);
            result.Add(new WeeklyCapacityFacts(
                $"W{weekNumber:00}", cursor, end, declared, unavailable, committed, reserve, effective));
            cursor = end;
            weekNumber++;
        }
        return result;
    }

    private static decimal AllocateCommitmentHours(
        CommitmentFacts commitment,
        DateTimeOffset bucketStart,
        DateTimeOffset bucketEnd,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var commitmentStart = commitment.StartDate ?? windowStart;
        var commitmentEnd = commitment.DueDate?.AddDays(1) ?? windowEnd;
        if (commitmentEnd <= commitmentStart) commitmentEnd = commitmentStart.AddDays(1);
        var boundedStart = commitmentStart > windowStart ? commitmentStart : windowStart;
        var boundedEnd = commitmentEnd < windowEnd ? commitmentEnd : windowEnd;
        if (boundedEnd <= boundedStart) return 0m;
        var overlapStart = boundedStart > bucketStart ? boundedStart : bucketStart;
        var overlapEnd = boundedEnd < bucketEnd ? boundedEnd : bucketEnd;
        if (overlapEnd <= overlapStart) return 0m;
        var totalDays = Math.Max(1m / 24m, (decimal)(boundedEnd - boundedStart).TotalDays);
        var overlapDays = Math.Max(0m, (decimal)(overlapEnd - overlapStart).TotalDays);
        return Math.Round(commitment.EstimatedHours * overlapDays / totalDays, 2);
    }

    private static ProjectWeeklyCapacityDto ToWeeklyCapacityDto(WeeklyCapacityFacts item)
    {
        var denominator = Math.Max(0m, item.DeclaredCapacityHours - item.AvailabilityReductionHours);
        var load = denominator <= 0m ? 100m : Math.Round(item.ExistingCommittedHours * 100m / denominator, 2);
        return ToWeeklyCapacityDto(item, 0m, 0m, load);
    }

    private static ProjectWeeklyCapacityDto ToWeeklyCapacityDto(
        WeeklyCapacityFacts item,
        decimal delivery,
        decimal overhead,
        decimal load)
        => new(
            item.WeekKey,
            item.StartsAt,
            item.EndsAt,
            item.DeclaredCapacityHours,
            item.AvailabilityReductionHours,
            item.ExistingCommittedHours,
            item.FocusReserveHours,
            item.EffectiveAvailableHours,
            delivery,
            overhead,
            load);

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
        IReadOnlySet<string> ProfessionalProfiles,
        bool ManagerProfileEligible,
        decimal EvidenceConfidence,
        decimal WeeklyCapacityHours,
        decimal WindowCapacityHours,
        decimal ExistingCommittedHours,
        decimal FocusReserveHours,
        decimal AvailableHours,
        int ActiveProjectCount,
        string TimeZoneId,
        string CapacityState,
        IReadOnlyList<WeeklyCapacityFacts> WeeklyCapacity);

    private sealed record WeeklyCapacityFacts(
        string WeekKey,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        decimal DeclaredCapacityHours,
        decimal AvailabilityReductionHours,
        decimal ExistingCommittedHours,
        decimal FocusReserveHours,
        decimal EffectiveAvailableHours);

    private sealed record CommitmentFacts(
        Guid UserId,
        DateTimeOffset? StartDate,
        DateTimeOffset? DueDate,
        int EstimatedHours);
}
