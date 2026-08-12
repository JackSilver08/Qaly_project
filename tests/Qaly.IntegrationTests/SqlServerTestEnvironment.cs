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
        try
        {
            var builder = string.IsNullOrWhiteSpace(configured)
                ? new SqlConnectionStringBuilder
                {
                    DataSource = "localhost",
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    Encrypt = false
                }
                : new SqlConnectionStringBuilder(configured);

            builder.InitialCatalog = "master";
            builder.ConnectTimeout = 2;

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch (Exception ex) when (!string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection was supplied for SQL Server integration tests, but the database is not reachable.",
                ex);
        }
        catch
        {
            return false;
        }
    }
}
