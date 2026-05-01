namespace Qaly.Application.DTOs.User;

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? AvatarUrl,
    DateTimeOffset CreatedAt);

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
