using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qaly.Infrastructure.Data;

public class QalyDbContextFactory : IDesignTimeDbContextFactory<QalyDbContext>
{
    public QalyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QalyDbContext>();
        optionsBuilder.UseSqlServer(
        "Server=CMI\\SQLEXPRESS;Database=QalyDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true");
        return new QalyDbContext(optionsBuilder.Options);
    }
}
