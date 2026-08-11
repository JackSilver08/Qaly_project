using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public static class AiAssistantManualGuidanceRegistry
{
    private static readonly IReadOnlyDictionary<string, (string Label, string Route, string Permission)> Routes =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["projects"] = ("Mở danh sách dự án và chọn Tạo dự án", "/projects", "project.create"),
            ["tasks"] = ("Mở Tasks để tạo hoặc phân rã công việc", "/tasks", "task.create"),
            ["teams"] = ("Kiểm tra thành viên, vai trò và năng lực", "/teams", "organization.read"),
            ["meetings"] = ("Mở Groups rồi chọn cuộc họp cần xử lý", "/groups", "meeting.read"),
            ["groups"] = ("Thiết lập không gian cộng tác", "/groups", "group.read"),
            ["calendar"] = ("Mở Project để đối chiếu lộ trình, Sprint và mốc Task", "/projects", "calendar.read"),
            ["wiki"] = ("Mở Project rồi chọn Wiki", "/projects", "wiki.read"),
            ["analytics"] = ("Mở Analytics và báo cáo", "/analytics", "analytics.read"),
            ["organizations"] = ("Mở quản lý tổ chức và Rulebook", "/organizations", "organization.read"),
            ["settings"] = ("Kiểm tra cấu hình tổ chức và AI", "/settings", "settings.read")
        };

    public static AiAssistantManualGuidanceDto ForCapability(string? capabilityId)
    {
        var routeKeys = string.Equals(capabilityId, AiProjectLaunchContract.CapabilityId, StringComparison.Ordinal) ||
                        string.Equals(capabilityId, AiProjectOrchestrationContract.StaffingCapabilityId, StringComparison.Ordinal) ||
                        string.Equals(capabilityId, AiProjectOrchestrationContract.ExecuteCapabilityId, StringComparison.Ordinal) ||
                        string.Equals(capabilityId, AiProjectOrchestrationContract.MonitorCapabilityId, StringComparison.Ordinal) ||
                        string.Equals(capabilityId, "project.create.v1", StringComparison.Ordinal)
            ? new[] { "projects", "teams", "tasks" }
            : new[] { "projects", "tasks", "settings" };
        var steps = routeKeys.Select((key, index) =>
        {
            var route = Routes[key];
            return new AiAssistantManualGuidanceStepDto(index + 1, route.Label, route.Route, route.Permission);
        }).ToArray();
        return new AiAssistantManualGuidanceDto(
            AiAssistantConversationContract.GuidanceSchemaId,
            true,
            "Bạn vẫn có thể tiếp tục bằng các màn hình Qaly hiện có; các liên kết dưới đây do máy chủ xác nhận.",
            steps);
    }

    public static bool IsRegisteredRoute(string route)
        => Routes.Values.Any(item => string.Equals(item.Route, route, StringComparison.Ordinal));
}
