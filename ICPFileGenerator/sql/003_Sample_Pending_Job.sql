-- Optional sample Pending job for local Worker testing
-- Default (host/TEL): USE [ICP]
-- Local AGA only:     USE [TEL-ICP] if your local DB name differs
USE [ICP];
GO

INSERT INTO dbo.ICPFileGeneratorJob
(
    Id,
    RequestId,
    SourceSystem,
    SourceRecordId,
    FileType,
    Status,
    RetryCount,
    CreateTime
)
VALUES
(
    NEWID(),
    NEWID(),
    N'ICP',
    N'demo-001',
    N'TXT',
    N'Pending',
    0,
    SYSUTCDATETIME()
);
GO
