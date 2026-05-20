using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Wiki;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/wiki")]
public class WikiController : ControllerBase
{
    private readonly IWikiService _wikiService;

    public WikiController(IWikiService wikiService)
    {
        _wikiService = wikiService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPages(Guid projectId, CancellationToken ct)
    {
        var result = await _wikiService.GetByProjectAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePage(Guid projectId, [FromBody] CreateWikiPageDto dto, CancellationToken ct)
    {
        if (dto == null)
        {
            return BadRequest(new { error = "Wiki request payload is required." });
        }

        var result = await _wikiService.CreateAsync(projectId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePage(Guid projectId, Guid id, [FromBody] UpdateWikiPageDto dto, CancellationToken ct)
    {
        if (dto == null)
        {
            return BadRequest(new { error = "Wiki request payload is required." });
        }

        var result = await _wikiService.UpdateAsync(projectId, id, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePage(Guid projectId, Guid id, CancellationToken ct)
    {
        var result = await _wikiService.DeleteAsync(projectId, id, ct);
        return StatusCode(result.StatusCode, result);
    }
}
