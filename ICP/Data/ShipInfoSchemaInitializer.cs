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

    private const string EnsureDepositColumnLengthSql = """
        IF COL_LENGTH('dbo.ICP_HEADER', 'DEPOSIT') IS NOT NULL
           AND COL_LENGTH('dbo.ICP_HEADER', 'DEPOSIT') < 30
        BEGIN
            ALTER TABLE dbo.ICP_HEADER ALTER COLUMN DEPOSIT NVARCHAR(30) NULL;
        END
        """;

    private const string EnsureAttachmentsTableSql = """
        IF OBJECT_ID(N'dbo.Attachments', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Attachments (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Attachments PRIMARY KEY DEFAULT NEWID(),
                AttachmentType NVARCHAR(50) NOT NULL,
                AttachmentOwnerId NVARCHAR(100) NOT NULL,
                OriginalFileName NVARCHAR(255) NOT NULL,
                StoredFileName NVARCHAR(255) NOT NULL,
                RelativePath NVARCHAR(500) NOT NULL,
                FileSize BIGINT NOT NULL,
                ContentType NVARCHAR(100) NULL,
                CreateTime DATETIME2(7) NOT NULL CONSTRAINT DF_Attachments_CreateTime DEFAULT GETDATE(),
                CreateUser NVARCHAR(100) NULL,
                UpdateTime DATETIME2(7) NULL,
                UpdateUser NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL CONSTRAINT DF_Attachments_IsDeleted DEFAULT 0
            );
            CREATE NONCLUSTERED INDEX IX_Attachments_Active_Owner
                ON dbo.Attachments(AttachmentType, AttachmentOwnerId)
                WHERE IsDeleted = 0;
        END
        """;

    private const string EnsureCaseStatusColumnsSql = """
        IF COL_LENGTH('dbo.ICP_HEADER', 'DEPOSIT_CASE_STATUS') IS NULL
        BEGIN
            ALTER TABLE dbo.ICP_HEADER
                ADD DEPOSIT_CASE_STATUS NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_ICP_HEADER_DEPOSIT_CASE_STATUS DEFAULT (N'NotInitiated');
        END

        IF COL_LENGTH('dbo.ICP_HEADER', 'ARUR_CASE_STATUS') IS NULL
        BEGIN
            ALTER TABLE dbo.ICP_HEADER
                ADD ARUR_CASE_STATUS NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_ICP_HEADER_ARUR_CASE_STATUS DEFAULT (N'NotInitiated');
        END

        IF COL_LENGTH('dbo.ICP_DETAIL', 'DEPOSIT_CASE_STATUS') IS NULL
        BEGIN
            ALTER TABLE dbo.ICP_DETAIL
                ADD DEPOSIT_CASE_STATUS NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_ICP_DETAIL_DEPOSIT_CASE_STATUS DEFAULT (N'NotInitiated');
        END

        IF COL_LENGTH('dbo.ICP_DETAIL', 'ARUR_CASE_STATUS') IS NULL
        BEGIN
            ALTER TABLE dbo.ICP_DETAIL
                ADD ARUR_CASE_STATUS NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_ICP_DETAIL_ARUR_CASE_STATUS DEFAULT (N'NotInitiated');
        END
        """;

    private const string MigrateCaseStatusDataSql = """
        IF COL_LENGTH('dbo.ICP_HEADER', 'DEPOSIT_CASE_STATUS') IS NOT NULL
        BEGIN
            UPDATE dbo.ICP_HEADER
            SET DEPOSIT_CASE_STATUS = N'Initiated'
            WHERE NULLIF(LTRIM(RTRIM(DEPOSIT)), N'') IS NOT NULL
              AND DEPOSIT_CASE_STATUS = N'NotInitiated';

            UPDATE dbo.ICP_HEADER
            SET ARUR_CASE_STATUS = N'Initiated'
            WHERE NULLIF(LTRIM(RTRIM(RT_NO)), N'') IS NOT NULL
              AND ARUR_CASE_STATUS = N'NotInitiated';

            UPDATE d
            SET d.DEPOSIT_CASE_STATUS = h.DEPOSIT_CASE_STATUS
            FROM dbo.ICP_DETAIL d
            INNER JOIN dbo.ICP_HEADER h ON d.INVOICE_NO = h.INVOICE_NO AND d.TET_PO = h.TET_PO
            WHERE h.DEPOSIT_CASE_STATUS = N'Initiated'
              AND d.DEPOSIT_CASE_STATUS = N'NotInitiated';

            UPDATE d
            SET d.ARUR_CASE_STATUS = h.ARUR_CASE_STATUS
            FROM dbo.ICP_DETAIL d
            INNER JOIN dbo.ICP_HEADER h ON d.INVOICE_NO = h.INVOICE_NO AND d.TET_PO = h.TET_PO
            WHERE h.ARUR_CASE_STATUS = N'Initiated'
              AND d.ARUR_CASE_STATUS = N'NotInitiated';
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
            await db.Database.ExecuteSqlRawAsync(EnsureAttachmentsTableSql, cancellationToken);
            await db.Database.ExecuteSqlRawAsync(EnsureDepositColumnLengthSql, cancellationToken);
            await db.Database.ExecuteSqlRawAsync(EnsureCaseStatusColumnsSql, cancellationToken);
            await db.Database.ExecuteSqlRawAsync(MigrateCaseStatusDataSql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to ensure ShipInfo schema objects exist.");
            throw;
        }
    }
}
