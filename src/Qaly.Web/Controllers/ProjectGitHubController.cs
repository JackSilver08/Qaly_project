using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.GitHub;
using Qaly.Application.Services.GitHub;

namespace Qaly.Web.Controllers;

/// <summary>
/// Ánh xạ repository GitHub với một project. Chỉ đọc/quản lý metadata kết nối;
/// mọi thao tác được scope theo tenant qua service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/github")]
public class ProjectGitHubController : BaseApiController
{
    private readonly IGitHubRepositoryConnectionService _service;

    public ProjectGitHubController(IGitHubRepositoryConnectionService service)
    {
        _service = service;
    }

    [HttpGet("repositories")]
    public async Task<IActionResult> GetRepositories(Guid projectId, CancellationToken ct)
    {
        var result = await _service.GetByProjectAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("repositories")]
    public async Task<IActionResult> AddRepository(
        Guid projectId,
        [FromBody] CreateGitHubRepositoryConnectionDto dto,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(projectId, dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("repositories/{connectionId:guid}")]
    public async Task<IActionResult> RemoveRepository(Guid projectId, Guid connectionId, CancellationToken ct)
    {
        var result = await _service.RemoveAsync(projectId, connectionId, ct);
        return StatusCode(result.StatusCode, result);
    }
}
