-- Optional sample Pending job for local Worker testing
USE [TEL-ICP];
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
