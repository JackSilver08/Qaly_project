using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.ApiKey;

namespace Qaly.Application.Services;

public interface IApiKeyService
{
    Task<Result<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ApiKeyDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result> RevokeAsync(Guid keyId, CancellationToken ct = default);
}
