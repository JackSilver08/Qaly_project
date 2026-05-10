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
    /// BCrypt hash của key — tuyệt đối không lưu plaintext.
    /// </summary>
    [MaxLength(200)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// 8 ký tự đầu để hiển thị trong UI (e.g. "qaly_sk_").
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
