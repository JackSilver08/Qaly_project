namespace Qaly.Domain.Interfaces;

/// <summary>
/// Unit of Work pattern - quản lý transaction across multiple repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
