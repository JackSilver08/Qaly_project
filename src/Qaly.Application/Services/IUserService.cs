using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.User;

namespace Qaly.Application.Services;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserDirectoryDto>>> GetActiveAsync(CancellationToken ct = default);
    Task<Result<UserDirectoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
}
