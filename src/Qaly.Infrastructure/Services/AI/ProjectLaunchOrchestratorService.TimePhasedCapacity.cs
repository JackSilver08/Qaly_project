using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class ProjectLaunchOrchestratorService
{
    private static Dictionary<Guid, MutableWeeklyAllocation[]> BuildMutableWeeklyAllocations(
        IReadOnlyList<ProjectStaffingMemberDto> members)
        => members.ToDictionary(
            member => member.UserId,
            member => (member.WeeklyAllocation ?? [])
                .Select(week => new MutableWeeklyAllocation(week))
                .ToArray());

    private static AssignmentSelection? SelectTimePhasedAssignment(
        ProjectLaunchSprintPlanDto sprint,
        ProjectLaunchTaskPlanDto task,
        IReadOnlyList<ProjectStaffingMemberDto> members,
        IReadOnlyDictionary<Guid, MutableWeeklyAllocation[]> allocations,
        decimal reviewerCoordinationOverheadPercent,
        decimal maxUtilizationPercent,
        Guid? managerUserId,
        Guid? requiredAssigneeId = null,
        Guid? preferredReviewerId = null)
    {
        var assignees = requiredAssigneeId.HasValue
            ? members.Where(item => item.UserId == requiredAssigneeId.Value).ToArray()
            : members.ToArray();
        var choices = new List<TimePhasedAssignmentChoice>();
        foreach (var assignee in assignees)
        {
            var reviewerCandidates = members
                .Where(item => item.UserId != assignee.UserId)
                .OrderByDescending(item => item.UserId == preferredReviewerId)
                .ThenByDescending(item => item.UserId == managerUserId)
                .ThenByDescending(item => string.Equals(item.ProposedRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal))
                .ThenBy(item => item.LoadAfterPercent)
                .Cast<ProjectStaffingMemberDto?>()
                .ToList();
            if (reviewerCandidates.Count == 0)
                reviewerCandidates.Add(null);

            foreach (var reviewer in reviewerCandidates)
            {
                if (!CombinedSkillsCover(task.RequiredSkillNames, assignee, reviewer))
                    continue;
                var reviewerUserId = reviewer?.UserId ?? managerUserId ?? assignee.UserId;
                if (!CanReserveTimePhasedAssignment(
                        sprint,
                        task,
                        assignee.UserId,
                        reviewerUserId,
                        allocations,
                        reviewerCoordinationOverheadPercent,
                        maxUtilizationPercent,
                        out var peakLoad))
                    continue;

                var directMatches = task.RequiredSkillNames.Count(required =>
                    assignee.CoveredSkills.Any(skill => Normalize(skill) == Normalize(required)));
                var currentDelivery = allocations.GetValueOrDefault(assignee.UserId)?
                    .Sum(item => item.ProposedDeliveryHours) ?? decimal.MaxValue;
                choices.Add(new TimePhasedAssignmentChoice(
                    new AssignmentSelection(assignee, reviewer),
                    directMatches,
                    peakLoad,
                    currentDelivery,
                    reviewer?.UserId == preferredReviewerId));
            }
        }

        return choices
            .OrderByDescending(item => item.DirectSkillMatches)
            .ThenBy(item => item.PeakLoadPercent)
            .ThenBy(item => item.CurrentDeliveryHours)
            .ThenByDescending(item => item.PreferredReviewer)
            .ThenBy(item => item.Assignment.Assignee.UserId)
            .Select(item => item.Assignment)
            .FirstOrDefault();
    }

    private static bool CanReserveTimePhasedAssignment(
        ProjectLaunchSprintPlanDto sprint,
        ProjectLaunchTaskPlanDto task,
        Guid assigneeUserId,
        Guid reviewerUserId,
        IReadOnlyDictionary<Guid, MutableWeeklyAllocation[]> allocations,
        decimal reviewerCoordinationOverheadPercent,
        decimal maxUtilizationPercent,
        out decimal peakLoadPercent)
    {
        peakLoadPercent = decimal.MaxValue;
        if (!allocations.TryGetValue(assigneeUserId, out var assigneeBuckets) || assigneeBuckets.Length == 0)
            return false;
        if (!allocations.TryGetValue(reviewerUserId, out var reviewerBuckets) || reviewerBuckets.Length == 0)
            return false;

        var deliveryShares = CalculateWeeklyShares(assigneeBuckets, sprint.StartDate, sprint.EndDate, task.EstimatedHours);
        var reviewHours = Math.Round(task.EstimatedHours * reviewerCoordinationOverheadPercent / 100m, 2);
        var reviewShares = CalculateWeeklyShares(reviewerBuckets, sprint.StartDate, sprint.EndDate, reviewHours);
        if (deliveryShares.Count == 0 || (reviewHours > 0m && reviewShares.Count == 0))
            return false;

        var additions = new Dictionary<MutableWeeklyAllocation, (decimal Delivery, decimal Review)>();
        foreach (var (bucket, hours) in deliveryShares)
            additions[bucket] = (additions.GetValueOrDefault(bucket).Delivery + hours, additions.GetValueOrDefault(bucket).Review);
        foreach (var (bucket, hours) in reviewShares)
            additions[bucket] = (additions.GetValueOrDefault(bucket).Delivery, additions.GetValueOrDefault(bucket).Review + hours);

        foreach (var (bucket, addition) in additions)
        {
            if (!bucket.CanAdd(addition.Delivery, addition.Review, maxUtilizationPercent, out var projectedLoad))
                return false;
            peakLoadPercent = Math.Min(peakLoadPercent, projectedLoad);
        }
        peakLoadPercent = additions.Max(item =>
            item.Key.ProjectedLoadPercent(item.Value.Delivery, item.Value.Review));
        return true;
    }

    private static void ReserveTimePhasedAssignment(
        ProjectLaunchSprintPlanDto sprint,
        ProjectLaunchTaskPlanDto task,
        Guid assigneeUserId,
        Guid reviewerUserId,
        IReadOnlyDictionary<Guid, MutableWeeklyAllocation[]> allocations,
        decimal reviewerCoordinationOverheadPercent)
    {
        if (allocations.TryGetValue(assigneeUserId, out var assigneeBuckets))
            AllocateAcrossWeeks(assigneeBuckets, sprint.StartDate, sprint.EndDate, task.EstimatedHours, reviewer: false);
        if (allocations.TryGetValue(reviewerUserId, out var reviewerBuckets))
        {
            var reviewHours = Math.Round(task.EstimatedHours * reviewerCoordinationOverheadPercent / 100m, 2);
            AllocateAcrossWeeks(reviewerBuckets, sprint.StartDate, sprint.EndDate, reviewHours, reviewer: true);
        }
    }

    private static List<(MutableWeeklyAllocation Bucket, decimal Hours)> CalculateWeeklyShares(
        IReadOnlyList<MutableWeeklyAllocation> buckets,
        DateTimeOffset start,
        DateTimeOffset end,
        decimal hours)
    {
        var overlapping = buckets
            .Select(bucket => new
            {
                Bucket = bucket,
                Days = Math.Max(0m, (decimal)(Min(end, bucket.EndsAt) - Max(start, bucket.StartsAt)).TotalDays)
            })
            .Where(item => item.Days > 0m)
            .ToArray();
        if (overlapping.Length == 0 || hours <= 0m) return [];
        var totalDays = overlapping.Sum(item => item.Days);
        var result = new List<(MutableWeeklyAllocation Bucket, decimal Hours)>();
        decimal allocated = 0m;
        for (var index = 0; index < overlapping.Length; index++)
        {
            var share = index == overlapping.Length - 1
                ? hours - allocated
                : Math.Round(hours * overlapping[index].Days / totalDays, 2);
            result.Add((overlapping[index].Bucket, share));
            allocated += share;
        }
        return result;
    }

    private static ProjectStaffingScenarioDto EvaluateTimePhasedCapacity(
        ProjectStaffingScenarioDto scenario,
        IReadOnlyList<ProjectLaunchSprintPlanDto> sprints,
        decimal reviewerCoordinationOverheadPercent,
        decimal maxUtilizationPercent,
        string sourceVersionHash,
        string assignmentMode)
    {
        var candidateByUser = scenario.ManagerCandidates.ToDictionary(item => item.UserId);
        var allocations = scenario.Members.ToDictionary(
            member => member.UserId,
            member => (candidateByUser.GetValueOrDefault(member.UserId)?.WeeklyCapacity ?? member.WeeklyAllocation ?? [])
                .Select(week => new MutableWeeklyAllocation(week))
                .ToArray());
        var blocking = scenario.BlockingReasons
            .Where(item => !item.StartsWith("Capacity tuần ", StringComparison.Ordinal) &&
                           !item.StartsWith("Ngưỡng sử dụng tuần ", StringComparison.Ordinal) &&
                           !item.StartsWith("Task chưa được giao ", StringComparison.Ordinal))
            .ToList();

        foreach (var sprint in sprints.Where(item => item.Selected))
        {
            foreach (var task in sprint.Tasks.Where(item => item.Selected))
            {
                if (!task.ProposedAssigneeId.HasValue || !allocations.TryGetValue(task.ProposedAssigneeId.Value, out var deliveryBuckets))
                {
                    if (!string.Equals(assignmentMode, ProjectLaunchAssignmentModes.PreserveAssignments, StringComparison.Ordinal))
                        blocking.Add($"Task chưa được giao '{task.Title}' nên chưa thể chứng minh capacity theo tuần.");
                    continue;
                }

                AllocateAcrossWeeks(deliveryBuckets, sprint.StartDate, sprint.EndDate, task.EstimatedHours, reviewer: false);
                var reviewerId = task.ProposedReviewerId ?? scenario.ManagerUserId;
                if (reviewerId.HasValue && allocations.TryGetValue(reviewerId.Value, out var reviewerBuckets))
                {
                    var overhead = Math.Round(task.EstimatedHours * reviewerCoordinationOverheadPercent / 100m, 2);
                    AllocateAcrossWeeks(reviewerBuckets, sprint.StartDate, sprint.EndDate, overhead, reviewer: true);
                }
            }
        }

        var members = scenario.Members.Select(member =>
        {
            var weeks = allocations.GetValueOrDefault(member.UserId) ?? [];
            var weeklyDtos = weeks.Select(item => item.ToDto()).ToArray();
            foreach (var week in weeklyDtos)
            {
                var newHours = week.ProposedDeliveryHours + week.ReviewerCoordinationHours;
                if (newHours > week.EffectiveAvailableHours + 0.01m)
                    blocking.Add($"Capacity tuần {week.WeekKey} của {member.DisplayName} thiếu {newHours - week.EffectiveAvailableHours:0.##} giờ sau availability, tải Project khác và focus reserve.");
                if (week.LoadAfterPercent > maxUtilizationPercent + 0.01m)
                    blocking.Add($"Ngưỡng sử dụng tuần {week.WeekKey} của {member.DisplayName} là {week.LoadAfterPercent:0.##}%, vượt Rulebook {maxUtilizationPercent:0.##}%.");
            }
            return member with
            {
                ProposedHours = weeklyDtos.Sum(item => item.ProposedDeliveryHours),
                ReviewerCoordinationHours = weeklyDtos.Sum(item => item.ReviewerCoordinationHours),
                LoadAfterPercent = weeklyDtos.Select(item => item.LoadAfterPercent).DefaultIfEmpty(100m).Max(),
                WeeklyAllocation = weeklyDtos
            };
        }).ToArray();

        var timePhasedBlocking = blocking.Any(item =>
            item.StartsWith("Capacity tuần ", StringComparison.Ordinal) ||
            item.StartsWith("Ngưỡng sử dụng tuần ", StringComparison.Ordinal) ||
            item.StartsWith("Task chưa được giao ", StringComparison.Ordinal));
        var decisions = scenario.RuleDecisions
            .Where(item => item.RuleKey is not "reviewer_coordination_overhead_percent" and not "time_phased_weekly_capacity" and not "max_utilization_percent")
            .Concat([
                Decision("max_utilization_percent", timePhasedBlocking ? "block" : "pass", $"Đã kiểm tra tải từng tuần với ngưỡng {maxUtilizationPercent:0.##}%.", sourceVersionHash),
                Decision("reviewer_coordination_overhead_percent", timePhasedBlocking ? "block" : "pass", $"Đã gán và kiểm tra {reviewerCoordinationOverheadPercent:0.##}% review/coordination overhead theo reviewer của từng Task.", sourceVersionHash),
                Decision("time_phased_weekly_capacity", timePhasedBlocking ? "block" : "pass", "Capacity được kiểm theo từng tuần từ declared availability, Qaly commitments và Sprint/Task thật; external calendar không được giả lập.", sourceVersionHash)
            ])
            .ToArray();
        var feasible = scenario.Feasible && blocking.Count == 0;
        var score = feasible && members.Length > 0
            ? Math.Max(0m, 100m - members.Average(item => item.LoadAfterPercent) * 0.35m)
            : 0m;
        return scenario with
        {
            Feasible = feasible,
            Score = Math.Round(score, 2),
            Members = members,
            BlockingReasons = blocking.Distinct(StringComparer.Ordinal).ToArray(),
            Risks = feasible ? scenario.Risks : scenario.Risks.Concat(["Cần điều chỉnh lịch Sprint, estimate, staffing hoặc allocation trước khi xác nhận."]).Distinct().ToArray(),
            RuleDecisions = decisions
        };
    }

    private static void AllocateAcrossWeeks(
        IReadOnlyList<MutableWeeklyAllocation> buckets,
        DateTimeOffset start,
        DateTimeOffset end,
        decimal hours,
        bool reviewer)
    {
        var overlapping = buckets
            .Select(bucket => new
            {
                Bucket = bucket,
                Days = Math.Max(0m, (decimal)(Min(end, bucket.EndsAt) - Max(start, bucket.StartsAt)).TotalDays)
            })
            .Where(item => item.Days > 0m)
            .ToArray();
        if (overlapping.Length == 0 || hours <= 0m) return;
        var totalDays = overlapping.Sum(item => item.Days);
        decimal allocated = 0m;
        for (var index = 0; index < overlapping.Length; index++)
        {
            var share = index == overlapping.Length - 1
                ? hours - allocated
                : Math.Round(hours * overlapping[index].Days / totalDays, 2);
            if (reviewer) overlapping[index].Bucket.ReviewerCoordinationHours += share;
            else overlapping[index].Bucket.ProposedDeliveryHours += share;
            allocated += share;
        }
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;

    private sealed class MutableWeeklyAllocation
    {
        public MutableWeeklyAllocation(ProjectWeeklyCapacityDto source)
        {
            Source = source;
        }

        private ProjectWeeklyCapacityDto Source { get; }
        public DateTimeOffset StartsAt => Source.StartsAt;
        public DateTimeOffset EndsAt => Source.EndsAt;
        public decimal ProposedDeliveryHours { get; set; }
        public decimal ReviewerCoordinationHours { get; set; }

        public bool CanAdd(
            decimal deliveryHours,
            decimal reviewHours,
            decimal maxUtilizationPercent,
            out decimal projectedLoadPercent)
        {
            var newHours = ProposedDeliveryHours + ReviewerCoordinationHours + deliveryHours + reviewHours;
            projectedLoadPercent = ProjectedLoadPercent(deliveryHours, reviewHours);
            return newHours <= Source.EffectiveAvailableHours + 0.01m &&
                   projectedLoadPercent <= maxUtilizationPercent + 0.01m;
        }

        public decimal ProjectedLoadPercent(decimal deliveryHours, decimal reviewHours)
        {
            var denominator = Math.Max(0m, Source.DeclaredCapacityHours - Source.AvailabilityReductionHours);
            return denominator <= 0m
                ? 100m
                : Math.Round((Source.ExistingCommittedHours + ProposedDeliveryHours + ReviewerCoordinationHours + deliveryHours + reviewHours) * 100m / denominator, 2);
        }

        public ProjectWeeklyCapacityDto ToDto()
        {
            var denominator = Math.Max(0m, Source.DeclaredCapacityHours - Source.AvailabilityReductionHours);
            var load = denominator <= 0m
                ? 100m
                : Math.Round((Source.ExistingCommittedHours + ProposedDeliveryHours + ReviewerCoordinationHours) * 100m / denominator, 2);
            return Source with
            {
                ProposedDeliveryHours = ProposedDeliveryHours,
                ReviewerCoordinationHours = ReviewerCoordinationHours,
                LoadAfterPercent = load
            };
        }
    }

    private sealed record TimePhasedAssignmentChoice(
        AssignmentSelection Assignment,
        int DirectSkillMatches,
        decimal PeakLoadPercent,
        decimal CurrentDeliveryHours,
        bool PreferredReviewer);
}
