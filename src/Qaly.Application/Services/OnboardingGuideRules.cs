namespace Qaly.Application.Services;

/// <summary>One step of the onboarding guide. Kept short on purpose — cards, not a manual.</summary>
/// <param name="Title">Four or five words.</param>
/// <param name="Detail">One sentence.</param>
public sealed record OnboardingStep(string Title, string Detail);

/// <summary>
/// The guide a newcomer sees, tailored to what their role can actually do.
///
/// Deliberately terse: four cards and a five-step process. Anything longer stops being read.
/// </summary>
/// <param name="RoleLabel">Their role, in Vietnamese.</param>
/// <param name="Summary">One sentence on what this role is for.</param>
/// <param name="FirstActions">What to do first, in order.</param>
/// <param name="Workflow">The team's work process, same five steps for everyone.</param>
/// <param name="AiHint">What the assistant can do for this role.</param>
/// <param name="SampleQuestions">Prompts that will work at this role's AI tier.</param>
public sealed record OnboardingGuide(
    string RoleLabel,
    string Summary,
    IReadOnlyList<OnboardingStep> FirstActions,
    IReadOnlyList<OnboardingStep> Workflow,
    string AiHint,
    IReadOnlyList<string> SampleQuestions);

public static class OnboardingGuideRules
{
    /// <summary>The delivery process, identical for every role so the team shares one vocabulary.</summary>
    private static readonly OnboardingStep[] SharedWorkflow =
    [
        new("1. Nhận việc", "Task được giao xuất hiện ở Tổng quan và trong bảng Kanban của dự án."),
        new("2. Bắt đầu", "Kéo task sang Đang làm và bật chấm công để nhóm thấy bạn đã vào việc."),
        new("3. Cập nhật", "Bình luận khi có vướng mắc; đừng để task đứng yên quá lâu."),
        new("4. Nộp kết quả", "Đính kèm evidence rồi chuyển sang Chờ review."),
        new("5. Đóng task", "Người review duyệt evidence, task chuyển Hoàn thành và tiến độ tự cập nhật.")
    ];

    public static OnboardingGuide Build(string? projectRole, bool isOwner = false, bool isSystemAdmin = false)
    {
        var tier = AiCapabilityRules.ResolveTier(projectRole, isSystemAdmin, isOwner);
        var effectiveRole = isOwner
            ? ProjectRoleRules.Owner
            : ProjectRoleRules.NormalizeProjectRole(projectRole);

        var manages = isOwner || isSystemAdmin || ProjectRoleRules.CanManageProject(effectiveRole);
        var readOnly = !manages
            && (ProjectRoleRules.IsViewer(effectiveRole) || ProjectRoleRules.IsCustomer(effectiveRole));
        var specialist = !manages
            && (string.Equals(effectiveRole, ProjectRoleRules.Developer, StringComparison.Ordinal)
                || string.Equals(effectiveRole, ProjectRoleRules.Tester, StringComparison.Ordinal)
                || string.Equals(effectiveRole, ProjectRoleRules.Reviewer, StringComparison.Ordinal));

        return new OnboardingGuide(
            RoleLabel: ProjectPermissionRules.DescribeRoleVietnamese(effectiveRole),
            Summary: BuildSummary(manages, specialist, readOnly, effectiveRole),
            FirstActions: BuildFirstActions(manages, specialist, readOnly),
            Workflow: SharedWorkflow,
            AiHint: AiCapabilityRules.DescribeVietnamese(tier),
            SampleQuestions: BuildSampleQuestions(tier));
    }

    private static string BuildSummary(bool manages, bool specialist, bool readOnly, string role)
    {
        if (manages) return "Bạn điều phối dự án: phân công, theo dõi rủi ro và quyết định kế hoạch.";
        if (readOnly)
        {
            return ProjectRoleRules.IsCustomer(role)
                ? "Bạn theo dõi phần dự án được chia sẻ với mình."
                : "Bạn theo dõi dự án ở chế độ chỉ đọc.";
        }
        if (specialist) return "Bạn xử lý công việc chuyên môn và định hình task cho nhóm.";
        return "Bạn thực hiện các task được giao trong dự án.";
    }

    private static OnboardingStep[] BuildFirstActions(bool manages, bool specialist, bool readOnly)
    {
        if (manages)
        {
            return
            [
                new("Xem thẻ vai trò", "Tab Thành viên hiển thị đầy đủ quyền bạn đang có."),
                new("Kiểm tra rủi ro", "Tổng quan liệt kê task quá hạn và task chưa ai xem."),
                new("Phân công việc", "Hỏi trợ lý AI ai đang còn năng lực trước khi giao task."),
                new("Đặt vai trò cho nhóm", "Tạo vai trò riêng nếu nhóm bạn có chuyên môn đặc thù.")
            ];
        }

        if (readOnly)
        {
            return
            [
                new("Xem thẻ vai trò", "Tab Thành viên cho biết bạn xem được những gì."),
                new("Theo dõi tiến độ", "Mở dự án để xem % hoàn thành và các mốc chính."),
                new("Hỏi trợ lý AI", "Trợ lý tóm tắt tình hình dự án cho bạn.")
            ];
        }

        if (specialist)
        {
            return
            [
                new("Xem thẻ vai trò", "Tab Thành viên hiển thị đầy đủ quyền bạn đang có."),
                new("Nhận task đầu tiên", "Tổng quan hiển thị các task đang giao cho bạn."),
                new("Bật chấm công", "Chấm công giúp nhóm ước lượng chính xác hơn."),
                new("Dùng AI phân tích", "Bạn xem được khối lượng công việc của cả nhóm.")
            ];
        }

        return
        [
            new("Xem thẻ vai trò", "Tab Thành viên hiển thị đầy đủ quyền bạn đang có."),
            new("Nhận task đầu tiên", "Tổng quan hiển thị các task đang giao cho bạn."),
            new("Bật chấm công", "Chấm công giúp nhóm ước lượng chính xác hơn."),
            new("Hỏi trợ lý AI", "Trợ lý cho bạn xem tiến độ và tóm tắt dự án.")
        ];
    }

    private static string[] BuildSampleQuestions(AiCapabilityTier tier) => tier switch
    {
        AiCapabilityTier.Full =>
        [
            "Dự án đang chậm ở đâu?",
            "Ai còn năng lực nhận thêm việc?",
            "Tóm tắt tiến độ sprint hiện tại."
        ],
        AiCapabilityTier.Specialist =>
        [
            "Khối lượng công việc của nhóm thế nào?",
            "Task nào đang quá hạn?",
            "Tóm tắt tiến độ dự án."
        ],
        AiCapabilityTier.Contributor =>
        [
            "Tôi đang có task nào?",
            "Tóm tắt tiến độ dự án.",
            "Tuần này tôi đã chấm công bao nhiêu giờ?"
        ],
        AiCapabilityTier.ReadOnly =>
        [
            "Tóm tắt tiến độ dự án.",
            "Dự án còn bao nhiêu task chưa xong?"
        ],
        _ => []
    };
}
