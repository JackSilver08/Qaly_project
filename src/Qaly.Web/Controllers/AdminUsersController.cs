using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users")]
public class AdminUsersController : BaseApiController
{
    private readonly QalyDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminUsersController(QalyDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => new AdminUserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.AvatarUrl,
                user.CreatedAt))
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateAdminUserRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Full name and email are required.");
        }

        if (await _context.Users.AnyAsync(user => user.Email == email, ct))
        {
            return Conflict("Email is already registered.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Password is required.");
        }

        var password = request.Password;
        if (password.Length < 8)
        {
            return BadRequest("Password must be at least 8 characters.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = HashPassword(password),
            Role = NormalizeSystemRole(request.Role),
            IsActive = request.IsActive
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminCreateUser", nameof(User), user.Id.ToString(), new { user.Email, user.Role, user.IsActive }, ct);

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new AdminUserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive,
            user.AvatarUrl,
            user.CreatedAt));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateAdminUserRequest request, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync([id], ct);
        if (user == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            user.Role = NormalizeSystemRole(request.Role);
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            user.AvatarUrl = request.AvatarUrl.Trim();
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminUpdateUser", nameof(User), user.Id.ToString(), new { user.FullName, user.Role, user.IsActive, user.AvatarUrl }, ct);
        return Ok(new AdminUserDto(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.AvatarUrl, user.CreatedAt));
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportUsers(ImportUsersRequest request, CancellationToken ct)
    {
        var created = 0;
        foreach (var item in request.Users)
        {
            var email = NormalizeEmail(item.Email);
            if (string.IsNullOrWhiteSpace(item.FullName) ||
                string.IsNullOrWhiteSpace(email) ||
                await _context.Users.AnyAsync(user => user.Email == email, ct))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Password) || item.Password.Length < 8)
            {
                continue;
            }

            _context.Users.Add(new User
            {
                FullName = item.FullName.Trim(),
                Email = email,
                PasswordHash = HashPassword(item.Password),
                Role = NormalizeSystemRole(item.Role),
                IsActive = true
            });
            created++;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AdminImportUsers", nameof(User), "bulk", new { created }, ct);
        return Ok(new { created });
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    private static string NormalizeSystemRole(string? role)
        => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Member";

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}

public sealed record AdminUserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? AvatarUrl,
    DateTimeOffset CreatedAt);

public sealed record CreateAdminUserRequest(
    string FullName,
    string Email,
    string? Password,
    string? Role,
    bool IsActive = true);

public sealed record UpdateAdminUserRequest(
    string? FullName,
    string? Role,
    bool? IsActive,
    string? AvatarUrl);

public sealed record ImportUsersRequest(IReadOnlyList<CreateAdminUserRequest> Users);
