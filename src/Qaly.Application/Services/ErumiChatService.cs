using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Analytics;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Application.DTOs.Project;
using Microsoft.Extensions.AI;

namespace Qaly.Application.Services;

public sealed class ErumiChatService : IErumiChatService
{
    private static readonly string[] WorkspaceSources = { "AnalyticsService", "Tasks", "TimeEntries", "ProjectMembers" };
    private static readonly string[] ProjectSources = { "AnalyticsService", "Projects", "Tasks", "TimeEntries", "ProjectMembers" };
    private static readonly string[] IntentRouterSources = { "Erumi intent router" };
    private static readonly string[] UploadedFileSources = { "UploadedFile", "ImportService" };
    private static readonly string[] WorkspaceChartLabels = { "Task hoàn thành", "Giờ đã log" };
    private static readonly string[] StatusChartLabels = { "Hoàn thành", "Đang làm", "Khác/chưa bắt đầu" };

    private readonly IAnalyticsService _analyticsService;
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiGateway _aiGateway;
    private readonly AiTools? _aiTools;

    public ErumiChatService(
        IAnalyticsService analyticsService,
        IProjectService projectService,
        ITaskService taskService,
        IRepository<ProjectMember> memberRepo,
        ICurrentUserService currentUserService,
        IAiGateway aiGateway,
        AiTools? aiTools = null)
    {
        _analyticsService = analyticsService;
        _projectService = projectService;
        _taskService = taskService;
        _memberRepo = memberRepo;
        _currentUserService = currentUserService;
        _aiGateway = aiGateway;
        _aiTools = aiTools;
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
        if (request.Files is { Count: > 0 })
        {
            return Result.Success(BuildUploadedFileResponse(request.Files, sw));
        }

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
            return await BuildWorkspaceResponseAsync(request, sw, ct);
        }

