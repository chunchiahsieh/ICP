USE [TEL-ICP];
GO

CREATE OR ALTER PROCEDURE dbo.ClaimNextFileGenerationJob
    @WorkerId NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Id UNIQUEIDENTIFIER;

    BEGIN TRANSACTION;

    SELECT TOP (1) @Id = j.Id
    FROM dbo.ICPFileGeneratorJob AS j WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE j.Status = N'Pending'
    ORDER BY j.CreateTime ASC;

    IF @Id IS NULL
    BEGIN
        COMMIT TRANSACTION;
        RETURN;
    END

    UPDATE dbo.ICPFileGeneratorJob
    SET
        Status = N'Processing',
        WorkerId = @WorkerId,
        StartTime = SYSUTCDATETIME(),
        UpdateTime = SYSUTCDATETIME(),
        ErrorMessage = NULL
    WHERE Id = @Id;

    SELECT
        Id,
        RequestId,
        SourceSystem,
        SourceRecordId,
        FileType,
        InputFilePath,
        OutputFilePath,
        Status,
        WorkerId,
        RetryCount,
        ErrorMessage,
        CreateTime,
        StartTime,
        CompleteTime,
        UpdateTime
    FROM dbo.ICPFileGeneratorJob
    WHERE Id = @Id;

    COMMIT TRANSACTION;
END
GO
