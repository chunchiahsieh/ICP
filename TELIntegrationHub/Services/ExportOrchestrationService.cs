using System.Text.Json;
using Microsoft.Data.SqlClient;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Services;

public interface IExportOrchestrationService
{
    Task AcceptExportRequestAsync(ExportRequestDto request, CancellationToken cancellationToken = default);

    Task MarkExportCompletedAsync(
        Guid requestId,
        string? outputFilePath,
        CancellationToken cancellationToken = default);

    Task MarkExportFailedAsync(Guid requestId, string error, CancellationToken cancellationToken = default);
}

public sealed class ExportOrchestrationService : IExportOrchestrationService
{
    private readonly string _icpConnection;
    private readonly string _fileGenConnection;
    private readonly IMessageLogService _messageLog;
    private readonly ILogger<ExportOrchestrationService> _logger;

    public ExportOrchestrationService(
        IConfiguration configuration,
        IMessageLogService messageLog,
        ILogger<ExportOrchestrationService> logger)
    {
        _icpConnection = configuration.GetConnectionString("ICP_Connection")
            ?? throw new InvalidOperationException("ConnectionStrings:ICP_Connection is required.");
        _fileGenConnection = configuration.GetConnectionString("ICPFileGenerator")
            ?? throw new InvalidOperationException("ConnectionStrings:ICPFileGenerator is required.");
        _messageLog = messageLog;
        _logger = logger;
    }

    public async Task AcceptExportRequestAsync(
        ExportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException("requestId is required.");
        }

        await using (var icp = new SqlConnection(_icpConnection))
        {
            await icp.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(
                """
                UPDATE dbo.EXPORT_REQUEST
                SET Status = N'Processing', ErrorMessage = NULL, UpdateTime = SYSUTCDATETIME()
                WHERE Id = @Id;
                """,
                icp);
            cmd.Parameters.AddWithValue("@Id", request.RequestId);
            var updated = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (updated == 0)
            {
                ThrowNotFound(request.RequestId);
            }
        }

        var jobId = Guid.NewGuid();
        await using (var fg = new SqlConnection(_fileGenConnection))
        {
            await fg.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(
                """
                INSERT INTO dbo.ICPFileGeneratorJob
                (
                    Id, RequestId, SourceSystem, SourceRecordId, FileType,
                    InputFilePath, Status, RetryCount, CreateTime
                )
                VALUES
                (
                    @Id, @RequestId, N'ICP', @SourceRecordId, N'ShippingAdvice',
                    @InputFilePath, N'Pending', 0, SYSUTCDATETIME()
                );
                """,
                fg);
            cmd.Parameters.AddWithValue("@Id", jobId);
            cmd.Parameters.AddWithValue("@RequestId", request.RequestId);
            cmd.Parameters.AddWithValue(
                "@SourceRecordId",
                string.IsNullOrWhiteSpace(request.SourceRecordId)
                    ? request.RequestId.ToString("D")
                    : request.SourceRecordId);
            cmd.Parameters.AddWithValue("@InputFilePath", (object?)request.StoredPath ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var payload = JsonSerializer.Serialize(new
        {
            request.RequestId,
            request.FileName,
            request.SourceRecordId,
            request.StoredPath,
            jobId
        });
        var log = await _messageLog.RecordReceivedAsync(
            request.RequestId.ToString("D"),
            "export.request",
            "ICP",
            request.RequestId.ToString("D"),
            payload,
            "Export",
            cancellationToken);
        await _messageLog.MarkSuccessAsync(log.Id, cancellationToken);

        _logger.LogInformation(
            "ExportRequest {RequestId} → Processing; Job {JobId} Pending",
            request.RequestId,
            jobId);
    }

    public async Task MarkExportCompletedAsync(
        Guid requestId,
        string? outputFilePath,
        CancellationToken cancellationToken = default)
    {
        await UpdateExportStatusAsync(
            requestId,
            "Completed",
            error: null,
            outputFilePath,
            cancellationToken);
        var payload = JsonSerializer.Serialize(new { requestId, outputFilePath });
        var log = await _messageLog.RecordReceivedAsync(
            $"{requestId:D}:completed",
            "export.completed",
            "ICPFileGenerator",
            requestId.ToString("D"),
            payload,
            "Export",
            cancellationToken);
        await _messageLog.MarkSuccessAsync(log.Id, cancellationToken);
    }

    public async Task MarkExportFailedAsync(
        Guid requestId,
        string error,
        CancellationToken cancellationToken = default)
    {
        await UpdateExportStatusAsync(requestId, "Failed", error, outputFilePath: null, cancellationToken);
        var log = await _messageLog.RecordReceivedAsync(
            $"{requestId:D}:failed",
            "export.failed",
            "ICPFileGenerator",
            requestId.ToString("D"),
            $"{{\"requestId\":\"{requestId:D}\",\"error\":{JsonSerializer.Serialize(error)}}}",
            "Export",
            cancellationToken);
        await _messageLog.MarkFailedAsync(log.Id, error, cancellationToken);
    }

    private async Task UpdateExportStatusAsync(
        Guid requestId,
        string status,
        string? error,
        string? outputFilePath,
        CancellationToken cancellationToken)
    {
        await using var icp = new SqlConnection(_icpConnection);
        await icp.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            """
            UPDATE dbo.EXPORT_REQUEST
            SET
                Status = @Status,
                ErrorMessage = @ErrorMessage,
                OutputFilePath = CASE
                    WHEN @OutputFilePath IS NULL THEN OutputFilePath
                    ELSE @OutputFilePath
                END,
                UpdateTime = SYSUTCDATETIME()
            WHERE Id = @Id;
            """,
            icp);
        cmd.Parameters.AddWithValue("@Id", requestId);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue(
            "@ErrorMessage",
            string.IsNullOrWhiteSpace(error) ? DBNull.Value : error.Length > 2000 ? error[..2000] : error);
        cmd.Parameters.AddWithValue(
            "@OutputFilePath",
            string.IsNullOrWhiteSpace(outputFilePath)
                ? DBNull.Value
                : outputFilePath.Length > 1024
                    ? outputFilePath[..1024]
                    : outputFilePath);
        var updated = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (updated == 0)
        {
            ThrowNotFound(requestId);
        }

        _logger.LogInformation(
            "ExportRequest {RequestId} → {Status} OutputFilePath={OutputFilePath}",
            requestId,
            status,
            outputFilePath);
    }

    private void ThrowNotFound(Guid requestId)
    {
        var builder = new SqlConnectionStringBuilder(_icpConnection);
        var dataSource = builder.DataSource;
        var catalog = builder.InitialCatalog;

        _logger.LogWarning(
            "EXPORT_REQUEST {RequestId} not found. Hub ICP_Connection DataSource={DataSource} Database={Database}",
            requestId,
            dataSource,
            catalog);

        throw new ExportRequestNotFoundException(requestId, dataSource, catalog);
    }
}
