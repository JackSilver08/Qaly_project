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
        Module("sprints", "Sprints / Lộ trình", "/projects", "native_surface",
            Native("read", AiAssistantTurnContract.GroundedReadIntent, "Đọc Sprint/progress trong Project context."),
            Native("analyze", AiAssistantResearchPlanContract.CapabilityId, "Phân tích Sprint/progress."),
            Native("draft", AiProjectOrchestrationContract.StaffingCapabilityId, "Project Launch plan tạo Sprint draft."),
            Native("confirm", AiProjectOrchestrationContract.ExecuteCapabilityId, "Batch confirm tạo canonical Sprint."),
            Surface("readback", "Project receipt đọc lại Sprint đã tạo."),
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
            Surface("readback", "Đọc lại evidence/capacity canonical."),
            Surface("navigation", "Mở Teams để xem/cập nhật dữ liệu nguồn.")),
        Module("groups_polls", "Groups / Polls", "/groups", "native_surface",
            Surface("read", "Group, message và Poll dùng API canonical."),
            Surface("analyze", "Group summary hiện có tạo insight có nguồn."),
            Surface("draft", "Poll composer hiện có tạo draft trong Group."),
            Surface("confirm", "Người dùng xác nhận trong màn hình Poll."),
            Surface("readback", "Group/Poll đọc lại từ server."),
            Surface("navigation", "Mở Groups.")),
        Module("meetings", "Meetings", "/groups", "native_surface",
            Surface("read", "Meeting session/import/action item dùng API canonical."),
            Surface("analyze", "Auto Checknote và action extraction có privacy gate."),
            Surface("draft", "Action item được review trước khi link/create Task."),
            Surface("confirm", "Người dùng xác nhận link/create Task."),
            Surface("readback", "Đọc lại action-item mapping."),
            Surface("navigation", "Mở Group để chọn Meeting.")),
        Module("calendar", "Lịch / Mốc thời gian", "/projects", "guided",
            Surface("read", "Mốc Project/Sprint/Task và availability là nguồn lịch nội bộ."),
            Native("analyze", AiProjectOrchestrationContract.StaffingCapabilityId, "Kiểm tra xung đột lịch nội bộ/capacity."),
            Surface("navigation", "Mở Project/Lộ trình; calendar provider ngoài vẫn deferred.")),
        Module("wiki", "Wiki / Tài liệu", "/projects", "native_surface",
            Surface("read", "Wiki dùng canonical Project API và visibility policy."),
            Surface("draft", "Wiki editor tạo bản nháp nội dung."),
            Surface("confirm", "Người dùng lưu nội dung trong Wiki editor."),
            Surface("readback", "Wiki page đọc lại từ server."),
            Surface("navigation", "Mở Project để chọn Wiki.")),
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
