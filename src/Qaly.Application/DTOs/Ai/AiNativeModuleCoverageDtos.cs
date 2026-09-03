namespace Qaly.Application.DTOs.Ai;

public static class AiNativeModuleCoverageContract
{
    public const string SchemaId = "ai_native_module_coverage.v1";
    public static readonly IReadOnlyList<string> RequiredOperations =
        ["read", "analyze", "draft", "confirm", "readback", "navigation", "manual"];
}

public sealed record AiNativeModuleOperationDto(
    string Operation,
    string Status,
    string? CapabilityId,
    string UserMessage);

public sealed record AiNativeModuleCoverageDto(
    string ModuleId,
    string Label,
    string Route,
    string CoverageClass,
    IReadOnlyList<AiNativeModuleOperationDto> Operations);

public sealed record AiNativeCoverageMatrixDto(
    string SchemaId,
    int ModuleCount,
    int OperationCount,
    int CoveredOperationCount,
    int CoveragePercent,
    IReadOnlyList<AiNativeModuleCoverageDto> Modules);

public static class AiNativeModuleCoverageCatalog
{
    private static AiNativeModuleOperationDto Native(string operation, string capability, string message)
        => new(operation, "native", capability, message);

    private static AiNativeModuleOperationDto Surface(string operation, string message)
        => new(operation, "existing_native_surface", null, message);

    private static AiNativeModuleOperationDto Guided(string operation, string message)
        => new(operation, "guided", null, message);

    private static AiNativeModuleCoverageDto Module(
        string id,
        string label,
        string route,
        string coverageClass,
        params AiNativeModuleOperationDto[] overrides)
    {
        var byOperation = overrides.ToDictionary(item => item.Operation, StringComparer.Ordinal);
        var operations = AiNativeModuleCoverageContract.RequiredOperations.Select(operation =>
            byOperation.TryGetValue(operation, out var configured)
                ? configured
                : Guided(operation, $"Trợ lý hướng dẫn bằng luồng {label} hiện có; chưa có mutation adapter riêng cho thao tác này."))
            .ToArray();
        return new(id, label, route, coverageClass, operations);
    }

