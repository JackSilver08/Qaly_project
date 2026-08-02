using Microsoft.Data.SqlClient;

namespace Qaly.IntegrationTests;

internal static class SqlServerTestEnvironment
{
    private static readonly Lazy<bool> _available = new(IsSqlServerAvailable);

    public static bool IsAvailable()
        => _available.Value;

    private static bool IsSqlServerAvailable()
    {
        try
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = "localhost",
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                Encrypt = false,
                ConnectTimeout = 2
            };

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
