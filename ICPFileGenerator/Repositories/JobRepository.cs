using System.Data;
using ICPFileGenerator.Infrastructure.Database;
using ICPFileGenerator.Models;
using Microsoft.Data.SqlClient;

namespace ICPFileGenerator.Repositories;

public sealed class JobRepository : IJobRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public JobRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> ResetTimeoutJobsAsync(
        int processingTimeoutMinutes,
        int maxRetryCount,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            DECLARE @cutoff DATETIME2 = DATEADD(MINUTE, -@TimeoutMinutes, SYSUTCDATETIME());

            UPDATE dbo.ICPFileGeneratorJob
            SET
                Status = CASE
                    WHEN RetryCount + 1 < @MaxRetryCount THEN N'Pending'
                    ELSE N'Failed'
                END,
                RetryCount = RetryCount + 1,
                WorkerId = NULL,
                ErrorMessage = CASE
                    WHEN RetryCount + 1 < @MaxRetryCount THEN N'Processing timeout; requeued.'
                    ELSE N'Processing timeout; max retry exceeded.'
                END,
                UpdateTime = SYSUTCDATETIME(),
                CompleteTime = CASE
                    WHEN RetryCount + 1 < @MaxRetryCount THEN NULL
                    ELSE SYSUTCDATETIME()
                END
            WHERE Status = N'Processing'
              AND StartTime IS NOT NULL
              AND StartTime < @cutoff;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TimeoutMinutes", processingTimeoutMinutes);
        command.Parameters.AddWithValue("@MaxRetryCount", maxRetryCount);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<FileGenerationJob?> ClaimNextAsync(
        string workerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.ClaimNextFileGenerationJob", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@WorkerId", workerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapJob(reader);
    }

    public async Task MarkCompletedAsync(
        Guid jobId,
        string outputFilePath,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.ICPFileGeneratorJob
            SET
                Status = N'Completed',
                OutputFilePath = @OutputFilePath,
                ErrorMessage = NULL,
                CompleteTime = SYSUTCDATETIME(),
                UpdateTime = SYSUTCDATETIME()
            WHERE Id = @Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", jobId);
        command.Parameters.AddWithValue("@OutputFilePath", outputFilePath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid jobId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.ICPFileGeneratorJob
            SET
                Status = N'Failed',
                ErrorMessage = @ErrorMessage,
                CompleteTime = SYSUTCDATETIME(),
                UpdateTime = SYSUTCDATETIME()
            WHERE Id = @Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", jobId);
        command.Parameters.AddWithValue(
            "@ErrorMessage",
            errorMessage.Length > 4000 ? errorMessage[..4000] : errorMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileGenerationJob>> QueryAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                Id, RequestId, SourceSystem, SourceRecordId, FileType,
                InputFilePath, OutputFilePath, Status, WorkerId, RetryCount,
                ErrorMessage, CreateTime, StartTime, CompleteTime, UpdateTime
            FROM dbo.ICPFileGeneratorJob
            """;

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " WHERE Status = @Status";
        }

        sql += " ORDER BY CreateTime DESC";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(status))
        {
            command.Parameters.AddWithValue("@Status", status);
        }

        var jobs = new List<FileGenerationJob>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(MapJob(reader));
        }

        return jobs;
    }

    public async Task<FileGenerationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, RequestId, SourceSystem, SourceRecordId, FileType,
                InputFilePath, OutputFilePath, Status, WorkerId, RetryCount,
                ErrorMessage, CreateTime, StartTime, CompleteTime, UpdateTime
            FROM dbo.ICPFileGeneratorJob
            WHERE Id = @Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapJob(reader);
    }

    public async Task<FileGenerationJob?> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id, RequestId, SourceSystem, SourceRecordId, FileType,
                InputFilePath, OutputFilePath, Status, WorkerId, RetryCount,
                ErrorMessage, CreateTime, StartTime, CompleteTime, UpdateTime
            FROM dbo.ICPFileGeneratorJob
            WHERE RequestId = @RequestId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RequestId", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapJob(reader);
    }

    private static FileGenerationJob MapJob(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RequestId = reader.GetGuid(reader.GetOrdinal("RequestId")),
            SourceSystem = reader.GetString(reader.GetOrdinal("SourceSystem")),
            SourceRecordId = reader.GetString(reader.GetOrdinal("SourceRecordId")),
            FileType = reader.GetString(reader.GetOrdinal("FileType")),
            InputFilePath = reader.IsDBNull(reader.GetOrdinal("InputFilePath"))
                ? null
                : reader.GetString(reader.GetOrdinal("InputFilePath")),
            OutputFilePath = reader.IsDBNull(reader.GetOrdinal("OutputFilePath"))
                ? null
                : reader.GetString(reader.GetOrdinal("OutputFilePath")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            WorkerId = reader.IsDBNull(reader.GetOrdinal("WorkerId"))
                ? null
                : reader.GetString(reader.GetOrdinal("WorkerId")),
            RetryCount = reader.GetInt32(reader.GetOrdinal("RetryCount")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage"))
                ? null
                : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            CreateTime = reader.GetDateTime(reader.GetOrdinal("CreateTime")),
            StartTime = reader.IsDBNull(reader.GetOrdinal("StartTime"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("StartTime")),
            CompleteTime = reader.IsDBNull(reader.GetOrdinal("CompleteTime"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("CompleteTime")),
            UpdateTime = reader.IsDBNull(reader.GetOrdinal("UpdateTime"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdateTime"))
        };
}
