using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.ApiKey;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/auth/api-keys")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeysController(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    /// <summary>
    /// Tạo API Key mới. Key chỉ hiển thị một lần duy nhất tại thời điểm tạo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyDto dto, CancellationToken ct)
    {
        var result = await _apiKeyService.CreateAsync(dto, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Lấy danh sách API Keys của user hiện tại.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _apiKeyService.GetAllAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Thu hồi (revoke) một API Key.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var result = await _apiKeyService.RevokeAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }
}
