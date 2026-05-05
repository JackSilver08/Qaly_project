using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.User;

namespace Qaly.Application.Services;

public interface IAuthService
{
    Task<Result<UserDto>> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<Result<UserDto>> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default);
}
