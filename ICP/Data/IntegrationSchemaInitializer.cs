using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ICP.Data;

public static class IntegrationSchemaInitializer
{
    private const string EnsureOutboxTableSql = """
        IF OBJECT_ID(N'dbo.INTEGRATION_EVENT_OUTBOX', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.INTEGRATION_EVENT_OUTBOX (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_INTEGRATION_EVENT_OUTBOX PRIMARY KEY,
                EventType NVARCHAR(100) NOT NULL,
                CaseType NVARCHAR(20) NOT NULL,
                HeaderKey NVARCHAR(200) NOT NULL,
                CaseNo NVARCHAR(50) NOT NULL,
                PayloadJson NVARCHAR(MAX) NOT NULL,
                Status NVARCHAR(20) NOT NULL,
                RetryCount INT NOT NULL CONSTRAINT DF_INTEGRATION_EVENT_OUTBOX_RetryCount DEFAULT (0),
                LastError NVARCHAR(MAX) NULL,
                PublishedAt DATETIME2 NULL,
                CreateTime DATETIME2 NOT NULL CONSTRAINT DF_INTEGRATION_EVENT_OUTBOX_CreateTime DEFAULT (SYSUTCDATETIME()),
                CreateUser NVARCHAR(100) NULL,
                UpdateTime DATETIME2 NULL,
                UpdateUser NVARCHAR(100) NULL
            );

            CREATE INDEX IX_INTEGRATION_EVENT_OUTBOX_Status_CreateTime
                ON dbo.INTEGRATION_EVENT_OUTBOX (Status, CreateTime);
        END
        """;

    private const string EnsureExportRequestTableSql = """
        IF OBJECT_ID(N'dbo.EXPORT_REQUEST', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.EXPORT_REQUEST (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EXPORT_REQUEST PRIMARY KEY,
                FileName NVARCHAR(260) NOT NULL,
                StoredPath NVARCHAR(1024) NOT NULL,
                Status NVARCHAR(32) NOT NULL,
                ErrorMessage NVARCHAR(2000) NULL,
                CreateTime DATETIME2 NOT NULL CONSTRAINT DF_EXPORT_REQUEST_CreateTime DEFAULT (SYSUTCDATETIME()),
                UpdateTime DATETIME2 NULL
            );

            CREATE INDEX IX_EXPORT_REQUEST_Status
                ON dbo.EXPORT_REQUEST (Status);
        END
        """;

    public static async Task EnsureOutboxTableAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(EnsureOutboxTableSql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to ensure integration outbox schema exists.");
            throw;
        }
    }

    public static async Task EnsureExportRequestTableAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(EnsureExportRequestTableSql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to ensure EXPORT_REQUEST schema exists.");
            throw;
        }
    }
}
