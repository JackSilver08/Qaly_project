using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.User;

namespace Qaly.Application.Services;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserDto>>> GetActiveAsync(CancellationToken ct = default);
    Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
}
