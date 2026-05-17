using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qaly.Infrastructure.Data;

public class QalyDbContextFactory : IDesignTimeDbContextFactory<QalyDbContext>
{
    public QalyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QalyDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1434;Database=QalyDb;User Id=sa;Password=Qaly@Dev2026!;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true");
        return new QalyDbContext(optionsBuilder.Options);
    }
}
