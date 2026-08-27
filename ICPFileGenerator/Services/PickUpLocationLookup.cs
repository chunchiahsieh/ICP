using ICPFileGenerator.Infrastructure.Database;
using ICPFileGenerator.Models;
using Microsoft.Data.SqlClient;

namespace ICPFileGenerator.Services;

public interface IPickUpLocationLookup
{
    /// <summary>
    /// Loads PickUpLocation rows from ICP SystemConfigs (Key1=SLOC).
    /// </summary>
    Task<IReadOnlyDictionary<string, PickUpLocationInfo>> LoadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class PickUpLocationLookup : IPickUpLocationLookup
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<PickUpLocationLookup> _logger;

    public PickUpLocationLookup(
        ISqlConnectionFactory connectionFactory,
        ILogger<PickUpLocationLookup> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, PickUpLocationInfo>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Key1, Value1, Value2, Value3
            FROM dbo.SystemConfigs
            WHERE Category = N'PickUpLocation'
              AND IsDeleted = 0
              AND Key1 IS NOT NULL
              AND LTRIM(RTRIM(Key1)) <> N'';
            """;

        var map = new Dictionary<string, PickUpLocationInfo>(StringComparer.OrdinalIgnoreCase);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var sloc = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            if (string.IsNullOrWhiteSpace(sloc))
            {
                continue;
            }

            var info = new PickUpLocationInfo
            {
                Sloc = sloc,
                Location = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim(),
                ContactPerson = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim(),
                PhoneNo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim()
            };

            // First wins if duplicate SLOC.
            map.TryAdd(sloc, info);
        }

        _logger.LogInformation("Loaded {Count} PickUpLocation SLOC entries.", map.Count);
        return map;
    }
}
