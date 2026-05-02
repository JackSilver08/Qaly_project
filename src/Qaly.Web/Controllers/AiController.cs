using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
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
        => Ok(new { reply = await _aiService.ChatAsync(request.Message, request.ProjectId) });
}

public sealed record AiPriorityRequest(string Title, string? Description, string? ProjectContext);

public sealed record AiSubtasksRequest(string Title, string? Description);

public sealed record AiChatRequest(string Message, Guid? ProjectId);
