using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ICP.Data;

public static class ForwarderSchemaInitializer
{
    private const string EnsureArchiveTableSql = """
        IF OBJECT_ID(N'dbo.ForwarderDataUploadArchive', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.ForwarderDataUploadArchive (
                Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ForwarderDataUploadArchive PRIMARY KEY,
                SourceId BIGINT NOT NULL,
                Type NVARCHAR(20) NOT NULL,
                InvoiceNo NVARCHAR(50) NOT NULL,
                CustomerReference NVARCHAR(100) NULL,
                MaterialCode NVARCHAR(100) NULL,
                OrderMaterialName NVARCHAR(500) NULL,
                Quantity DECIMAL(18,4) NULL,
                PortOfLoading NVARCHAR(100) NULL,
                ShipToName NVARCHAR(300) NULL,
                ShipToAddress NVARCHAR(MAX) NULL,
                ShipToPartyCountryCode NVARCHAR(100) NULL,
                ShipToPortCode NVARCHAR(50) NULL,
                FreightCharge NVARCHAR(100) NULL,
                ConfirmedCustomDate DATETIME NULL,
                Hawb NVARCHAR(50) NULL,
                Mawb NVARCHAR(50) NULL,
                Etd DATETIME NULL,
                Eta DATETIME NULL,
                Flight1 NVARCHAR(50) NULL,
                Flight2 NVARCHAR(50) NULL,
                Cb NVARCHAR(50) NULL,
                Action NVARCHAR(100) NULL,
                Mdp NVARCHAR(50) NULL,
                CreateTime DATETIME NOT NULL,
                CreateUser NVARCHAR(50) NOT NULL,
                UpdateTime DATETIME NULL,
                UpdateUser NVARCHAR(50) NULL,
                FilePath NVARCHAR(500) NOT NULL,
                RemovedTime DATETIME NOT NULL,
                RemovedUser NVARCHAR(50) NOT NULL,
                ReplacedByFilePath NVARCHAR(500) NOT NULL
            );
        END
        """;

    public static async Task EnsureArchiveTableAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(EnsureArchiveTableSql, cancellationToken);
        logger.LogInformation("ForwarderDataUploadArchive table ensured.");
    }
}
