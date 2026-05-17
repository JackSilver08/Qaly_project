using System.Security.Cryptography;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.User;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class AuthService : IAuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 10000;

    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ISessionService _sessionService;

    public AuthService(
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ISessionService sessionService)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _sessionService = sessionService;
    }

    public async Task<Result<UserDto>> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var email = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return Result.Failure<UserDto>("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<UserDto>("Email is required.");
        }

        if (dto.Password.Length < 8)
        {
            return Result.Failure<UserDto>("Password must be at least 8 characters.");
        }

        if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
        {
            return Result.Failure<UserDto>("Password confirmation does not match.");
        }

        var exists = (await _userRepo.FindAsync(user => user.Email == email, ct)).Any();

        if (exists)
        {
            return Result.Failure<UserDto>("Email is already registered.", 409);
        }

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = HashPassword(dto.Password),
            Role = "Member",
            IsActive = true
        };

        await _userRepo.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await TryLogAuditAsync("Create", nameof(User), user.Id.ToString(), new { user.Email, user.Role }, ct);

        return Result.Created(user.ToDto());
    }

    public async Task<Result<UserDto>> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var email = NormalizeEmail(dto.Email);
        var matchingUsers = await _userRepo.FindAsync(item => item.Email == email, ct);
        var user = matchingUsers.Count > 0 ? matchingUsers[0] : null;

        if (user == null || !user.IsActive || !VerifyPassword(dto.Password, user.PasswordHash))
        {
            return Result.Failure<UserDto>("Invalid email or password.", 401);
        }

        await TryLogAuditAsync("Login", nameof(User), user.Id.ToString(), new { user.Email }, ct);

        return Result.Success(user.ToDto());
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        return user == null ? Result.NotFound<UserDto>() : Result.Success(user.ToDto());
    }

    public async Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null)
        {
            return Result.NotFound<UserDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return Result.Failure<UserDto>("Full name is required.");
        }

        user.FullName = dto.FullName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl) ? null : dto.AvatarUrl.Trim();

        await _userRepo.UpdateAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await TryLogAuditAsync("Update", nameof(User), user.Id.ToString(), new { user.FullName, user.AvatarUrl }, ct);

        return Result.Success(user.ToDto());
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null)
        {
            return Result.NotFound();
        }

        if (!VerifyPassword(dto.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure("Current password is incorrect.", 400);
        }

        if (dto.NewPassword.Length < 8)
        {
            return Result.Failure("New password must be at least 8 characters.", 400);
        }

        if (!string.Equals(dto.NewPassword, dto.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return Result.Failure("Password confirmation does not match.", 400);
        }

        user.PasswordHash = HashPassword(dto.NewPassword);
        await _userRepo.UpdateAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await TryLogAuditAsync("ChangePassword", nameof(User), user.Id.ToString(), null, ct);

        // Security: Revoke all other sessions after password change
        await RevokeSessionsAsync(userId, ct);

        return Result.Success();
    }

    public async Task<Result> RevokeSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        await _sessionService.RevokeAllUserSessionsAsync(userId, ct);
        await TryLogAuditAsync("RevokeSessions", nameof(User), userId.ToString(), null, ct);
        return Result.Success();
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task TryLogAuditAsync(string action, string entityType, string entityId, object? changes, CancellationToken ct)
    {
        try
        {
            await _auditLogService.LogAsync(action, entityType, entityId, changes, ct);
        }
        catch
        {
            // Audit logging must never block authentication or profile updates.
        }
    }
}
