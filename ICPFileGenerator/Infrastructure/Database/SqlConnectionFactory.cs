using Microsoft.Data.SqlClient;

namespace ICPFileGenerator.Infrastructure.Database;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ICPFileGenerator") ?? string.Empty;
    }

    public SqlConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:ICPFileGenerator is not configured.");
        }

        return new SqlConnection(_connectionString);
    }
}
