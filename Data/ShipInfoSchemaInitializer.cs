using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ICP.Data;

public static class ShipInfoSchemaInitializer
{
    private const string EnsureAuditLogTableSql = """
        IF OBJECT_ID(N'dbo.SHIPINFO_AUDIT_LOG', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SHIPINFO_AUDIT_LOG (
                Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SHIPINFO_AUDIT_LOG PRIMARY KEY,
                EntityType NVARCHAR(20) NOT NULL,
                EntityKey NVARCHAR(200) NOT NULL,
                HeaderKey NVARCHAR(200) NULL,
                Action NVARCHAR(20) NOT NULL,
                FieldName NVARCHAR(100) NULL,
                OldValue NVARCHAR(MAX) NULL,
                NewValue NVARCHAR(MAX) NULL,
                UserName NVARCHAR(100) NOT NULL,
                CaseType NVARCHAR(20) NULL,
                CaseNo NVARCHAR(50) NULL,
                OldStatus NVARCHAR(50) NULL,
                NewStatus NVARCHAR(50) NULL,
                ActionTime DATETIME2 NOT NULL
            );
        END
        """;

    public static async Task EnsureAuditLogTableAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(EnsureAuditLogTableSql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to ensure SHIPINFO_AUDIT_LOG table exists.");
            throw;
        }
    }
}
