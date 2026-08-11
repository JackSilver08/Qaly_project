namespace Qaly.Application.Services;

/// <summary>
/// How much of the AI assistant a project role may use.
///
/// Tiers are ordered: a higher tier is a strict superset of every lower one. This is the single
/// place that answers "may this role use this AI capability" — the chat tool filter and the AI job
/// endpoints both read from here so they cannot drift apart.
/// </summary>
public enum AiCapabilityTier
{
    /// <summary>Not a project member. No AI access to this project at all.</summary>
    None = 0,

    /// <summary>Read-only roles (Viewer, Customer): progress and summaries, never a write.</summary>
    ReadOnly = 1,

    /// <summary>Member: read-only plus AI actions scoped to their own work.</summary>
    Contributor = 2,

    /// <summary>Developer, Tester, Reviewer: contributor plus team-level analysis and task shaping.</summary>
    Specialist = 3,

    /// <summary>Owner, Manager, ScrumMaster, system Admin: everything, including staffing and replanning.</summary>
    Full = 4
}

public static class AiCapabilityRules
{
    /// <summary>
    /// Reading progress and summaries is available to every project member, including the
    /// read-only roles. This is the floor the customer asked for.
    /// </summary>
    public const AiCapabilityTier ProgressAndSummaryTier = AiCapabilityTier.ReadOnly;

    /// <summary>Analysis over other people's work (workload, risk, delay resolution).</summary>
    public const AiCapabilityTier TeamAnalysisTier = AiCapabilityTier.Specialist;

    /// <summary>Staffing, replanning and anything that reshapes the plan for the whole team.</summary>
    public const AiCapabilityTier PlanningTier = AiCapabilityTier.Full;

    /// <summary>
    /// Tools every tier at or above <see cref="AiCapabilityTier.ReadOnly"/> may call.
    /// </summary>
    private static readonly string[] ReadOnlyTools =
    [
        "GetProjectSummary",
        "GetOverdueTasks",
        "SearchKnowledge"
    ];

    /// <summary>
    /// Added at <see cref="AiCapabilityTier.Contributor"/>: everything here acts on the caller's
    /// own work only. Notably absent is any view of another member's workload.
    /// </summary>
    private static readonly string[] ContributorTools =
    [
        "GetMyTimeLogs",
        "StartTimeTracking",
        "StopTimeTracking",
        "AddComment"
    ];

    /// <summary>
    /// Added at <see cref="AiCapabilityTier.Specialist"/>: team-level reads and task shaping, but
    /// still not staffing decisions.
    /// </summary>
    private static readonly string[] SpecialistTools =
    [
        "GetMemberWorkload",
        "CreateTask",
        "SetTaskPriority",
        "AddDueDate"
    ];

    /// <summary>
    /// Tool only unlocked by being the assignee of a task, regardless of tier.
    /// </summary>
    public const string AssigneeOnlyTool = "UpdateTaskStatus";

    /// <summary>
    /// Resolves the tier for a project role. <paramref name="isSystemAdmin"/> and
    /// <paramref name="isProjectOwner"/> short-circuit to <see cref="AiCapabilityTier.Full"/>.
    /// </summary>
    public static AiCapabilityTier ResolveTier(
        string? projectRole,
        bool isSystemAdmin = false,
        bool isProjectOwner = false)
    {
        if (isSystemAdmin || isProjectOwner)
        {
            return AiCapabilityTier.Full;
        }

        if (string.IsNullOrWhiteSpace(projectRole))
        {
            return AiCapabilityTier.None;
        }

        var normalized = ProjectRoleRules.NormalizeProjectRole(projectRole);

        if (ProjectRoleRules.IsProjectManager(normalized))
        {
            return AiCapabilityTier.Full;
        }

        if (string.Equals(normalized, ProjectRoleRules.Developer, StringComparison.Ordinal)
            || string.Equals(normalized, ProjectRoleRules.Tester, StringComparison.Ordinal)
            || string.Equals(normalized, ProjectRoleRules.Reviewer, StringComparison.Ordinal))
        {
            return AiCapabilityTier.Specialist;
        }

        if (ProjectRoleRules.IsViewer(normalized) || ProjectRoleRules.IsCustomer(normalized))
        {
            return AiCapabilityTier.ReadOnly;
        }

        return AiCapabilityTier.Contributor;
    }

    public static bool Allows(AiCapabilityTier tier, AiCapabilityTier required)
        => tier != AiCapabilityTier.None && tier >= required;

    /// <summary>
    /// Names of the chat tools a tier may call. <see cref="AiCapabilityTier.Full"/> returns null,
    /// meaning "no filtering — every registered tool".
    /// </summary>
    public static IReadOnlySet<string>? AllowedToolNames(AiCapabilityTier tier, bool isTaskAssignee)
    {
        if (tier == AiCapabilityTier.Full)
        {
            return null;
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tier == AiCapabilityTier.None)
        {
            return allowed;
        }

        allowed.UnionWith(ReadOnlyTools);

        if (tier >= AiCapabilityTier.Contributor)
        {
            allowed.UnionWith(ContributorTools);
        }

        if (tier >= AiCapabilityTier.Specialist)
        {
            allowed.UnionWith(SpecialistTools);
        }

        // Read-only roles never write, even on a task that somehow lists them as assignee.
        if (isTaskAssignee && tier >= AiCapabilityTier.Contributor)
        {
            allowed.Add(AssigneeOnlyTool);
        }

        return allowed;
    }

    /// <summary>Short Vietnamese description of the tier, shown in the UI and the onboarding guide.</summary>
    public static string DescribeVietnamese(AiCapabilityTier tier) => tier switch
    {
        AiCapabilityTier.Full => "Toàn quyền AI: phân tích, lập kế hoạch, đề xuất phân công, tạo và sửa task.",
        AiCapabilityTier.Specialist => "AI chuyên môn: phân tích khối lượng nhóm, tạo và định hình task, hỏi đáp dự án.",
        AiCapabilityTier.Contributor => "AI cơ bản: xem tiến độ, tóm tắt, và thao tác trên công việc của chính bạn.",
        AiCapabilityTier.ReadOnly => "AI chỉ đọc: xem tiến độ và tóm tắt dự án.",
        _ => "Không có quyền dùng AI trong dự án này."
    };
}
