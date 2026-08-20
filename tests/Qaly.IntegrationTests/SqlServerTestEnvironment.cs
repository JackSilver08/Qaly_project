using Microsoft.Data.SqlClient;

namespace Qaly.IntegrationTests;

internal static class SqlServerTestEnvironment
{
    private static readonly Lazy<bool> _available = new(IsSqlServerAvailable);

    public static bool IsAvailable()
        => _available.Value;

    private static bool IsSqlServerAvailable()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                return CanConnect(new SqlConnectionStringBuilder(configured));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "ConnectionStrings__DefaultConnection was supplied for SQL Server integration tests, but the database is not reachable.",
                    ex);
            }
        }

        foreach (var dataSource in new[] { "localhost", "(localdb)\\MSSQLLocalDB" })
        {
            if (CanConnect(new SqlConnectionStringBuilder
                {
                    DataSource = dataSource,
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    Encrypt = false
                }))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanConnect(SqlConnectionStringBuilder builder)
    {
        try
        {
            builder.InitialCatalog = "master";
            builder.ConnectTimeout = 2;

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