        return await BuildProjectResponseAsync(request.ProjectId.Value, request, sw, ct);
    }

    private async Task<Result<ErumiChatResponseDto>> BuildWorkspaceResponseAsync(
        ErumiChatRequestDto request,
        Stopwatch sw,
        CancellationToken ct)
    {
        var normalized = Normalize(request.Message);
        var result = await _analyticsService.GetWorkspaceAnalyticsAsync(ct);
        if (!result.IsSuccess || result.Data == null)
        {
            return Result.Failure<ErumiChatResponseDto>(result.Error ?? "Không thể lấy dữ liệu workspace.", result.StatusCode);
        }

        var data = result.Data;
        if (IsExportQuestion(normalized))
        {
            return Result.Success(CreateResponse(
                "Mình có thể xuất báo cáo khi bạn chọn một dự án cụ thể. Hãy chọn dự án ở thanh nhập, rồi yêu cầu ví dụ: `Xuất báo cáo Excel cho dự án này`.",
                "export_requires_project",
                sw,
                sources: WorkspaceSources,
                confidence: 0.86));
        }

        if (IsWorkspaceProjectTableQuestion(normalized))
        {
            return await BuildWorkspaceProjectComparisonResponseAsync(normalized, data, sw, ct);
        }

        return await ExecuteWorkspaceAiChatAsync(request, data, sw, ct);
    }

    private async Task<Result<ErumiChatResponseDto>> ExecuteWorkspaceAiChatAsync(
        ErumiChatRequestDto request,
        WorkspaceAnalyticsDto data,
        Stopwatch sw,
        CancellationToken ct)
    {
        var projectsResult = await _projectService.GetAllAsync(pageSize: 100, ct: ct);
        var projects = projectsResult.Data?.Items
            .Where(p => !string.Equals(p.Status, "Archived", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? new List<ProjectDto>();

        var projectsList = new List<string>();
        foreach (var p in projects)
        {
            var analyticsResult = await _analyticsService.GetProjectAnalyticsAsync(p.Id, ct);
            if (analyticsResult.IsSuccess && analyticsResult.Data != null)
            {
                var an = analyticsResult.Data;
                projectsList.Add($"- Dự án: {p.Name} | Trạng thái: {p.Status} | Tổng task: {an.TotalTasks} | Đang làm: {an.InProgressTasks} | Hoàn thành: {an.DoneTasks} | Quá hạn: {an.OverdueTasks}");
            }
            else
            {
                projectsList.Add($"- Dự án: {p.Name} | Trạng thái: {p.Status} | Tổng task: {p.TaskCount}");
            }
        }
        var projectsContext = string.Join("\n", projectsList);

        var systemPrompt = $@"Bạn là Erumi, trợ lý phân tích AI đắc lực của hệ thống Qaly.
Bạn đang hỗ trợ người dùng quản lý toàn bộ Workspace (Tất cả dự án).

TỔNG QUAN WORKSPACE:
- Tổng dự án: {data.TotalProjects}
- Đang hoạt động: {data.ActiveProjects}
- Tổng nhiệm vụ: {data.TotalTasks}
- Hoàn thành tuần này: {data.DoneTasksThisWeek}
- Tổng thời gian đã log tuần này: {data.TotalHoursLoggedThisWeek:0.##} giờ

DANH SÁCH CÁC DỰ ÁN ĐANG HOẠT ĐỘNG:
{projectsContext}

Thời gian hiện tại: {DateTimeOffset.Now:dd/MM/yyyy HH:mm}.

YÊU CẦU ĐẦU RA (BẮT BUỘC):
Bạn phải trả về câu trả lời của mình dưới dạng một đối tượng JSON duy nhất theo cấu trúc bên dưới (không viết thêm lời thoại nào ngoài JSON, không đặt JSON trong khối code markdown). Nếu bạn muốn trả về biểu đồ, metric hoặc bảng dữ liệu động từ danh sách trên, hãy tự định nghĩa chúng trong JSON:
{{
  ""reply"": ""Câu trả lời phân tích chi tiết của bạn bằng tiếng Việt hỗ trợ Markdown. Hãy trình bày đẹp mắt, súc tích."",
  ""metrics"": [
    {{ ""label"": ""Tên chỉ số"", ""value"": ""Giá trị"", ""tone"": ""neutral|good|warning|danger"", ""hint"": ""Ghi chú nhỏ (nếu cần)"" }}
  ],
  ""tables"": [
    {{
      ""title"": ""Tiêu đề bảng"",
      ""description"": ""Mô tả bảng"",
      ""columns"": [
        {{ ""key"": ""id"", ""label"": ""Cột"", ""type"": ""text|number"", ""align"": ""left|center|right"" }}
      ],
      ""rows"": [
        {{ ""id"": ""Giá trị"" }}
      ]
    }}
  ],
  ""charts"": [
    {{
      ""type"": ""bar|pie|line"",
      ""title"": ""Tiêu đề biểu đồ"",
      ""labels"": [""Nhãn 1"", ""Nhãn 2""],
      ""values"": [10.0, 20.0],
      ""unit"": ""Đơn vị""
    }}
  ],
  ""actions"": [
    {{ ""type"": ""suggested_action"", ""label"": ""Gợi ý câu hỏi tiếp theo"" }}
  ],
  ""files"": []
}}";

        var aiRequest = new AiRequest
        {
            JobType = "workspace_analytics_chat",
            SystemPrompt = systemPrompt,
            Prompt = request.Message,
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = false,
            ProjectId = null,
            UserId = _currentUserService.UserId,
            History = request.History,
            UseCache = true,
            Tools = _aiTools?.GetAvailableTools()
        };

        var aiResponse = await _aiGateway.ExecuteAsync(aiRequest, ct);
        return Result.Success(ParseStructuredAiResponse(aiResponse.Content, "workspace_analytics", sw, WorkspaceSources, aiResponse.IsMock));
    }

    private async Task<Result<ErumiChatResponseDto>> BuildProjectResponseAsync(
        Guid projectId,
        ErumiChatRequestDto request,
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
        var normalized = Normalize(request.Message);
        if (IsExportQuestion(normalized))
        {
            return Result.Success(BuildProjectExportResponse(project.Id, project.Name, normalized, sw));
        }

        var data = analyticsResult.Data;
        var intent = ClassifyProjectIntent(normalized);
        if (intent == "project_team" && IsTableQuestion(normalized))
        {
            return await BuildProjectTeamResponseAsync(project.Id, project.Name, project.OwnerId, project.OwnerName, normalized, sw, ct);
        }

        if (intent == "productivity" && IsTableQuestion(normalized))
        {
            return Result.Success(BuildProjectWorkloadTableResponse(project.Id, project.Name, data, sw));
        }

        if (IsTaskTableQuestion(normalized))
        {
            return await BuildProjectTaskTableResponseAsync(project.Id, project.Name, normalized, sw, ct);
        }

        return await ExecuteProjectAiChatAsync(request, project, data, sw, ct);
    }

    private async Task<Result<ErumiChatResponseDto>> ExecuteProjectAiChatAsync(
        ErumiChatRequestDto request,
        ProjectDto project,
        ProjectAnalyticsDto data,
        Stopwatch sw,
        CancellationToken ct)
    {
        var tasksResult = await _taskService.GetByProjectAsync(project.Id, pageSize: 100, ct: ct);
        var tasks = tasksResult.Data?.Items ?? Array.Empty<TaskItemDto>();
        var tasksContext = string.Join("\n", tasks.Select(t => 
            $"- Task: {t.Title} | Trạng thái: {t.Status} | Người làm: {t.AssigneeName ?? "Chưa giao"} | Độ ưu tiên: {t.Priority} | Hạn chót: {t.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa có"}"));

        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(m => m.ProjectId == project.Id)
            .Include(m => m.User)
            .ToListAsync(ct);
        var membersContext = string.Join("\n", members.Select(m => 
            $"- {m.User.FullName} | Vai trò: {m.Role ?? "Member"}{(m.UserId == project.OwnerId ? " (PM/Owner)" : "")}"));

        var projectContext = $@"Dự án: {project.Name}
Mô tả: {project.Description ?? "Không có mô tả."}
Trạng thái: {project.Status}
Thời gian thực tế đã log: {data.TotalActualHours:0.##}h trên tổng kế hoạch {data.TotalEstimatedHours:0.##}h.
Nhiệm vụ quá hạn: {data.OverdueTasks} task.";

        var systemPrompt = $@"Bạn là Erumi, trợ lý phân tích AI đắc lực của hệ thống Qaly.
Bạn đang hỗ trợ người dùng phân tích và quản lý dự án sau:
{projectContext}

THÀNH VIÊN DỰ ÁN:
{membersContext}

DANH SÁCH NHIỆM VỤ (TASKS):
{tasksContext}

Thời gian hiện tại: {DateTimeOffset.Now:dd/MM/yyyy HH:mm}.

YÊU CẦU ĐẦU RA (BẮT BUỘC):
Bạn phải trả về câu trả lời của mình dưới dạng một đối tượng JSON duy nhất theo cấu trúc bên dưới (không viết thêm lời thoại nào ngoài JSON, không đặt JSON trong khối code markdown). Nếu bạn muốn trả về biểu đồ, metric hoặc bảng dữ liệu động từ danh sách nhiệm vụ trên, hãy tự định nghĩa chúng trong JSON:
{{
  ""reply"": ""Câu trả lời phân tích chi tiết của bạn bằng tiếng Việt hỗ trợ Markdown. Hãy trình bày đẹp mắt, súc tích."",
  ""metrics"": [
    {{ ""label"": ""Tên chỉ số"", ""value"": ""Giá trị"", ""tone"": ""neutral|good|warning|danger"", ""hint"": ""Ghi chú nhỏ (nếu cần)"" }}
  ],
  ""tables"": [
    {{
      ""title"": ""Tiêu đề bảng"",
      ""description"": ""Mô tả bảng"",
      ""columns"": [
        {{ ""key"": ""id"", ""label"": ""Cột"", ""type"": ""text|number"", ""align"": ""left|center|right"" }}
      ],
      ""rows"": [
        {{ ""id"": ""Giá trị"" }}
      ]
    }}
  ],
  ""charts"": [
    {{
      ""type"": ""bar|pie|line"",
      ""title"": ""Tiêu đề biểu đồ"",
      ""labels"": [""Nhãn 1"", ""Nhãn 2""],
      ""values"": [10.0, 20.0],
      ""unit"": ""Đơn vị""
    }}
  ],
  ""actions"": [
    {{ ""type"": ""suggested_action"", ""label"": ""Gợi ý câu hỏi tiếp theo"" }}
  ],
  ""files"": []
}}";

        var tools = await GetFilteredToolsForProjectAsync(project.Id, _currentUserService.UserId ?? Guid.Empty, ct);

        var aiRequest = new AiRequest
        {
            JobType = "project_analytics_chat",
            SystemPrompt = systemPrompt,
            Prompt = request.Message,
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = false,
            ProjectId = project.Id,
            UserId = _currentUserService.UserId,
            History = request.History,
            UseCache = true,
            Tools = tools
        };

        var aiResponse = await _aiGateway.ExecuteAsync(aiRequest, ct);
        var intent = ClassifyProjectIntent(Normalize(request.Message));

        return Result.Success(ParseStructuredAiResponse(aiResponse.Content, intent, sw, ProjectSources, aiResponse.IsMock));
    }

    private async Task<System.Collections.Generic.IList<Microsoft.Extensions.AI.AITool>?> GetFilteredToolsForProjectAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct)
    {
        if (_aiTools == null)
        {
            return null;
        }

        var allTools = _aiTools.GetAvailableTools();

        // 1. Check if the user is a system admin
        bool isAdmin = ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

        // 2. Check project owner (PM/Owner)
        var projectResult = await _projectService.GetByIdAsync(projectId, ct);
        if (!projectResult.IsSuccess || projectResult.Data == null)
        {
            return new System.Collections.Generic.List<Microsoft.Extensions.AI.AITool>();
        }

        var project = projectResult.Data;
        bool isOwner = project.OwnerId == userId;

        if (isAdmin || isOwner)
        {
            return allTools;
        }

        // 3. Check member role in project
        var member = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

        if (member == null)
        {
            return new System.Collections.Generic.List<Microsoft.Extensions.AI.AITool>();
        }

        string normalizedRole = ProjectRoleRules.NormalizeProjectRole(member.Role);
        bool isPM = ProjectRoleRules.IsProjectManager(normalizedRole);

        if (isPM)
        {
            return allTools;
        }

        // 4. For normal members and task assignees:
        var assignedTasksResult = await _taskService.GetByProjectAsync(
            projectId: projectId,
            assigneeId: userId,
            pageSize: 1,
            ct: ct);

        bool isAssignee = assignedTasksResult.IsSuccess 
            && assignedTasksResult.Data != null 
            && assignedTasksResult.Data.TotalCount > 0;

        var allowedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GetProjectSummary",
            "GetOverdueTasks",
            "GetMemberWorkload",
            "SearchKnowledge",
            "GetMyTimeLogs",
            "SuggestTaskAssignment",
            "AddComment",
            "StartTimeTracking",
            "StopTimeTracking"
        };

        if (isAssignee)
        {
            allowedToolNames.Add("UpdateTaskStatus");
        }

        return allTools
            .OfType<AIFunction>()
            .Where(t => allowedToolNames.Contains(t.Metadata.Name))
            .Cast<AITool>()
            .ToList();
    }

    private static ErumiChatResponseDto ParseStructuredAiResponse(
        string rawContent,
        string intent,
        Stopwatch sw,
        IReadOnlyList<string> sources,
        bool isMock)
    {
        sw.Stop();
        
        string reply = rawContent;
        var metrics = new List<ErumiMetricDto>();
        var tables = new List<ErumiTableDto>();
        var charts = new List<ErumiChartDto>();
        var actions = new List<ErumiActionDto>();
        var files = new List<ErumiFileDto>();
        double confidence = isMock ? 0.5 : 0.9;
        string confidenceReason = isMock ? "Hệ thống đang hoạt động ở chế độ fallback ngoại tuyến." : "Dữ liệu được phân tích bởi mô hình AI.";
        
        try
        {
            var cleaned = rawContent.Trim();
            if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(7);
            }
            if (cleaned.EndsWith("```", StringComparison.Ordinal))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }
            cleaned = cleaned.Trim();

            int firstBrace = cleaned.IndexOf('{');
            int lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            var doc = System.Text.Json.JsonDocument.Parse(cleaned);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("reply", out var replyProp))
            {
                reply = replyProp.GetString() ?? rawContent;
            }

            if (root.TryGetProperty("metrics", out var metricsProp) && metricsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in metricsProp.EnumerateArray())
                {
                    string label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                    string val = el.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    string? tone = el.TryGetProperty("tone", out var t) ? t.GetString() : null;
                    string? hint = el.TryGetProperty("hint", out var h) ? h.GetString() : null;
                    if (!string.IsNullOrEmpty(label))
                    {
                        metrics.Add(new ErumiMetricDto(label, val, tone, hint));
                    }
                }
            }

            if (root.TryGetProperty("tables", out var tablesProp) && tablesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in tablesProp.EnumerateArray())
                {
                    string title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    string? description = el.TryGetProperty("description", out var d) ? d.GetString() : null;
                    
                    var cols = new List<ErumiTableColumnDto>();
                    if (el.TryGetProperty("columns", out var colsProp) && colsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var colEl in colsProp.EnumerateArray())
                        {
                            string key = colEl.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                            string label = colEl.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                            string type = colEl.TryGetProperty("type", out var typ) ? typ.GetString() ?? "text" : "text";
                            string align = colEl.TryGetProperty("align", out var al) ? al.GetString() ?? "left" : "left";
                            if (!string.IsNullOrEmpty(key))
                            {
                                cols.Add(new ErumiTableColumnDto(key, label, type, align));
                            }
                        }
                    }

                    var rows = new List<IReadOnlyDictionary<string, object?>>();
                    if (el.TryGetProperty("rows", out var rowsProp) && rowsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var rowEl in rowsProp.EnumerateArray())
                        {
                            var rowDict = new Dictionary<string, object?>();
                            foreach (var prop in rowEl.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    rowDict[prop.Name] = prop.Value.GetDouble();
                                }
                                else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.True || prop.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                                {
                                    rowDict[prop.Name] = prop.Value.GetBoolean();
                                }
                                else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
                                {
                                    rowDict[prop.Name] = null;
                                }
                                else
                                {
                                    rowDict[prop.Name] = prop.Value.GetString();
                                }
                            }
                            rows.Add(rowDict);
                        }
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        tables.Add(new ErumiTableDto(title, cols, rows, description));
                    }
                }
            }

            if (root.TryGetProperty("charts", out var chartsProp) && chartsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in chartsProp.EnumerateArray())
                {
                    string type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "bar" : "bar";
                    string title = el.TryGetProperty("title", out var tit) ? tit.GetString() ?? "" : "";
                    string? unit = el.TryGetProperty("unit", out var u) ? u.GetString() : null;
                    
                    var labels = new List<string>();
                    if (el.TryGetProperty("labels", out var labelsProp) && labelsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var labelEl in labelsProp.EnumerateArray())
                        {
                            labels.Add(labelEl.GetString() ?? "");
                        }
                    }

                    var values = new List<double>();
                    if (el.TryGetProperty("values", out var valuesProp) && valuesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var valEl in valuesProp.EnumerateArray())
                        {
                            values.Add(valEl.GetDouble());
                        }
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        charts.Add(new ErumiChartDto(type, title, labels, values, unit));
                    }
                }
            }

            if (root.TryGetProperty("actions", out var actionsProp) && actionsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in actionsProp.EnumerateArray())
                {
                    string type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "suggested_action" : "suggested_action";
                    string label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(label))
                    {
                        actions.Add(new ErumiActionDto(type, label, null, false));
                    }
                }
            }

            if (root.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in filesProp.EnumerateArray())
                {
                    string label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                    string format = el.TryGetProperty("format", out var f) ? f.GetString() ?? "xlsx" : "xlsx";
                    string url = el.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    string? description = el.TryGetProperty("description", out var d) ? d.GetString() : null;
                    if (!string.IsNullOrEmpty(url))
                    {
                        files.Add(new ErumiFileDto(label, format, url, description));
                    }
                }
            }

            if (root.TryGetProperty("confidence", out var confProp))
            {
                if (confProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    confidence = confProp.GetDouble();
                }
                else if (confProp.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(confProp.GetString(), out var parsedConf))
                {
                    confidence = parsedConf;
                }
            }

            if (root.TryGetProperty("confidence_reason", out var confReasonProp))
            {
                confidenceReason = confReasonProp.GetString() ?? confidenceReason;
            }
        }
        catch
        {
            reply = rawContent;
        }

        return new ErumiChatResponseDto(
            reply,
            metrics,
            tables,
            charts,
            actions,
            files,
            sources,
            confidence,
            UsedAi: !isMock,
            intent,
            LatencyMs: (int)sw.ElapsedMilliseconds,
            ConfidenceReason: confidenceReason);
    }

    private static ErumiChatResponseDto BuildUploadedFileResponse(
        IReadOnlyList<ErumiUploadedFileDto> files,
        Stopwatch sw)
    {
        var readableFiles = files
            .Where(file => file.Headers is { Count: > 0 } && file.PreviewRows is { Count: > 0 })
            .Take(2)
            .ToList();

        var tables = new List<ErumiTableDto>();
        foreach (var file in readableFiles)
        {
            var headers = file.Headers ?? Array.Empty<string>();
            var columns = headers
                .Select((header, index) => new ErumiTableColumnDto($"col{index}", string.IsNullOrWhiteSpace(header) ? $"Cột {index + 1}" : header))
                .ToArray();

            var rows = (file.PreviewRows ?? Array.Empty<IReadOnlyList<string>>())
                .Take(8)
                .Select(row =>
                {
                    var values = new Dictionary<string, object?>();
                    for (var i = 0; i < columns.Length; i++)
                    {
                        values[columns[i].Key] = i < row.Count ? row[i] : string.Empty;
                    }

                    return (IReadOnlyDictionary<string, object?>)values;
                })
                .ToList();

            tables.Add(new ErumiTableDto(
                $"Preview file {file.FileName}",
                columns,
                rows,
                $"Tổng {file.TotalRowCount ?? rows.Count} dòng, {headers.Count} cột. Hiển thị tối đa 8 dòng đầu."));
        }

        var metrics = files.Select(file =>
        {
            var tone = string.IsNullOrWhiteSpace(file.Error) ? "good" : "warning";
            var value = file.TotalRowCount.HasValue ? $"{file.TotalRowCount.Value} dòng" : $"{Math.Round(file.Size / 1024d, 1):0.#} KB";
            return new ErumiMetricDto(file.FileName, value, tone, string.IsNullOrWhiteSpace(file.Error) ? file.ContentType : file.Error);
        }).ToList();

        var failedCount = files.Count(file => !string.IsNullOrWhiteSpace(file.Error));
        var reply = readableFiles.Count > 0
            ? $"Mình đã đọc nhanh **{readableFiles.Count} file dạng bảng** và dựng preview để bạn kiểm tra. Bạn có thể hỏi tiếp như: `cột nào giống task`, `lọc dòng quá hạn`, hoặc `gợi ý import vào dự án`."
            : "Mình đã nhận file, nhưng chưa đọc được nội dung dạng bảng. Hiện luồng nhanh hỗ trợ tốt nhất CSV/XLSX/TXT có cấu trúc; PDF/DOCX phân tích sâu sẽ cần pipeline file riêng.";

        if (failedCount > 0 && readableFiles.Count > 0)
        {
            reply += $" Có **{failedCount} file** chưa đọc được bằng bộ parse hiện tại.";
        }

        return CreateResponse(
            reply,
            "uploaded_file_preview",
            sw,
            metrics,
            tables: tables,
            sources: UploadedFileSources,
            confidence: readableFiles.Count > 0 ? 0.84 : 0.64);
    }

    private async Task<Result<ErumiChatResponseDto>> BuildWorkspaceProjectComparisonResponseAsync(
        string normalized,
        WorkspaceAnalyticsDto workspaceData,
        Stopwatch sw,
        CancellationToken ct)
    {
        var projectsResult = await _projectService.GetAllAsync(pageSize: 100, ct: ct);
        if (!projectsResult.IsSuccess || projectsResult.Data == null)
        {
            return Result.Failure<ErumiChatResponseDto>(
                projectsResult.Error ?? "Không thể lấy danh sách dự án.",
                projectsResult.StatusCode);
        }

        var projects = projectsResult.Data.Items
            .Where(project => !string.Equals(project.Status, "Archived", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        if (projects.Count == 0)
        {
            return Result.Success(CreateResponse(
                "Mình chưa thấy dự án đang hoạt động nào để lập bảng so sánh.",
                "compare_projects",
                sw,
                sources: WorkspaceSources,
                confidence: 0.82));
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var comparisonLabels = new List<string>();
        var progressValues = new List<double>();
        var overdueValues = new List<double>();
        foreach (var project in projects)
        {
            var analyticsResult = await _analyticsService.GetProjectAnalyticsAsync(project.Id, ct);
            if (!analyticsResult.IsSuccess || analyticsResult.Data == null)
            {
                continue;
            }

            var analytics = analyticsResult.Data;
            var progress = Percent(analytics.DoneTasks, analytics.TotalTasks);
            var risk = ProjectRiskLabel(analytics);
            comparisonLabels.Add(project.Name);
            progressValues.Add(progress);
            overdueValues.Add(analytics.OverdueTasks);

            rows.Add(new Dictionary<string, object?>
            {
                ["name"] = project.Name,
                ["status"] = project.Status,
                ["totalTasks"] = analytics.TotalTasks,
                ["doneTasks"] = analytics.DoneTasks,
                ["progress"] = $"{progress:0.#}%",
                ["overdueTasks"] = analytics.OverdueTasks,
                ["actualHours"] = $"{analytics.TotalActualHours:0.##}h",
                ["risk"] = risk
            });
        }

        if (rows.Count == 0)
        {
            return Result.Success(CreateResponse(
                "Mình lấy được danh sách dự án, nhưng chưa tổng hợp được số liệu analytics để so sánh.",
                "compare_projects",
                sw,
                sources: WorkspaceSources,
                confidence: 0.72));
        }

        var riskProjects = rows.Count(row => string.Equals(row["risk"]?.ToString(), "Cao", StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(row["risk"]?.ToString(), "Trung bình", StringComparison.OrdinalIgnoreCase));
        var table = new ErumiTableDto(
            "So sánh dự án",
            ProjectComparisonColumns(),
            rows,
            "Tối đa 8 dự án đang hoạt động, sắp xếp theo dữ liệu người dùng có quyền truy cập.");

        var charts = new List<ErumiChartDto>
        {
            new(
                "bar",
                ContainsAny(normalized, "qua han", "rui ro", "risk") ? "Task quá hạn theo dự án" : "Tiến độ theo dự án",
                comparisonLabels,
                ContainsAny(normalized, "qua han", "rui ro", "risk") ? overdueValues : progressValues,
                ContainsAny(normalized, "qua han", "rui ro", "risk") ? "task" : "%")
        };

        var metrics = new List<ErumiMetricDto>
        {
            new("Dự án so sánh", rows.Count.ToString(CultureInfo.InvariantCulture), "neutral"),
            new("Dự án có rủi ro", riskProjects.ToString(CultureInfo.InvariantCulture), riskProjects > 0 ? "warning" : "good"),
            new("Tổng task workspace", workspaceData.TotalTasks.ToString(CultureInfo.InvariantCulture), "neutral")
        };

        var reply = riskProjects > 0
            ? $"Mình đã lập bảng so sánh **{rows.Count} dự án**. Có **{riskProjects} dự án** đang có dấu hiệu cần theo dõi do tiến độ hoặc task quá hạn."
            : $"Mình đã lập bảng so sánh **{rows.Count} dự án**. Nhìn chung các dự án chưa có tín hiệu rủi ro lớn từ số liệu task hiện tại.";

        return Result.Success(CreateResponse(
            reply,
            "compare_projects",
            sw,
            metrics,
            tables: new[] { table },
            charts: charts,
            sources: ConcatSources(WorkspaceSources, "Projects")));
    }

    private async Task<Result<ErumiChatResponseDto>> BuildProjectTeamResponseAsync(
        Guid projectId,
        string projectName,
        Guid ownerId,
        string ownerName,
        string normalized,
        Stopwatch sw,
        CancellationToken ct)
    {
        var members = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .Include(member => member.User)
            .ToListAsync(ct);

        var currentUserId = _currentUserService.UserId;
        var requesterIsOwner = currentUserId.HasValue && currentUserId.Value == ownerId;
        var requesterRole = currentUserId.HasValue
            ? members.FirstOrDefault(member => member.UserId == currentUserId.Value)?.Role
            : null;
        var safeOwnerName = string.IsNullOrWhiteSpace(ownerName) ? "người tạo dự án" : ownerName;

        string reply;
        ErumiTableDto? table = null;
        if (IsBossQuestion(normalized))
        {
            reply = requesterIsOwner
                ? $"Bạn là người tạo dự án **{projectName}**, nên trong dự án này bạn chính là **sếp/Owner mặc định**."
                : $"Sếp/Owner mặc định của dự án **{projectName}** là **{safeOwnerName}** - người tạo dự án này.";

            if (!string.IsNullOrWhiteSpace(requesterRole))
            {
                reply += $" Vai trò hiện tại của bạn trong dự án là **{requesterRole}**.";
            }
        }
        else
        {
            var memberRows = members
                .OrderBy(member => member.UserId == ownerId ? 0 : 1)
                .ThenBy(member => member.Role)
                .ThenBy(member => member.User.FullName)
                .Select(member =>
                {
                    var name = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email : member.User.FullName;
                    var role = string.IsNullOrWhiteSpace(member.Role) ? "Member" : member.Role;
                    var ownerSuffix = member.UserId == ownerId ? " - sếp/Owner mặc định" : string.Empty;
                    return $"- **{name}**: {role}{ownerSuffix}";
                })
                .ToList();

            var ownerLine = requesterIsOwner
                ? "Bạn là người tạo dự án nên bạn là **sếp/Owner mặc định**."
                : $"Sếp/Owner mặc định là **{safeOwnerName}**.";

            reply = memberRows.Count == 0
                ? $"Mình chưa thấy danh sách thành viên của **{projectName}**. {ownerLine}"
                : $"Dự án **{projectName}** hiện có **{memberRows.Count} thành viên**. {ownerLine}\n\n{string.Join("\n", memberRows)}";

            table = BuildTeamTable(members, ownerId);
        }

        return Result.Success(CreateResponse(
            reply,
            "project_team",
            sw,
            tables: table == null ? null : new[] { table },
            sources: ProjectSources));
    }

    private async Task<Result<ErumiChatResponseDto>> BuildProjectTaskTableResponseAsync(
        Guid projectId,
        string projectName,
        string normalized,
        Stopwatch sw,
        CancellationToken ct)
    {
        var tasksResult = await _taskService.GetByProjectAsync(projectId, pageSize: 100, ct: ct);
        if (!tasksResult.IsSuccess || tasksResult.Data == null)
        {
            return Result.Failure<ErumiChatResponseDto>(
                tasksResult.Error ?? "Không thể lấy danh sách task.",
                tasksResult.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;
        var overdueOnly = ContainsAny(normalized, "qua han", "tre han", "deadline", "risk", "rui ro", "cham tien do");
        var tasks = tasksResult.Data.Items
            .Where(task => !overdueOnly || (task.DueDate.HasValue && task.DueDate.Value < now && !IsDoneStatus(task.Status)))
            .OrderByDescending(task => task.DueDate.HasValue && task.DueDate.Value < now && !IsDoneStatus(task.Status))
            .ThenBy(task => task.DueDate ?? DateTimeOffset.MaxValue)
            .ThenByDescending(task => PriorityWeight(task.Priority))
            .Take(20)
            .ToList();

        var table = new ErumiTableDto(
            overdueOnly ? "Task quá hạn cần chú ý" : "Danh sách task dự án",
            TaskTableColumns(),
            tasks.Select(task => (IReadOnlyDictionary<string, object?>)BuildTaskRow(task, now)).ToList(),
            overdueOnly
                ? "Các task chưa Done và có hạn chót nhỏ hơn thời điểm hiện tại."
                : "Tối đa 20 task đầu tiên theo mức độ cần chú ý.");

        var reply = tasks.Count == 0
            ? overdueOnly
                ? $"Dự án **{projectName}** hiện không có task quá hạn trong phạm vi bạn có quyền xem."
                : $"Mình chưa thấy task nào trong dự án **{projectName}** để lập bảng."
            : overdueOnly
                ? $"Mình tìm thấy **{tasks.Count} task quá hạn** trong dự án **{projectName}** và đã lập bảng để bạn xử lý nhanh."
                : $"Mình đã lập bảng **{tasks.Count} task** của dự án **{projectName}** để bạn dễ so sánh trạng thái, priority và deadline.";

        var metrics = new List<ErumiMetricDto>
        {
            new(overdueOnly ? "Task quá hạn" : "Task hiển thị", tasks.Count.ToString(CultureInfo.InvariantCulture), overdueOnly && tasks.Count > 0 ? "danger" : "neutral")
        };

        return Result.Success(CreateResponse(
            reply,
            overdueOnly ? "list_overdue_tasks" : "show_tasks_as_table",
            sw,
            metrics,
            tables: new[] { table },
            sources: ConcatSources(ProjectSources, "TaskService")));
    }

    private static ErumiChatResponseDto BuildProjectWorkloadTableResponse(
        Guid projectId,
        string projectName,
        ProjectAnalyticsDto data,
        Stopwatch sw)
    {
        var memberRows = data.MemberProductivity
            .OrderByDescending(item => item.AssignedTasks)
            .ThenBy(item => item.FullName)
            .Select(item => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["member"] = item.FullName,
                ["assignedTasks"] = item.AssignedTasks,
                ["doneTasks"] = item.DoneTasks,
                ["openTasks"] = Math.Max(0, item.AssignedTasks - item.DoneTasks),
                ["loggedHours"] = $"{item.LoggedHours:0.##}h",
                ["load"] = WorkloadLabel(item.AssignedTasks - item.DoneTasks)
            })
            .ToList();

        var table = new ErumiTableDto(
            "Workload theo thành viên",
            WorkloadTableColumns(),
            memberRows,
            "Số liệu lấy từ task được phân công và time log của dự án.");

        var busiest = data.MemberProductivity.OrderByDescending(item => item.AssignedTasks - item.DoneTasks).FirstOrDefault();
        var reply = busiest == null
            ? $"Dự án **{projectName}** chưa có dữ liệu thành viên để lập bảng workload."
            : $"Mình đã lập bảng workload của **{projectName}**. Người đang có nhiều task mở nhất là **{busiest.FullName}** với **{Math.Max(0, busiest.AssignedTasks - busiest.DoneTasks)} task**.";

        return CreateResponse(
            reply,
            "list_member_workload",
            sw,
            tables: new[] { table },
            sources: ConcatSources(ProjectSources, "ProjectMembers"));
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

    private static ErumiFileDto[] BuildProjectExportFiles(Guid projectId, string projectName)
        => new[]
        {
            new ErumiFileDto(
                "Tải báo cáo Excel",
                "xlsx",
                $"/api/ai/export/{projectId}?format=excel",
                $"Báo cáo dữ liệu dự án {projectName}."),
            new ErumiFileDto(
                "Tải báo cáo Word",
                "docx",
                $"/api/ai/export/{projectId}?format=word",
                $"Báo cáo văn bản dự án {projectName}.")
        };

    private static ErumiChatResponseDto BuildProjectExportResponse(Guid projectId, string projectName, string normalized, Stopwatch sw)
    {
        var wantsExcel = ContainsAny(normalized, "excel", "xlsx", "bang tinh", "spreadsheet");
        var wantsWord = ContainsAny(normalized, "word", "docx", "van ban", "document");
        var wantsPdf = ContainsAny(normalized, "pdf");

        if (wantsPdf && !wantsExcel && !wantsWord)
        {
            return CreateResponse(
                "Hiện luồng export nhanh của Erumi chưa có PDF trực tiếp. Mình có thể xuất **Excel** hoặc **Word** cho dự án này trước; PDF sẽ cần bổ sung service chuyển đổi riêng.",
                "export_project_report",
                sw,
                sources: ConcatSources(ProjectSources, "AiExportService"),
                confidence: 0.82);
        }

        var allFiles = BuildProjectExportFiles(projectId, projectName);
        var files = wantsExcel && !wantsWord
            ? allFiles.Where(file => file.Format == "xlsx").ToArray()
            : wantsWord && !wantsExcel
                ? allFiles.Where(file => file.Format == "docx").ToArray()
                : allFiles;

        var reply = files.Length == 1
            ? $"Mình đã chuẩn bị link tải **{files[0].Format.ToUpperInvariant()}** cho báo cáo dự án **{projectName}** theo yêu cầu của bạn."
            : $"Mình đã chuẩn bị link tải **Excel** và **Word** cho báo cáo dự án **{projectName}** theo yêu cầu của bạn.";

        return CreateResponse(
            reply,
            "export_project_report",
            sw,
            files: files,
            sources: ConcatSources(ProjectSources, "AiExportService"),
            confidence: 0.94);
    }

    private static ErumiTableColumnDto[] ProjectComparisonColumns()
        => new[]
        {
            new ErumiTableColumnDto("name", "Dự án"),
            new ErumiTableColumnDto("status", "Trạng thái"),
            new ErumiTableColumnDto("totalTasks", "Tổng task", "number", "right"),
            new ErumiTableColumnDto("doneTasks", "Hoàn thành", "number", "right"),
            new ErumiTableColumnDto("progress", "Tiến độ", "text", "right"),
            new ErumiTableColumnDto("overdueTasks", "Quá hạn", "number", "right"),
            new ErumiTableColumnDto("actualHours", "Giờ log", "text", "right"),
            new ErumiTableColumnDto("risk", "Rủi ro")
        };

    private static ErumiTableColumnDto[] TaskTableColumns()
        => new[]
        {
            new ErumiTableColumnDto("title", "Task"),
            new ErumiTableColumnDto("status", "Trạng thái"),
            new ErumiTableColumnDto("priority", "Ưu tiên"),
            new ErumiTableColumnDto("assignee", "Người làm"),
            new ErumiTableColumnDto("dueDate", "Deadline"),
            new ErumiTableColumnDto("overdueDays", "Trễ", "text", "right")
        };

    private static ErumiTableColumnDto[] WorkloadTableColumns()
        => new[]
        {
            new ErumiTableColumnDto("member", "Thành viên"),
            new ErumiTableColumnDto("assignedTasks", "Được giao", "number", "right"),
            new ErumiTableColumnDto("doneTasks", "Hoàn thành", "number", "right"),
            new ErumiTableColumnDto("openTasks", "Đang mở", "number", "right"),
            new ErumiTableColumnDto("loggedHours", "Giờ log", "text", "right"),
            new ErumiTableColumnDto("load", "Tải")
        };

    private static ErumiTableColumnDto[] TeamTableColumns()
        => new[]
        {
            new ErumiTableColumnDto("name", "Thành viên"),
            new ErumiTableColumnDto("role", "Vai trò"),
            new ErumiTableColumnDto("owner", "Owner")
        };

    private static ErumiTableDto BuildTeamTable(IReadOnlyList<ProjectMember> members, Guid ownerId)
        => new(
            "Thành viên dự án",
            TeamTableColumns(),
            members
                .OrderBy(member => member.UserId == ownerId ? 0 : 1)
                .ThenBy(member => member.Role)
                .ThenBy(member => member.User.FullName)
                .Select(member => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                {
                    ["name"] = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email : member.User.FullName,
                    ["role"] = string.IsNullOrWhiteSpace(member.Role) ? "Member" : member.Role,
                    ["owner"] = member.UserId == ownerId ? "Có" : ""
                })
                .ToList());

    private static Dictionary<string, object?> BuildTaskRow(TaskItemDto task, DateTimeOffset now)
    {
        var overdueDays = task.DueDate.HasValue && task.DueDate.Value < now && !IsDoneStatus(task.Status)
            ? Math.Max(1, (int)Math.Ceiling((now - task.DueDate.Value).TotalDays)).ToString(CultureInfo.InvariantCulture) + " ngày"
            : "";

        return new Dictionary<string, object?>
        {
            ["title"] = task.Title,
            ["status"] = task.Status,
            ["priority"] = task.Priority,
            ["assignee"] = string.IsNullOrWhiteSpace(task.AssigneeName) ? "Chưa phân công" : task.AssigneeName,
            ["dueDate"] = task.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa có",
            ["overdueDays"] = overdueDays
        };
    }

    private static string BuildSummaryReply(string projectName, string? description, ProjectAnalyticsDto data)
    {
        var progress = Percent(data.DoneTasks, data.TotalTasks);
        var about = string.IsNullOrWhiteSpace(description)
            ? $"Dự án **{projectName}** hiện chưa có mô tả chi tiết trong hệ thống."
            : $"Dự án **{projectName}** là về: {description.Trim()}";
        var overdueText = data.OverdueTasks == 0
            ? "không có task quá hạn"
            : $"có **{data.OverdueTasks} task quá hạn** cần xử lý";

        return $"{about}\n\nTình hình hiện tại: có **{data.TotalTasks} task**, đã hoàn thành **{data.DoneTasks} task** (**{progress:0.#}%**), đang làm **{data.InProgressTasks} task** và {overdueText}. Tổng thời gian đã log là **{data.TotalActualHours:0.##}h** trên kế hoạch **{data.TotalEstimatedHours:0.##}h**.";
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

        return $"Năng suất của **{projectName}**: **{mostDone.FullName}** đang hoàn thành nhiều task nhất ({mostDone.DoneTasks}), **{busiest.FullName}** có workload cao nhất ({busiest.AssignedTasks} task), và **{mostLogged.FullName}** log nhiều thời gian nhất ({mostLogged.LoggedHours:0.##}h). Nên theo dõi người có workload cao trước để tránh nghẽn tiến độ.";
    }

    private static string BuildProjectAnalysisReply(string projectName, ProjectAnalyticsDto data)
    {
        var progress = Percent(data.DoneTasks, data.TotalTasks);
        return $"Mình đã phân tích tổng quan dự án **{projectName}** từ dữ liệu task, workload và time log. Hiện dự án hoàn thành **{data.DoneTasks}/{data.TotalTasks} task** (**{progress:0.#}%**), có **{data.InProgressTasks} task đang làm** và **{data.OverdueTasks} task quá hạn**. Các biểu đồ bên dưới thể hiện phân bổ trạng thái, workload thành viên và nhịp hoàn thành 14 ngày gần nhất.";
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
        IReadOnlyList<ErumiTableDto>? tables = null,
        IReadOnlyList<ErumiChartDto>? charts = null,
        IReadOnlyList<ErumiActionDto>? actions = null,
        IReadOnlyList<ErumiFileDto>? files = null,
        IReadOnlyList<string>? sources = null,
        double confidence = 0.9,
        bool usedAi = false,
        string? confidenceReason = null)
    {
        sw.Stop();
        return new ErumiChatResponseDto(
            reply,
            metrics ?? Array.Empty<ErumiMetricDto>(),
            tables ?? Array.Empty<ErumiTableDto>(),
            charts ?? Array.Empty<ErumiChartDto>(),
            actions ?? Array.Empty<ErumiActionDto>(),
            files ?? Array.Empty<ErumiFileDto>(),
            sources ?? Array.Empty<string>(),
            confidence,
            UsedAi: usedAi,
            intent,
            LatencyMs: (int)sw.ElapsedMilliseconds,
            ConfidenceReason: confidenceReason ?? (usedAi ? "Được phân tích bởi mô hình AI." : "Dữ liệu chính xác được truy vấn trực tiếp từ cơ sở dữ liệu hệ thống."));
    }

    private static string ClassifyProjectIntent(string normalized)
    {
        if (ContainsAny(normalized, "rui ro", "qua han", "tre han", "cham tien do", "deadline", "risk"))
        {
            return "risk";
        }

        if (ContainsAny(normalized, "nang suat", "hieu suat", "workload", "khoi luong", "qua tai", "ai dang ranh"))
        {
            return "productivity";
        }

        if (IsTeamQuestion(normalized))
        {
            return "project_team";
        }

        if (ContainsAny(normalized, "phan tich du an", "phan tich project", "bao cao phan tich", "dashboard du an", "bieu do", "chart", "visual"))
        {
            return "project_analysis";
        }

        return "project_summary";
    }

    private static bool IsTeamQuestion(string normalized)
        => IsBossQuestion(normalized)
           || ContainsAny(
               normalized,
               "dong doi",
               "team member",
               "thanh vien",
               "nhom co ai",
               "ai trong du an",
               "ai tham gia",
               "danh sach team",
               "danh sach nhom",
               "vai tro",
               "role",
               "owner",
               "nguoi tao du an",
               "chu du an",
               "project owner");

    private static bool IsWorkspaceProjectTableQuestion(string normalized)
        => ContainsAny(
            normalized,
            "so sanh du an",
            "compare project",
            "compare projects",
            "bang du an",
            "danh sach du an",
            "liet ke du an",
            "du an nao",
            "xep hang du an",
            "rank project",
            "rank du an");

    private static bool IsTaskTableQuestion(string normalized)
        => ContainsAny(
            normalized,
            "so sanh task",
            "compare task",
            "so sanh cong viec",
            "bang task",
            "bang cong viec",
            "danh sach task",
            "danh sach cong viec",
            "liet ke task",
            "liet ke cong viec",
            "task nao",
            "cong viec nao");

    private static bool IsTableQuestion(string normalized)
        => ContainsAny(normalized, "bang", "table", "danh sach", "liet ke", "so sanh", "compare", "xep hang", "rank");

    private static bool IsExportQuestion(string normalized)
        => ContainsAny(
            normalized,
            "xuat file",
            "xuat bao cao",
            "tao file",
            "tai file",
            "download",
            "export",
            "excel",
            "xlsx",
            "word",
            "docx",
            "pdf",
            "bao cao file");

    private static bool IsBossQuestion(string normalized)
        => ContainsAny(
            normalized,
            "sep",
            "cap tren",
            "quan ly cua toi",
            "leader cua toi",
            "lead cua toi",
            "ai la sep",
            "sep toi",
            "pm cua toi",
            "chu du an la ai",
            "owner la ai",
            "nguoi tao du an la ai");

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

    private static string ProjectRiskLabel(ProjectAnalyticsDto data)
    {
        if (data.TotalTasks == 0)
        {
            return "Chưa đủ dữ liệu";
        }

        var overdueRate = Percent(data.OverdueTasks, data.TotalTasks);
        var progress = Percent(data.DoneTasks, data.TotalTasks);
        if (data.OverdueTasks >= 3 || overdueRate >= 25 || progress < 40)
        {
            return "Cao";
        }

        if (data.OverdueTasks > 0 || overdueRate >= 10 || progress < 70)
        {
            return "Trung bình";
        }

        return "Thấp";
    }

    private static string WorkloadLabel(int openTasks)
        => openTasks switch
        {
            >= 8 => "Cao",
            >= 4 => "Trung bình",
            _ => "Nhẹ"
        };

    private static int PriorityWeight(string? priority)
        => priority?.ToLowerInvariant() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0
        };

    private static bool IsDoneStatus(string? status)
        => string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase);

    private static string[] ConcatSources(IReadOnlyList<string> baseSources, params string[] extraSources)
        => baseSources.Concat(extraSources).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

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