    public static IReadOnlyList<AiNativeModuleCoverageDto> All { get; } =
    [
        Module("workspace", "Workspace / Dashboard", "/dashboard", "native_read",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc facts Workspace đã kiểm quyền."),
            Native("analyze", AiAssistantResearchPlanContract.CapabilityId, "Phân tích facts và lập phương án có nguồn."),
            Surface("readback", "Lịch sử Assistant lưu response, nguồn và artifact trên máy chủ."),
            Surface("navigation", "Mở Dashboard chính.")),
        Module("organizations", "Tổ chức / Rulebook", "/organizations", "native_action",
            Native("read", AiProjectLaunchContract.CapabilityId, "Đọc tổ chức và Rulebook hiệu lực."),
            Surface("draft", "Tạo Rulebook draft qua API tổ chức; chưa có hiệu lực."),
            Surface("confirm", "Owner review và kích hoạt đúng revision."),
            Surface("readback", "Đọc lại Rulebook/version hiệu lực."),
            Surface("navigation", "Mở quản lý tổ chức.")),
        Module("projects", "Dự án", "/projects", "native_action",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc Project facts có nguồn."),
            Native("analyze", AiAssistantResearchPlanContract.CapabilityId, "Phân tích Project và phương án."),
            Native("draft", AiProjectLaunchContract.CapabilityId, "Tạo Launch Brief và delivery plan để review."),
            Native("confirm", AiProjectOrchestrationContract.ExecuteCapabilityId, "Xác nhận batch Project Launch đã review."),
            Native("readback", AiProjectOrchestrationContract.MonitorCapabilityId, "Đọc receipt và monitor baseline."),
            Surface("navigation", "Mở Project hoặc deep-link từ receipt.")),
        Module("tasks", "Nhiệm vụ", "/tasks", "native_action",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc Task facts đã kiểm quyền."),
            Native("analyze", AiAssistantResearchPlanContract.CapabilityId, "Phân tích rủi ro và dependency."),
            Native("draft", AiAssistantTurnContract.TaskCreateIntent, "Soạn Task draft có cấu trúc."),
            Surface("confirm", "Action Composer xác nhận chọn lọc và idempotent."),
            Surface("readback", "Đọc lại Task đã tạo qua canonical API."),
            Surface("navigation", "Mở Tasks hoặc Task deep-link.")),
        Module("task_assignment", "Phân công Task / Kanban", "/projects", "native_action",
            Surface("read", "Đọc Task, người phụ trách hiện tại, skill evidence, capacity, availability và tải đa dự án."),
            Native("analyze", AiAssistantContextContract.TaskAssignmentScheduleCapability, "Xếp hạng ứng viên bằng hard constraint xác định, không coi chỗ trống là capacity."),
            Native("draft", AiAssistantContextContract.TaskAssignmentScheduleCapability, "Mở phương án người/lịch chỉnh được từ Assistant hoặc Kanban."),
            Native("confirm", AiAssistantContextContract.TaskAssignmentScheduleCapability, "Một xác nhận có stale-source và idempotency ổn định."),
            Native("readback", AiAssistantContextContract.TaskAssignmentScheduleCapability, "Đọc lại đúng một assignee và lịch canonical trước receipt."),
            Surface("navigation", "Assistant mở Task cụ thể hoặc tab Capacity; Kanban dùng cùng executor.")),
        Module("task_checklist", "Acceptance Checklist", "/tasks", "native_action",
            Surface("read", "Đọc checklist canonical theo Task và quyền Project."),
            Native("analyze", AiAssistantContextContract.AcceptanceChecklistCapability, "Phân tích Task để đề xuất tiêu chí nghiệm thu."),
            Native("draft", AiAssistantContextContract.AcceptanceChecklistCapability, "Lưu checklist draft editable trên máy chủ."),
            Native("confirm", AiAssistantContextContract.AcceptanceChecklistCapability, "Một xác nhận tạo checklist canonical."),
            Native("readback", AiAssistantContextContract.AcceptanceChecklistCapability, "Đọc lại từng checklist row trước receipt."),
            Surface("navigation", "Mở Task từ receipt hoặc Task Hub.")),
        Module("task_breakdown", "Task Breakdown", "/tasks", "native_action",
            Surface("read", "Đọc task cha, subtask và dependency canonical."),
            Native("analyze", AiAssistantContextContract.TaskBreakdownCapability, "Phân tích phạm vi và số subtask được yêu cầu."),
            Native("draft", AiAssistantContextContract.TaskBreakdownCapability, "Lưu breakdown draft có thứ tự/dependency."),
            Native("confirm", AiAssistantContextContract.TaskBreakdownCapability, "Một xác nhận tạo atomic subtask graph."),
            Native("readback", AiAssistantContextContract.TaskBreakdownCapability, "Đọc lại đúng số subtask và dependency trước receipt."),
            Surface("navigation", "Mở task cha/subtask từ Task Hub.")),
        Module("sprints", "Sprints / Lộ trình", "/projects", "native_surface",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc Sprint/progress trong Project context."),
            Native("analyze", AiAssistantContextContract.RoadmapAdjustCapability, "Phân tích dependency, capacity và deadline của Sprint."),
            Native("draft", AiAssistantContextContract.RoadmapAdjustCapability, "Đề xuất before/after có thể chỉnh sửa; chưa ghi dữ liệu."),
            Native("confirm", AiAssistantContextContract.RoadmapAdjustCapability, "Chỉ áp dụng các điều chỉnh Sprint được chọn sau một xác nhận."),
            Native("readback", AiAssistantContextContract.RoadmapAdjustCapability, "Đọc lại Sprint canonical sau khi áp dụng."),
            Surface("navigation", "Mở Project và tab lộ trình.")),
        Module("teams", "Nhóm dự án / Thành viên", "/teams", "native_surface",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc membership/workload được phép."),
            Native("analyze", AiProjectOrchestrationContract.StaffingCapabilityId, "Đánh giá staffing/capacity theo Rulebook."),
            Native("draft", AiProjectOrchestrationContract.StaffingCapabilityId, "Tạo staffing scenarios."),
            Native("confirm", AiProjectOrchestrationContract.ExecuteCapabilityId, "Batch confirm membership/role đã review."),
            Surface("readback", "Receipt xác minh thành viên và vai trò."),
            Surface("navigation", "Mở Teams.")),
        Module("skills_capacity", "Kỹ năng / Capacity", "/teams", "native_surface",
            Surface("read", "Hồ sơ kỹ năng, evidence, capacity và availability dùng dữ liệu thật."),
            Native("analyze", AiProjectOrchestrationContract.StaffingCapabilityId, "Đối chiếu skill evidence và lịch tải."),
            Native("draft", AiAssistantContextContract.SkillEvidenceCapability, "Đề xuất attribution từ Task hoàn tất, assignee, required skill và acceptance đã xác nhận."),
            Native("confirm", AiAssistantContextContract.SkillEvidenceCapability, "Chỉ ghi evidence cho contributor và skill được chọn."),
            Native("readback", AiAssistantContextContract.SkillEvidenceCapability, "Đọc lại attribution/evidence canonical đã xác nhận."),
            Surface("navigation", "Mở Teams để xem/cập nhật dữ liệu nguồn.")),
        Module("groups_polls", "Groups / Polls", "/groups", "native_action",
            Surface("read", "Group, message và Poll dùng API canonical."),
            Surface("analyze", "Group summary hiện có tạo insight có nguồn."),
            Native("draft", AiAssistantContextContract.GroupPollCapability, "Lưu đúng một Poll draft editable trên máy chủ."),
            Native("confirm", AiAssistantContextContract.GroupPollCapability, "Một xác nhận tạo canonical Poll."),
            Native("readback", AiAssistantContextContract.GroupPollCapability, "Đọc lại Poll/options và trả receipt."),
            Surface("navigation", "Mở Groups.")),
        Module("meetings", "Meetings", "/groups", "native_surface",
            Surface("read", "Meeting session/import/action item dùng API canonical."),
            Native("analyze", AiAssistantContextContract.MeetingActionsCapability, "Trích quyết định, blocker và action item từ transcript đã authorize."),
            Native("draft", AiAssistantContextContract.MeetingActionsCapability, "Review từng action item; mặc định không tạo Task."),
            Native("confirm", AiAssistantContextContract.MeetingActionsCapability, "Chỉ map hoặc tạo Task cho action item được chọn."),
            Native("readback", AiAssistantContextContract.MeetingActionsCapability, "Đọc lại action-item mapping và Task canonical."),
            Surface("navigation", "Mở Group để chọn Meeting.")),
        Module("calendar", "Lịch / Mốc thời gian", "/projects", "guided",
            Surface("read", "Mốc Project/Sprint/Task và availability là nguồn lịch nội bộ."),
            Native("analyze", AiProjectOrchestrationContract.StaffingCapabilityId, "Kiểm tra xung đột lịch nội bộ/capacity."),
            Surface("navigation", "Mở Project/Lộ trình; calendar provider ngoài vẫn deferred.")),
        Module("wiki", "Wiki / Tài liệu", "/projects", "native_action",
            Surface("read", "Wiki dùng canonical Project API và visibility policy."),
            Native("analyze", AiAssistantContextContract.WikiBriefTaskCapability, "Tóm tắt có section-level source từ Wiki hiện tại."),
            Native("draft", AiAssistantContextContract.WikiBriefTaskCapability, "Lưu brief và Task tùy chọn thành draft."),
            Native("confirm", AiAssistantContextContract.WikiBriefTaskCapability, "Một xác nhận chỉ tạo Task khi người dùng chọn."),
            Native("readback", AiAssistantContextContract.WikiBriefTaskCapability, "Đọc lại Wiki/Task canonical trước receipt."),
            Surface("navigation", "Mở Project để chọn Wiki.")),
        Module("project_digest", "Project Weekly Digest", "/projects", "native_action",
            Surface("read", "Đọc preference và delivery state từ máy chủ."),
            Native("analyze", AiAssistantContextContract.ProjectDigestCapability, "Đối chiếu Project và lịch gửi hiện tại."),
            Native("draft", AiAssistantContextContract.ProjectDigestCapability, "Lưu cấu hình digest draft để review."),
            Native("confirm", AiAssistantContextContract.ProjectDigestCapability, "Một xác nhận cập nhật subscription canonical."),
            Native("readback", AiAssistantContextContract.ProjectDigestCapability, "Đọc lại lịch, trạng thái và revision."),
            Surface("navigation", "Mở Project từ receipt.")),
        Module("analytics_reports", "Phân tích / Báo cáo", "/analytics", "native_read",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc metric đã kiểm quyền."),
            Native("analyze", AiAssistantResearchPlanContract.CapabilityId, "Phân tích và đề xuất có nguồn."),
            Surface("draft", "Soạn report trong Assistant."),
            Surface("readback", "Session lưu report/nguồn; export Markdown thật."),
            Surface("navigation", "Mở Analytics.")),
        Module("notifications", "Thông báo", "/dashboard", "native_surface",
            Surface("read", "Đọc notification canonical của người dùng."),
            Surface("confirm", "Dismiss/clear dùng action UI có chủ đích."),
            Surface("readback", "Danh sách notification đọc lại từ server."),
            Surface("navigation", "Mở Dashboard notification center.")),
        Module("settings", "Thiết lập / AI / Privacy", "/settings", "native_surface",
            Surface("read", "Đọc cấu hình người dùng được phép."),
            Surface("draft", "Form model/privacy giữ thay đổi nháp trên UI."),
            Surface("confirm", "Người dùng lưu cấu hình rõ ràng."),
            Surface("readback", "Đọc lại cấu hình đã lưu."),
            Surface("navigation", "Mở Settings."))
    ];

    public static AiNativeCoverageMatrixDto Matrix()
    {
        var total = All.Count * AiNativeModuleCoverageContract.RequiredOperations.Count;
        var covered = All.Sum(module => module.Operations.Count(operation =>
            operation.Status is "native" or "existing_native_surface" or "guided"));
        return new(AiNativeModuleCoverageContract.SchemaId, All.Count, total, covered,
            total == 0 ? 0 : (int)Math.Round(covered * 100d / total), All);
    }
}
