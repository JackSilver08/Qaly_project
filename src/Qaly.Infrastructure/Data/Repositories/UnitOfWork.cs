using Qaly.Domain.Interfaces;

namespace Qaly.Infrastructure.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly QalyDbContext _context;

    public UnitOfWork(QalyDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
