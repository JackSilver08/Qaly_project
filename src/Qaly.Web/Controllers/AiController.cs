using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
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
    private readonly IAiActionComposerService _aiActionComposerService;
    private readonly IAiJobActivityService _aiJobActivityService;
    private readonly IAiAssistantSessionService _aiAssistantSessionService;
    private readonly IAiSafeTestOrchestratorService _aiSafeTestOrchestratorService;

    public AiController(
        IAiService aiService,
        IErumiChatService erumiChatService,
        IAiWorkflowService aiWorkflowService,
        IAiIngestionService ingestionService,
        IAnalyticsService analyticsService,
        IAgentRunService agentRunService,
        IAiPlatformQueryService aiPlatformQueryService,
        IProjectService projectService,
        ITaskService taskService,
        IAiActionComposerService aiActionComposerService,
        IAiJobActivityService aiJobActivityService,
        IAiAssistantSessionService aiAssistantSessionService,
        IAiSafeTestOrchestratorService aiSafeTestOrchestratorService)
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
        _aiActionComposerService = aiActionComposerService;
        _aiJobActivityService = aiJobActivityService;
        _aiAssistantSessionService = aiAssistantSessionService;
        _aiSafeTestOrchestratorService = aiSafeTestOrchestratorService;
    }

    [HttpPost("generate-plan")]
    public IActionResult GeneratePlan(GeneratePlanRequestDto request)
        => StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = "legacy_ai_planner_retired",
            error = "The legacy planner was retired. Open the unified Assistant and use the governed Project Launch capability."
        });

    [HttpPost("create-plan")]
    [ValidateAntiForgeryToken]
    public IActionResult CreatePlan(ConfirmPlanRequestDto request)
        => StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = "legacy_ai_planner_mutation_retired",
            error = "Direct planner mutation is disabled. Use the unified Assistant review, single-confirm and canonical read-back flow."
        });

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

    [HttpPost("actions/compose")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ComposeAction(
        AiActionComposeRequestDto dto,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { error = "Idempotency-Key is required.", errorCode = AiErrorCodes.InvalidRequest });
        }

        var result = await _aiActionComposerService.ComposeAsync(
            dto,
            idempotencyKey,
            Request.Headers["X-Request-Id"].FirstOrDefault(),
            ct);
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

    [HttpGet("jobs/{jobId:guid}/activity")]
    public async Task<IActionResult> GetJobActivity(
        Guid jobId,
        [FromQuery] int afterSequence = 0,
        CancellationToken ct = default)
    {
        var result = await _aiJobActivityService.GetAsync(jobId, afterSequence, ct);
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
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new
            {
                errorCode = Qaly.Application.Common.Models.AiErrorCodes.InvalidRequest,
                error = "Idempotency-Key is required for draft confirmation."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.RowVersion))
        {
            return BadRequest(new
            {
                errorCode = Qaly.Application.Common.Models.AiErrorCodes.InvalidRequest,
                error = "rowVersion is required. Reload the canonical draft before confirmation."
            });
        }

        dto = dto with { IdempotencyKey = idempotencyKey };
        var result = await _aiWorkflowService.ConfirmDraftAsync(draftId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("agent-runs")]
    public IActionResult StartAgentRun(StartAgentRunDto request)
        => StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = "legacy_agent_run_retired",
            error = "Legacy autonomous runs are read-only history. Start work through a registered unified Assistant capability."
        });

    [HttpGet("agent-runs/{runId:guid}")]
    public async Task<IActionResult> GetAgentRun(Guid runId, CancellationToken ct = default)
    {
        var result = await _agentRunService.GetAsync(runId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("agent-runs/{runId:guid}/approve")]
    public IActionResult ApproveAgentRun(Guid runId, ApproveAgentRunDto request)
        => StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = "legacy_agent_run_mutation_retired",
            error = "Legacy approval cannot mutate data. Review and confirm the registered native draft in the unified Assistant."
        });

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
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? projectId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.GetUsageAsync(organizationId, projectId, from, to, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("budget/scopes")]
    public async Task<IActionResult> GetBudgetScopes(CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.GetBudgetScopesAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("budget")]
    public async Task<IActionResult> GetBudget(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? projectId,
        CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.GetBudgetAsync(organizationId, projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("budget")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBudget(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? projectId,
        UpdateAiBudgetPolicyDto dto,
        CancellationToken ct = default)
    {
        var result = await _aiPlatformQueryService.UpdateBudgetAsync(organizationId, projectId, dto, ct);
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
    public IActionResult EnqueueMeetingActionExtraction(Guid meetingId)
    {
        _ = meetingId;
        Response.Headers["Deprecation"] = "true";
        return StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = AiErrorCodes.InvalidRequest,
            error = "The orphan generic meeting extraction route has been retired. Use the privacy-gated /api/meetings/{meetingId}/auto-checknote flow."
        });
    }

    [HttpPost("groups/{groupId:guid}/summaries")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnqueueGroupSummary(
        Guid groupId,
        GroupSummaryRequestDto request,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateGroupSelectedSummaryAsync(
            groupId,
            request,
            idempotencyKey,
            requestId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("dashboard/strategic-brief")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnqueueDashboardStrategicBrief(
        DashboardStrategicBriefRequestDto request,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateDashboardStrategicBriefAsync(
            request,
            idempotencyKey,
            requestId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

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

    [HttpPost("groups/{groupId:guid}/task-drafts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnqueueSourceLinkedTaskDraft(
        Guid groupId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateSourceLinkedTaskDraftAsync(
            groupId,
            request,
            idempotencyKey,
            requestId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

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

    [HttpPost("tasks/{taskId:guid}/skill-suggestions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnqueueTaskSkillSuggestion(
        Guid taskId,
        TaskSkillSuggestionRequestDto request,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateTaskSkillSuggestionAsync(
            taskId,
            request,
            idempotencyKey,
            requestId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("tasks/{taskId:guid}/breakdown")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueTaskBreakdown(
        Guid taskId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = "legacy_task_breakdown_retired",
            error = "Task breakdown is now a governed native draft. Open the unified Assistant, review the breakdown, then confirm it once."
        }));

    [HttpPost("tasks/{taskId:guid}/acceptance-checklist")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EnqueueAcceptanceChecklist(
        Guid taskId,
        AiFunctionJobRequest request,
        CancellationToken ct = default)
        => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = "legacy_acceptance_checklist_retired",
            error = "Acceptance checklist is now a governed native draft. Open the unified Assistant, review the checklist, then confirm it once."
        }));

    [HttpPost("projects/{projectId:guid}/progress-summary")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnqueueProjectProgressSummary(
        Guid projectId,
        ProjectProgressSummaryRequestDto request,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiWorkflowService.CreateProjectProgressSummaryAsync(
            projectId,
            request,
            idempotencyKey,
            requestId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

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
        ProjectProgressSummaryRequestDto request,
        CancellationToken ct = default)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        return EnqueueSprintProgressSummaryCoreAsync(
            projectId,
            sprintId,
            request,
            idempotencyKey,
            requestId,
            ct);
    }

    private async Task<IActionResult> EnqueueSprintProgressSummaryCoreAsync(
        Guid projectId,
        Guid sprintId,
        ProjectProgressSummaryRequestDto request,
        string idempotencyKey,
        string? requestId,
        CancellationToken ct)
    {
        var result = await _aiWorkflowService.CreateSprintProgressSummaryAsync(
            projectId,
            sprintId,
            request,
            idempotencyKey,
            requestId,
            ct);
        return StatusCode(result.StatusCode, result);
    }

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
    public async Task<IActionResult> ProjectSummary(Guid projectId, CancellationToken ct)
    {
        var access = await _projectService.GetByIdAsync(projectId, ct);
        if (!access.IsSuccess) return StatusCode(access.StatusCode, access);
        return Ok(new { summary = await _aiService.GenerateProjectSummaryAsync(projectId) });
    }

    [HttpGet("projects/{projectId:guid}/risks")]
    public async Task<IActionResult> ProjectRisks(Guid projectId, CancellationToken ct)
    {
        var access = await _projectService.GetByIdAsync(projectId, ct);
        if (!access.IsSuccess) return StatusCode(access.StatusCode, access);
        return Ok(new { risks = await _aiService.AnalyzeProjectRisksAsync(projectId) });
    }

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
    public async Task<IActionResult> SuggestAssignment(Guid taskId, [FromQuery] Guid projectId, CancellationToken ct)
    {
        var task = await _taskService.GetByIdAsync(taskId, ct);
        if (!task.IsSuccess) return StatusCode(task.StatusCode, task);
        if (task.Data?.ProjectId != projectId) return NotFound();
        return Ok(new { suggestion = await _aiService.SuggestTaskAssignmentAsync(taskId, projectId) });
    }

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

    [HttpPost("assistant/sessions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssistantSession(
        CreateAiAssistantSessionRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.CreateAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(result.StatusCode, result.Data)
            : StatusCode(result.StatusCode, result);
    }

    [HttpGet("assistant/sessions/recent")]
    public async Task<IActionResult> GetRecentAssistantSession(CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.GetRecentAsync(ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("assistant/sessions")]
    public async Task<IActionResult> ListAssistantSessions(
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var result = await _aiAssistantSessionService.ListAsync(includeArchived, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("assistant/capabilities/coverage")]
    public IActionResult GetAiNativeCoverageMatrix()
        => Ok(AiNativeModuleCoverageCatalog.Matrix());

    [HttpGet("assistant/quality/metrics")]
    public async Task<IActionResult> GetAssistantQualityMetrics(CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.GetQualityMetricsAsync(ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("assistant/sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetAssistantSession(Guid sessionId, CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.GetAsync(sessionId, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPatch("assistant/sessions/{sessionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameAssistantSession(
        Guid sessionId,
        UpdateAiAssistantSessionRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.RenameAsync(sessionId, request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPut("assistant/sessions/{sessionId:guid}/scope")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssistantSessionScope(
        Guid sessionId,
        UpdateAiAssistantSessionScopeRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.UpdateScopeAsync(sessionId, request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("assistant/sessions/{sessionId:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveAssistantSession(
        Guid sessionId,
        AiAssistantSessionControlRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.ArchiveAsync(sessionId, request.ExpectedVersion, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpDelete("assistant/sessions/{sessionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssistantSession(
        Guid sessionId,
        [FromQuery] long expectedVersion,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.DeleteAsync(sessionId, expectedVersion, ct);
        return result.IsSuccess ? NoContent() : StatusCode(result.StatusCode, result);
    }

    [HttpPut("assistant/sessions/{sessionId:guid}/clarification-draft")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAssistantClarificationDraft(
        Guid sessionId,
        UpdateAiAssistantClarificationDraftRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.SaveClarificationDraftAsync(sessionId, request, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpDelete("assistant/sessions/{sessionId:guid}/clarification-draft")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAssistantClarificationDraft(
        Guid sessionId,
        [FromQuery] long expectedVersion,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.ClearClarificationDraftAsync(sessionId, expectedVersion, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("assistant/turns")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssistantTurn(AiAssistantTurnRequestDto request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString().Trim();
        var correlationId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiAssistantSessionService.AppendTurnAsync(
            request,
            idempotencyKey,
            correlationId,
            ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("assistant/sessions/{sessionId:guid}/stream")]
    public async Task StreamAssistantTurn(
        Guid sessionId,
        [FromQuery] Guid clientTurnId,
        CancellationToken ct)
    {
        var initial = await _aiAssistantSessionService.GetAsync(sessionId, ct);
        if (!initial.IsSuccess || initial.Data == null)
        {
            Response.StatusCode = initial.StatusCode;
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Append("X-Accel-Buffering", "no");
        var sentEventCount = 0;
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        while (!ct.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
        {
            var current = await _aiAssistantSessionService.GetAsync(sessionId, ct);
            if (!current.IsSuccess || current.Data == null) break;
            var turn = current.Data.Turns.FirstOrDefault(item => item.ClientTurnId == clientTurnId);
            if (turn != null)
            {
                var pendingEvents = turn.ProcessEvents.Skip(sentEventCount).ToArray();
                foreach (var processEvent in pendingEvents)
                    await WriteSseAsync("progress", processEvent, ct);
                sentEventCount += pendingEvents.Length;

                if (turn.Status is "completed" or "failed" or "canceled")
                {
                    var answer = turn.Response?.Conversation?.Answer ?? turn.Response?.AssistantMessage;
                    if (!string.IsNullOrWhiteSpace(answer))
                    {
                        foreach (var delta in Chunk(answer, 36))
                        {
                            await WriteSseAsync("answer_delta", new { delta }, ct);
                            await Task.Delay(12, ct);
                        }
                    }
                    await WriteSseAsync("done", new { turn.Status, current.Data.Version }, ct);
                    return;
                }
            }
            else
            {
                await Response.WriteAsync(": waiting\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            await Task.Delay(350, ct);
        }
    }

    [HttpPost("assistant/test-runs/{runId:guid}/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmSafeTestRun(
        Guid runId,
        ConfirmAiSafeTestRunRequestDto request,
        CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString().Trim();
        var result = await _aiSafeTestOrchestratorService.ConfirmAsync(
            runId, request.ExpectedRevision, idempotencyKey, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpGet("assistant/test-runs/{runId:guid}")]
    public async Task<IActionResult> GetSafeTestRun(Guid runId, CancellationToken ct)
    {
        var result = await _aiSafeTestOrchestratorService.GetAsync(runId, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("assistant/turns/{turnId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAssistantTurn(
        Guid turnId,
        AiAssistantTurnControlRequestDto request,
        CancellationToken ct)
    {
        var result = await _aiAssistantSessionService.CancelTurnAsync(turnId, request.ExpectedVersion, ct);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result);
    }

    [HttpPost("assistant/turns/{turnId:guid}/resume")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeAssistantTurn(
        Guid turnId,
        AiAssistantTurnControlRequestDto request,
        CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key is required.", errorCode = AiErrorCodes.InvalidRequest });
        var correlationId = Request.Headers["X-Request-Id"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _aiAssistantSessionService.ResumeTurnAsync(
            turnId, request, idempotencyKey, correlationId, ct);
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

    private async Task WriteSseAsync(string eventName, object payload, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {eventName}\n", ct);
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (var index = 0; index < value.Length; index += size)
            yield return value.Substring(index, Math.Min(size, value.Length - index));
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
