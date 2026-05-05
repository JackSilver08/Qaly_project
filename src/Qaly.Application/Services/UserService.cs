using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.User;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepo;

    public UserService(IRepository<User> userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<Result<IReadOnlyList<UserDto>>> GetActiveAsync(CancellationToken ct = default)
    {
        var users = await _userRepo.GetQueryable()
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<UserDto>>(users.Select(user => user.ToDto()).ToList());
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct);
        return user == null ? Result.NotFound<UserDto>() : Result.Success(user.ToDto());
    }
}
