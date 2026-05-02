using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Comment;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/comments")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentsController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet("task/{taskItemId:guid}")]
    public async Task<IActionResult> GetByTask(Guid taskItemId, CancellationToken ct)
    {
        var result = await _commentService.GetByTaskAsync(taskItemId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCommentDto dto, CancellationToken ct)
    {
        var result = await _commentService.CreateAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _commentService.DeleteAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }
}
