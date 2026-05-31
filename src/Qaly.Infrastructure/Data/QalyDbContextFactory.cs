using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qaly.Infrastructure.Data;

public class QalyDbContextFactory : IDesignTimeDbContextFactory<QalyDbContext>
{
    public QalyDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var currentDirectory = Directory.GetCurrentDirectory();
        var startupProjectDirectory = ResolveStartupProjectDirectory(currentDirectory);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(startupProjectDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=LAPTOP-OTB0GQMG\\SQLEXPRESS;Initial Catalog=QalyDb;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        var optionsBuilder = new DbContextOptionsBuilder<QalyDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new QalyDbContext(optionsBuilder.Options);
    }

    private static string ResolveStartupProjectDirectory(string currentDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(currentDirectory, "src", "Qaly.Web"),
            Path.Combine(currentDirectory, "..", "Qaly.Web"),
            currentDirectory
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "appsettings.json"))
                || File.Exists(Path.Combine(fullPath, "appsettings.Development.json")))
            {
                return fullPath;
            }
        }

        return currentDirectory;
    }
}
