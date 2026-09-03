using System.ComponentModel.DataAnnotations;

namespace Qaly.Domain.Entities;

/// <summary>
/// API Key cho phép tích hợp bên ngoài (CI/CD, scripts) truy cập Qaly API
/// mà không cần đăng nhập qua UI.
/// </summary>
public class ApiKey : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash của secret ngẫu nhiên entropy cao — tuyệt đối không lưu plaintext.
    /// </summary>
    [MaxLength(200)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Prefix không nhạy cảm dùng để nhận diện key trong UI và thu hẹp truy vấn.
    /// </summary>
    [MaxLength(16)]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of scopes: ["tasks:read","tasks:write","projects:read","comments:write"]
    /// </summary>
    [MaxLength(1000)]
    public string Scopes { get; set; } = "[]";

    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
}
