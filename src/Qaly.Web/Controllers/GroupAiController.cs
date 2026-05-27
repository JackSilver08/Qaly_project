using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/ai")]
public class GroupAiController : BaseApiController
{
    private readonly IGroupAiService _groupAiService;

    public GroupAiController(IGroupAiService groupAiService)
    {
        _groupAiService = groupAiService;
    }

    [HttpPost("action-items")]
    public async Task<IActionResult> ExtractActionItems(
        Guid groupId,
        GroupAiActionItemsRequest request,
        CancellationToken ct)
    {
        var result = await _groupAiService.ExtractActionItemsAsync(groupId, request, ct);
        return StatusCode(result.StatusCode, result);
    }
}
