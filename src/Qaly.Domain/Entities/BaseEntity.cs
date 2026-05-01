namespace Qaly.Domain.Entities;

/// <summary>
/// Base entity cho tất cả entities trong hệ thống.
/// Cung cấp Id (GUID), CreatedAt, UpdatedAt.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
