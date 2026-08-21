-- Create table ICPFileGeneratorJob in the ICP database (shared with ICP app).
-- Local (AGA): USE [TEL-ICP]
-- TEL:        USE [ICP]
USE [TEL-ICP];
GO

IF OBJECT_ID(N'dbo.ICPFileGeneratorJob', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ICPFileGeneratorJob
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ICPFileGeneratorJob PRIMARY KEY,
        RequestId UNIQUEIDENTIFIER NOT NULL,
        SourceSystem NVARCHAR(64) NOT NULL,
        SourceRecordId NVARCHAR(128) NOT NULL,
        FileType NVARCHAR(32) NOT NULL,
        InputFilePath NVARCHAR(1024) NULL,
        OutputFilePath NVARCHAR(1024) NULL,
        Status NVARCHAR(32) NOT NULL,
        WorkerId NVARCHAR(64) NULL,
        RetryCount INT NOT NULL CONSTRAINT DF_ICPFileGeneratorJob_RetryCount DEFAULT (0),
        ErrorMessage NVARCHAR(4000) NULL,
        CreateTime DATETIME2 NOT NULL,
        StartTime DATETIME2 NULL,
        CompleteTime DATETIME2 NULL,
        UpdateTime DATETIME2 NULL,
        CONSTRAINT UQ_ICPFileGeneratorJob_RequestId UNIQUE (RequestId)
    );

    CREATE INDEX IX_ICPFileGeneratorJob_Status_CreateTime
        ON dbo.ICPFileGeneratorJob (Status, CreateTime);
END
GO
