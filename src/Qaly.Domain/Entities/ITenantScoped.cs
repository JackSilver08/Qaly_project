namespace Qaly.Domain.Entities;

/// <summary>
/// Đánh dấu entity thuộc về một tenant (Organization). Mọi truy vấn tới các
/// entity này bắt buộc phải scope theo <see cref="OrganizationId"/> ở tầng
/// data-access/service để đảm bảo cách ly tenant.
/// </summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
