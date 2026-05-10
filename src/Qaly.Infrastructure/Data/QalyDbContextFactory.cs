using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qaly.Infrastructure.Data;

public class QalyDbContextFactory : IDesignTimeDbContextFactory<QalyDbContext>
{
    public QalyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QalyDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=QalyDb;Trusted_Connection=True;MultipleActiveResultSets=true");

        return new QalyDbContext(optionsBuilder.Options);
    }
}
