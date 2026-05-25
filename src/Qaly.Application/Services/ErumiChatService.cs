using System.Diagnostics;
using System.Globalization;
using System.Text;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Analytics;

namespace Qaly.Application.Services;

public sealed class ErumiChatService : IErumiChatService
{
    private static readonly string[] WorkspaceSources = { "AnalyticsService", "Tasks", "TimeEntries", "ProjectMembers" };
    private static readonly string[] ProjectSources = { "AnalyticsService", "Projects", "Tasks", "TimeEntries", "ProjectMembers" };
    private static readonly string[] IntentRouterSources = { "Erumi intent router" };
    private static readonly string[] WorkspaceChartLabels = { "Task hoàn thành", "Giờ đã log" };
    private static readonly string[] StatusChartLabels = { "Hoàn thành", "Đang làm", "Khác/chưa bắt đầu" };

    private readonly IAnalyticsService _analyticsService;
    private readonly IProjectService _projectService;

    public ErumiChatService(
        IAnalyticsService analyticsService,
        IProjectService projectService)
    {
        _analyticsService = analyticsService;
        _projectService = projectService;
    }

    public async Task<Result<ErumiChatResponseDto>> ChatFastAsync(ErumiChatRequestDto request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return Result.Failure<ErumiChatResponseDto>("message is required.", 400);
        }

        var normalized = Normalize(message);
        if (IsGreeting(normalized))
        {
            return Result.Success(CreateResponse(
                "Chào bạn, mình là Erumi. Mình có thể trả lời nhanh các câu hỏi về tiến độ, task quá hạn, workload, năng suất và tạo biểu đồ từ dữ liệu Qaly.",
                "greeting",
                sw));
        }

        if (IsWriteIntent(normalized))
        {
            return Result.Success(BuildWriteConfirmationResponse(message, request.ProjectId, sw));
        }

        if (!request.ProjectId.HasValue)
        {
            return await BuildWorkspaceResponseAsync(normalized, sw, ct);
        }

