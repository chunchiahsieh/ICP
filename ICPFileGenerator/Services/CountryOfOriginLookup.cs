using ICPFileGenerator.Infrastructure.Database;
using Microsoft.Data.SqlClient;

namespace ICPFileGenerator.Services;

public interface ICountryOfOriginLookup
{
    Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class CountryOfOriginLookup : ICountryOfOriginLookup
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CountryOfOriginLookup(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Key1, Value1
            FROM dbo.SystemConfigs
            WHERE Category = N'Country of Origin'
              AND IsDeleted = 0
              AND Key1 IS NOT NULL
              AND LTRIM(RTRIM(Key1)) <> N'';
            """;

        var countries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0).Trim();
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
            {
                countries.TryAdd(code, name);
            }
        }

        return countries;
    }
}
