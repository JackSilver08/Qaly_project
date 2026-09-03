namespace Qaly.Application.DTOs.User;

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? AvatarUrl,
    DateTimeOffset CreatedAt);

/// <summary>
/// A collaborator-directory entry. System access role and account creation metadata
/// are intentionally omitted: membership pickers do not need platform-administration
/// data, and exposing it would widen the tenant boundary.
/// </summary>
public record UserDirectoryDto(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    string? AvatarUrl,
    string? SystemRole = null);

public record RegisterDto(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword);

public record LoginDto(
    string Email,
    string Password);

public record UpdateProfileDto(
    string FullName,
    string? AvatarUrl);
