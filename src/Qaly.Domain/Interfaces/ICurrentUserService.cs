namespace Qaly.Domain.Interfaces;

/// <summary>
/// Service lấy thông tin user hiện tại (từ HttpContext).
/// Interface ở Domain, implementation ở Infrastructure.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