        return await BuildProjectResponseAsync(request.ProjectId.Value, normalized, sw, ct);
    }

    private async Task<Result<ErumiChatResponseDto>> BuildWorkspaceResponseAsync(
        string normalized,
        Stopwatch sw,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetWorkspaceAnalyticsAsync(ct);
        if (!result.IsSuccess || result.Data == null)
        {
            return Result.Failure<ErumiChatResponseDto>(result.Error ?? "Không thể lấy dữ liệu workspace.", result.StatusCode);
        }

        var data = result.Data;
        var metrics = new List<ErumiMetricDto>
        {
            new("Tổng dự án", data.TotalProjects.ToString(CultureInfo.InvariantCulture), "neutral"),
            new("Đang hoạt động", data.ActiveProjects.ToString(CultureInfo.InvariantCulture), "good"),
            new("Tổng task", data.TotalTasks.ToString(CultureInfo.InvariantCulture), "neutral"),
            new("Hoàn thành tuần này", data.DoneTasksThisWeek.ToString(CultureInfo.InvariantCulture), "good"),
            new("Giờ log tuần này", $"{data.TotalHoursLoggedThisWeek:0.##}h", "neutral")
        };

        var charts = new List<ErumiChartDto>
        {
            new(
                "bar",
                "Nhịp làm việc tuần này",
                WorkspaceChartLabels,
                new[] { (double)data.DoneTasksThisWeek, data.TotalHoursLoggedThisWeek },
                null)
        };

        var reply = ContainsAny(normalized, "bieu do", "chart", "thong ke")
            ? "Mình đã lấy nhanh dữ liệu workspace và dựng biểu đồ tuần này từ số liệu hệ thống."
            : $"Workspace hiện có **{data.TotalProjects} dự án**, **{data.TotalTasks} task** và **{data.DoneTasksThisWeek} task hoàn thành trong 7 ngày gần nhất**. Tổng thời gian đã log tuần này là **{data.TotalHoursLoggedThisWeek:0.##}h**.";

        if (ContainsAny(normalized, "du an nao", "project nao", "chi tiet du an", "qua han", "rui ro"))
        {
            reply += "\n\nĐể phân tích rủi ro chi tiết hoặc biểu đồ theo trạng thái, bạn hãy chọn một dự án cụ thể ở dropdown phía trên.";
        }

        return Result.Success(CreateResponse(
            reply,
            "workspace_analytics",
            sw,
            metrics,
            charts,
            sources: WorkspaceSources));
    }

    private async Task<Result<ErumiChatResponseDto>> BuildProjectResponseAsync(
        Guid projectId,
        string normalized,
        Stopwatch sw,
        CancellationToken ct)
    {
        var projectResult = await _projectService.GetByIdAsync(projectId, ct);
        if (!projectResult.IsSuccess || projectResult.Data == null)
        {
            return Result.Failure<ErumiChatResponseDto>(projectResult.Error ?? "Không thể lấy dự án.", projectResult.StatusCode);
        }

        var analyticsResult = await _analyticsService.GetProjectAnalyticsAsync(projectId, ct);
        if (!analyticsResult.IsSuccess || analyticsResult.Data == null)
        {
            return Result.Failure<ErumiChatResponseDto>(analyticsResult.Error ?? "Không thể lấy dữ liệu phân tích.", analyticsResult.StatusCode);
        }

        var project = projectResult.Data;
        var data = analyticsResult.Data;
        var metrics = BuildProjectMetrics(data);
        var charts = BuildProjectCharts(data);
        var intent = ClassifyProjectIntent(normalized);
        var reply = intent switch
        {
            "risk" => BuildRiskReply(project.Name, data),
            "productivity" => BuildProductivityReply(project.Name, data),
            "chart" => $"Mình đã lấy dữ liệu thật của dự án **{project.Name}** và chuẩn bị các biểu đồ trạng thái, workload thành viên và xu hướng hoàn thành theo ngày.",
            _ => BuildSummaryReply(project.Name, data)
        };

        return Result.Success(CreateResponse(
            reply,
            intent,
            sw,
            metrics,
            charts,
            sources: ProjectSources));
    }

    private static List<ErumiMetricDto> BuildProjectMetrics(ProjectAnalyticsDto data)
    {
        var progress = Percent(data.DoneTasks, data.TotalTasks);
        var overdueTone = data.OverdueTasks > 0 ? "danger" : "good";
        var hourRatio = data.TotalEstimatedHours <= 0
            ? 0
            : Math.Round(data.TotalActualHours * 100 / data.TotalEstimatedHours, 1);

        return new List<ErumiMetricDto>
        {
            new("Tổng task", data.TotalTasks.ToString(CultureInfo.InvariantCulture), "neutral"),
            new("Hoàn thành", $"{data.DoneTasks} ({progress:0.#}%)", "good"),
            new("Đang làm", data.InProgressTasks.ToString(CultureInfo.InvariantCulture), "neutral"),
            new("Quá hạn", data.OverdueTasks.ToString(CultureInfo.InvariantCulture), overdueTone),
            new("Giờ thực tế", $"{data.TotalActualHours:0.##}h", "neutral", data.TotalEstimatedHours > 0 ? $"{hourRatio:0.#}% so với ước tính" : null)
        };
    }

    private static List<ErumiChartDto> BuildProjectCharts(ProjectAnalyticsDto data)
    {
        var otherOpenTasks = Math.Max(0, data.TotalTasks - data.DoneTasks - data.InProgressTasks);
        var memberRows = data.MemberProductivity
            .OrderByDescending(item => item.AssignedTasks)
            .Take(8)
            .ToList();

        return new List<ErumiChartDto>
        {
            new(
                "pie",
                "Phân bố trạng thái task",
                StatusChartLabels,
                new[] { (double)data.DoneTasks, data.InProgressTasks, otherOpenTasks },
                "task"),
            new(
                "bar",
                "Workload theo thành viên",
                memberRows.Select(item => item.FullName).ToArray(),
                memberRows.Select(item => (double)item.AssignedTasks).ToArray(),
                "task"),
            new(
                "line",
                "Task hoàn thành 14 ngày gần nhất",
                data.DailyProductivity.Select(item => item.Date.ToString("dd/MM", CultureInfo.InvariantCulture)).ToArray(),
                data.DailyProductivity.Select(item => (double)item.CompletedTasks).ToArray(),
                "task")
        };
    }

    private static string BuildSummaryReply(string projectName, ProjectAnalyticsDto data)
    {
        var progress = Percent(data.DoneTasks, data.TotalTasks);
        var overdueText = data.OverdueTasks == 0
            ? "không có task quá hạn"
            : $"có **{data.OverdueTasks} task quá hạn** cần xử lý";

        return $"Dự án **{projectName}** hiện có **{data.TotalTasks} task**, đã hoàn thành **{data.DoneTasks} task** (**{progress:0.#}%**), đang làm **{data.InProgressTasks} task** và {overdueText}. Tổng thời gian đã log là **{data.TotalActualHours:0.##}h** trên kế hoạch **{data.TotalEstimatedHours:0.##}h**.";
    }

    private static string BuildRiskReply(string projectName, ProjectAnalyticsDto data)
    {
        if (data.TotalTasks == 0)
        {
            return $"Dự án **{projectName}** chưa có task để phân tích rủi ro.";
        }

        var overdueRate = Percent(data.OverdueTasks, data.TotalTasks);
        if (data.OverdueTasks == 0)
        {
            return $"Rủi ro hiện tại của **{projectName}** đang thấp: chưa ghi nhận task quá hạn. Mình vẫn khuyến nghị theo dõi đều workload thành viên và xu hướng hoàn thành mỗi ngày.";
        }

        var level = overdueRate >= 25 ? "cao" : overdueRate >= 10 ? "trung bình" : "thấp";
        return $"Dự án **{projectName}** có rủi ro **{level}** vì đang có **{data.OverdueTasks}/{data.TotalTasks} task quá hạn** (**{overdueRate:0.#}%**). Nên ưu tiên rà soát các task trễ hạn, cân lại workload và chốt lại deadline gần nhất.";
    }

    private static string BuildProductivityReply(string projectName, ProjectAnalyticsDto data)
    {
        if (data.MemberProductivity.Count == 0)
        {
            return $"Dự án **{projectName}** chưa có dữ liệu thành viên để phân tích năng suất.";
        }

        var busiest = data.MemberProductivity.OrderByDescending(item => item.AssignedTasks).First();
        var mostDone = data.MemberProductivity.OrderByDescending(item => item.DoneTasks).First();
        var mostLogged = data.MemberProductivity.OrderByDescending(item => item.LoggedHours).First();

        return $"Năng suất của **{projectName}**: **{mostDone.FullName}** đang hoàn thành nhiều task nhất ({mostDone.DoneTasks}), **{busiest.FullName}** có workload cao nhất ({busiest.AssignedTasks} task), và **{mostLogged.FullName}** log nhiều thời gian nhất ({mostLogged.LoggedHours:0.##}h). Biểu đồ bên dưới lấy trực tiếp từ dữ liệu task và time log.";
    }

    private static ErumiChatResponseDto BuildWriteConfirmationResponse(string message, Guid? projectId, Stopwatch sw)
    {
        var action = new ErumiActionDto(
            "draft_change",
            "Chuẩn bị thay đổi để bạn xác nhận",
            new { message, projectId },
            RequiresConfirmation: true);

        return CreateResponse(
            "Mình đã nhận ra đây là yêu cầu thay đổi dữ liệu. Để tránh cập nhật nhầm, Erumi sẽ chỉ tạo bản nháp hành động và cần bạn xác nhận trên UI trước khi ghi vào hệ thống.",
            "write_confirmation",
            sw,
            actions: new[] { action },
            sources: IntentRouterSources);
    }

    private static ErumiChatResponseDto CreateResponse(
        string reply,
        string intent,
        Stopwatch sw,
        IReadOnlyList<ErumiMetricDto>? metrics = null,
        IReadOnlyList<ErumiChartDto>? charts = null,
        IReadOnlyList<ErumiActionDto>? actions = null,
        IReadOnlyList<string>? sources = null)
    {
        sw.Stop();
        return new ErumiChatResponseDto(
            reply,
            metrics ?? Array.Empty<ErumiMetricDto>(),
            charts ?? Array.Empty<ErumiChartDto>(),
            actions ?? Array.Empty<ErumiActionDto>(),
            sources ?? Array.Empty<string>(),
            UsedAi: false,
            intent,
            LatencyMs: (int)sw.ElapsedMilliseconds);
    }

    private static string ClassifyProjectIntent(string normalized)
    {
        if (ContainsAny(normalized, "rui ro", "qua han", "tre han", "cham tien do", "deadline", "risk"))
        {
            return "risk";
        }

        if (ContainsAny(normalized, "nang suat", "hieu suat", "workload", "khoi luong", "thanh vien", "qua tai", "ai dang ranh"))
        {
            return "productivity";
        }

        if (ContainsAny(normalized, "bieu do", "chart", "cot", "tron", "duong", "thong ke", "visual"))
        {
            return "chart";
        }

        return "project_summary";
    }

    private static bool IsGreeting(string normalized)
        => normalized is "hi" or "hello" or "xin chao" or "chao" or "chao ban"
           || ContainsAny(normalized, "xin chao", "hello erumi", "chao erumi");

    private static bool IsWriteIntent(string normalized)
        => ContainsAny(
            normalized,
            "tao task",
            "them task",
            "tao cong viec",
            "them cong viec",
            "cap nhat task",
            "doi trang thai",
            "chuyen trang thai",
            "assign",
            "phan cong",
            "gan cho");

    private static bool ContainsAny(string normalized, params string[] terms)
        => terms.Any(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static double Percent(int part, int total)
        => total <= 0 ? 0 : Math.Round(part * 100.0 / total, 1);

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Replace('đ', 'd');
    }
}
