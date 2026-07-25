using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : BaseApiController
{
    private readonly IAiService _aiService;
    private readonly IErumiChatService _erumiChatService;
    private readonly IAiWorkflowService _aiWorkflowService;
    private readonly IAiIngestionService _ingestionService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IAgentRunService _agentRunService;
    private readonly IAiPlatformQueryService _aiPlatformQueryService;
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;

    public AiController(
        IAiService aiService,
        IErumiChatService erumiChatService,
        IAiWorkflowService aiWorkflowService,
        IAiIngestionService ingestionService,
        IAnalyticsService analyticsService,
        IAgentRunService agentRunService,
        IAiPlatformQueryService aiPlatformQueryService,
        IProjectService projectService,
        ITaskService taskService)
    {
        _aiService = aiService;
        _erumiChatService = erumiChatService;
        _aiWorkflowService = aiWorkflowService;
        _ingestionService = ingestionService;
        _analyticsService = analyticsService;
        _agentRunService = agentRunService;
        _aiPlatformQueryService = aiPlatformQueryService;
        _projectService = projectService;
        _taskService = taskService;
    }

    [HttpPost("generate-plan")]
    public async Task<IActionResult> GeneratePlan(GeneratePlanRequestDto request, CancellationToken ct)
    {
        var result = await _aiService.GeneratePlanAsync(request.UserPrompt, request.ProjectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("create-plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(ConfirmPlanRequestDto request, CancellationToken ct)
    {
        Guid projectId;
        if (request.IsNewProject)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectName))
            {
                return BadRequest(new { error = "Tên dự án là bắt buộc." });
            }

            var createProjectResult = await _projectService.CreateAsync(new Qaly.Application.DTOs.Project.CreateProjectDto(
                request.ProjectName.Trim(),
                null,
                request.ProjectDescription?.Trim(),
                null,
                null,
                null
            ), ct);

            if (!createProjectResult.IsSuccess || createProjectResult.Data == null)
            {
                return StatusCode(createProjectResult.StatusCode, createProjectResult.Error);
            }

            projectId = createProjectResult.Data.Id;
        }
        else
        {
            if (request.ProjectId == null || request.ProjectId == Guid.Empty)
            {
                return BadRequest(new { error = "ProjectId là bắt buộc đối với dự án hiện tại." });
            }
            projectId = request.ProjectId.Value;
        }

        var createdTasks = new List<Qaly.Application.DTOs.Task.TaskItemDto>();
        var failedTasks = new List<CreatePlanTaskFailureDto>();
        if (request.Tasks != null)
        {
            foreach (var taskDto in request.Tasks)
            {
                if (string.IsNullOrWhiteSpace(taskDto.Title))
                {
                    failedTasks.Add(new CreatePlanTaskFailureDto(taskDto.Title, "Task title is required."));
                    continue;
                }

                var createTaskResult = await _taskService.CreateAsync(new Qaly.Application.DTOs.Task.CreateTaskDto(
                    taskDto.Title.Trim(),
                    taskDto.Description?.Trim(),
                    taskDto.Priority,
                    taskDto.DueDate,
                    taskDto.EstimatedHours,
                    projectId,
                    taskDto.AssigneeId
                ), ct);

                if (createTaskResult.IsSuccess && createTaskResult.Data != null)
                {
                    createdTasks.Add(createTaskResult.Data);
                }
                else
                {
                    failedTasks.Add(new CreatePlanTaskFailureDto(taskDto.Title.Trim(), createTaskResult.Error ?? "Task could not be created."));
                }
            }
        }

        if (createdTasks.Count == 0 && failedTasks.Count > 0)
        {
            return BadRequest(new
            {
                error = "Không thể tạo công việc nào từ kế hoạch AI.",
                projectId,
                taskCount = 0,
                failedTasks
            });
        }

        return Ok(new { projectId, taskCount = createdTasks.Count, failedTasks });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        await _ingestionService.SyncAllDataAsync();
        return Ok(new { message = "Data sync to vector database completed." });
    }

    [HttpPost("jobs")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateJob(CreateAiJobDto dto, CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateJobAsync(dto, idempotencyKey, requestId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs(
        [FromQuery] Guid? projectId,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.ListJobsAsync(projectId, status, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid jobId, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.GetJobAsync(jobId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("jobs/{jobId:guid}/result")]
    public async Task<IActionResult> GetJobResult(Guid jobId, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.GetJobResultAsync(jobId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("jobs/{jobId:guid}/retry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryJob(Guid jobId, RetryAiJobDto dto, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.RetryJobAsync(jobId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("jobs/{jobId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelJob(Guid jobId, CancelAiJobDto dto, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.CancelJobAsync(jobId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("drafts")]
    public async Task<IActionResult> ListDrafts(
        [FromQuery] Guid? projectId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.ListDraftsAsync(projectId, type, status, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("drafts/{draftId:guid}")]
    public async Task<IActionResult> GetDraft(Guid draftId, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.GetDraftAsync(draftId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("drafts/{draftId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PatchDraft(Guid draftId, PatchAiDraftDto dto, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.PatchDraftAsync(draftId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("drafts/{draftId:guid}/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDraft(Guid draftId, ConfirmAiDraftDto dto, CancellationToken ct = default)
    {
        var idempotencyKey = dto.IdempotencyKey ?? Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || string.IsNullOrWhiteSpace(dto.RowVersion))
        {
            return BadRequest(new
            {
                errorCode = Qaly.Application.Common.Models.AiErrorCodes.InvalidRequest,
                error = "Idempotency-Key and rowVersion are required."
            });
        }

        dto = dto with { IdempotencyKey = idempotencyKey };
        var result = await _aiWorkflowService.ConfirmDraftAsync(draftId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("agent-runs")]
    public async Task<IActionResult> StartAgentRun(StartAgentRunDto request, CancellationToken ct = default)
    {
        var result = await _agentRunService.StartAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("agent-runs/{runId:guid}")]
    public async Task<IActionResult> GetAgentRun(Guid runId, CancellationToken ct = default)
    {
        var result = await _agentRunService.GetAsync(runId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("agent-runs/{runId:guid}/approve")]
    public async Task<IActionResult> ApproveAgentRun(Guid runId, ApproveAgentRunDto request, CancellationToken ct = default)
    {
        var result = await _agentRunService.ApproveAsync(runId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("drafts/{draftId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDraft(Guid draftId, RejectAiDraftDto dto, CancellationToken ct = default)
    {
        var idempotencyKey = dto.IdempotencyKey ?? Request.Headers["Idempotency-Key"].ToString();
        dto = dto with { IdempotencyKey = idempotencyKey };
        var result = await _aiWorkflowService.RejectDraftAsync(draftId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(
        [FromQuery] Guid? projectId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.GetUsageAsync(projectId, from, to, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("budget")]
    public async Task<IActionResult> GetBudget([FromQuery] Guid projectId, CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.GetBudgetAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("budget")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBudget(
        [FromQuery] Guid projectId,
        UpdateAiBudgetPolicyDto dto,
        CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.UpdateBudgetAsync(projectId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.GetHealthAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("meetings/{meetingId:guid}/extract-actions")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueMeetingActionExtraction(
        Guid meetingId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "meeting_action_extract",
            "meeting_action_extract.v4",
            request.ProjectId,
            "meeting",
            meetingId,
            request,
            ct);

    [HttpPost("groups/{groupId:guid}/summaries")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueGroupSummary(
        Guid groupId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "chat_summary",
            "chat_summary.v4",
            request.ProjectId,
            "group",
            groupId,
            request,
            ct);

    [HttpPost("task-drafts/from-source")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueTaskDraft(
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "task_draft",
            "task_draft.v4",
            request.ProjectId,
            request.SourceType,
            request.SourceEntityId,
            request,
            ct);

    [HttpPost("tasks/{taskId:guid}/recommend-assignees")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueAssigneeRecommendation(
        Guid taskId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "assignee_recommendation",
            "assignee_recommendation.v4",
            request.ProjectId,
            "task",
            taskId,
            request,
            ct);

    [HttpPost("tasks/{taskId:guid}/breakdown")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueTaskBreakdown(
        Guid taskId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "task_breakdown",
            "task_breakdown.v4",
            request.ProjectId,
            "task",
            taskId,
            request,
            ct);

    [HttpPost("tasks/{taskId:guid}/acceptance-checklist")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueAcceptanceChecklist(
        Guid taskId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "acceptance_checklist",
            "acceptance_checklist.v4",
            request.ProjectId,
            "task",
            taskId,
            request,
            ct);

    [HttpPost("projects/{projectId:guid}/progress-summary")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueProjectProgressSummary(
        Guid projectId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "progress_summary",
            "progress_summary.v4",
            projectId,
            "project",
            projectId,
            request,
            ct);

    [HttpPost("projects/{projectId:guid}/suggest-resolution")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueProjectDelayResolution(
        Guid projectId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "project_delay_resolution",
            "project_delay_resolution.v4",
            projectId,
            "project",
            projectId,
            request,
            ct);

    [HttpPost("projects/{projectId:guid}/sprints/{sprintId:guid}/progress-summary")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueSprintProgressSummary(
        Guid projectId,
        Guid sprintId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => EnqueueFunctionAsync(
            "progress_summary",
            "progress_summary.v4",
            projectId,
            "sprint",
            sprintId,
            request,
            ct);

    [HttpPost("priority")]
    public async Task<IActionResult> SuggestPriority(AiPriorityRequest request)
    {
        var result = await _aiService.SuggestTaskPriorityAsync(
            request.Title,
            request.Description ?? string.Empty,
            request.ProjectContext ?? string.Empty);

        return Ok(new { suggestion = result });
    }

    [HttpGet("projects/{projectId:guid}/summary")]
    public async Task<IActionResult> ProjectSummary(Guid projectId)
        => Ok(new { summary = await _aiService.GenerateProjectSummaryAsync(projectId) });

    [HttpGet("projects/{projectId:guid}/risks")]
    public async Task<IActionResult> ProjectRisks(Guid projectId)
        => Ok(new { risks = await _aiService.AnalyzeProjectRisksAsync(projectId) });

    [HttpGet("projects/{projectId:guid}/insights")]
    public async Task<IActionResult> ProjectInsights(Guid projectId)
    {
        var analyticsResult = await _analyticsService.GetProjectAnalyticsAsync(projectId);
        if (!analyticsResult.IsSuccess) return StatusCode(analyticsResult.StatusCode, analyticsResult.Error);

        var dataJson = System.Text.Json.JsonSerializer.Serialize(analyticsResult.Data);
        var insights = await _aiService.GenerateAnalyticsInsightsAsync(projectId, dataJson);
        return Ok(new { insights });
    }

    [HttpGet("tasks/{taskId:guid}/assignment")]
    public async Task<IActionResult> SuggestAssignment(Guid taskId, [FromQuery] Guid projectId)
        => Ok(new { suggestion = await _aiService.SuggestTaskAssignmentAsync(taskId, projectId) });

    [HttpGet("tasks/{taskId:guid}/assignment-insight")]
    public async Task<IActionResult> AssignmentInsight(Guid taskId, [FromQuery] Guid projectId, CancellationToken ct = default)
    {
        var result = await _aiService.GetTaskAssignmentInsightAsync(taskId, projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] Guid? projectId = null)
        => Ok(new { items = await _aiService.SmartSearchAsync(query, projectId) });

    [HttpPost("subtasks")]
    public async Task<IActionResult> GenerateSubtasks(AiSubtasksRequest request)
        => Ok(new { items = await _aiService.GenerateSubtasksAsync(request.Title, request.Description ?? string.Empty) });

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(AiChatRequest request)
        => Ok(new { reply = await _aiService.ChatAsync(request.Message, request.ProjectId, request.Mode, request.History) });

    [HttpPost("chat/fast")]
    public async Task<IActionResult> ChatFast(ErumiChatRequestDto request, CancellationToken ct)
    {
        var result = await _erumiChatService.ChatFastAsync(request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("chat/stream")]
    public async Task ChatStreaming(AiChatRequest request)
    {
        Response.ContentType = "text/plain";
        await foreach (var token in _aiService.ChatStreamingAsync(request.Message, request.ProjectId, request.Mode, request.History))
        {
            await Response.WriteAsync(token);
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("export/{projectId:guid}")]
    public async Task<IActionResult> Export(Guid projectId, [FromQuery] string format = "excel")
    {
        var project = await _aiService.GetProjectWithTasksAsync(projectId);
        if (project == null) return NotFound();

        var exportService = HttpContext.RequestServices.GetRequiredService<IAiExportService>();
        var bytes = string.Equals(format, "word", StringComparison.OrdinalIgnoreCase)
            ? await exportService.ExportProjectToWordAsync(project)
            : await exportService.ExportProjectToExcelAsync(project);

        var fileName = $"{project.Name}_{DateTime.Now:yyyyMMdd}.{(string.Equals(format, "word", StringComparison.OrdinalIgnoreCase) ? "docx" : "xlsx")}";
        var contentType = string.Equals(format, "word", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return File(bytes, contentType, fileName);
    }

    private async Task<IActionResult> EnqueueFunctionAsync(
        string jobType,
        string schemaId,
        Guid? projectId,
        string? defaultSourceType,
        Guid? defaultSourceEntityId,
        AiFunctionJobRequest request,
        CancellationToken ct)
    {
        IReadOnlyList<AiJobSourceInputDto>? sources = request.Sources;
        if (defaultSourceEntityId.HasValue && !string.IsNullOrWhiteSpace(defaultSourceType))
        {
            var routeSource = new AiJobSourceInputDto(
                defaultSourceType,
                defaultSourceEntityId,
                null,
                request.SourceVersion,
                request.SourceHash);
            sources = new[] { routeSource }
                .Concat(request.Sources?.Where(source =>
                    source.SourceEntityId != defaultSourceEntityId ||
                    !string.Equals(source.SourceType, defaultSourceType, StringComparison.OrdinalIgnoreCase)) ?? [])
                .ToList();
        }

        var sourceType = string.IsNullOrWhiteSpace(defaultSourceType)
            ? request.SourceType ?? (sources is { Count: > 0 } ? sources[0].SourceType : string.Empty)
            : defaultSourceType;
        var sourceEntityId = defaultSourceEntityId ?? request.SourceEntityId;
        var sourceId = sourceEntityId?.ToString() ?? request.LegacySourceKey;
        var dto = new CreateAiJobDto(
            jobType,
            projectId,
            sourceType,
            sourceId,
            request.ProviderHint,
            request.Sensitive,
            request.SourceText,
            sources,
            schemaId,
            "4.0",
            request.SourceVersion,
            request.SourceHash,
            request.ConsentId,
            request.RetentionPolicyId,
            request.MaximumEstimatedCostUsd,
            request.CacheMode,
            request.Language,
            request.Options);
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateJobAsync(dto, idempotencyKey, requestId, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record AiPriorityRequest(string Title, string? Description, string? ProjectContext);

public sealed record AiSubtasksRequest(string Title, string? Description);

public sealed record AiChatRequest(string Message, Guid? ProjectId, string Mode = "erumi", IList<AiChatMessageDto>? History = null);
