using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IAiWorkflowService _aiWorkflowService;
    private readonly IAiIngestionService _ingestionService;
    private readonly IAnalyticsService _analyticsService;

    public AiController(
        IAiService aiService,
        IAiWorkflowService aiWorkflowService,
        IAiIngestionService ingestionService,
        IAnalyticsService analyticsService)
    {
        _aiService = aiService;
        _aiWorkflowService = aiWorkflowService;
        _ingestionService = ingestionService;
        _analyticsService = analyticsService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        await _ingestionService.SyncAllDataAsync();
        return Ok(new { message = "Data sync to vector database completed." });
    }

    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob(CreateAiJobDto dto, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.CreateJobAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("drafts/{draftId:guid}/confirm")]
    public async Task<IActionResult> ConfirmDraft(Guid draftId, ConfirmAiDraftDto dto, CancellationToken ct = default)
    {
        var result = await _aiWorkflowService.ConfirmDraftAsync(draftId, dto, ct);
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

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] Guid? projectId = null)
        => Ok(new { items = await _aiService.SmartSearchAsync(query, projectId) });

    [HttpPost("subtasks")]
    public async Task<IActionResult> GenerateSubtasks(AiSubtasksRequest request)
        => Ok(new { items = await _aiService.GenerateSubtasksAsync(request.Title, request.Description ?? string.Empty) });

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(AiChatRequest request)
        => Ok(new { reply = await _aiService.ChatAsync(request.Message, request.ProjectId, request.Mode) });

    [HttpPost("chat/stream")]
    public async Task ChatStreaming(AiChatRequest request)
    {
        Response.ContentType = "text/plain";
        await foreach (var token in _aiService.ChatStreamingAsync(request.Message, request.ProjectId, request.Mode))
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
}

public sealed record AiPriorityRequest(string Title, string? Description, string? ProjectContext);

public sealed record AiSubtasksRequest(string Title, string? Description);

public sealed record AiChatRequest(string Message, Guid? ProjectId, string Mode = "erumi");
