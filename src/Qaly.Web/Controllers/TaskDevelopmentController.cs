using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Services.GitHub;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/tasks/{taskId:guid}/development")]
public sealed class TaskDevelopmentController : BaseApiController
{
    private readonly ITaskDevelopmentService _service;
    public TaskDevelopmentController(ITaskDevelopmentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(Guid taskId, CancellationToken ct)
    {
        var result = await _service.GetAsync(taskId, ct);
        return StatusCode(result.StatusCode, result);
    }
}
