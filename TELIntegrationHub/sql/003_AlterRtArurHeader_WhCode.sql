USE [ILC];
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.RT_ARUR_HEADER')
      AND name = N'WHCode'
)
BEGIN
    THROW 50000, 'dbo.RT_ARUR_HEADER.WHCode does not exist.', 1;
END
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.RT_ARUR_HEADER')
      AND name = N'WHCode'
      AND max_length < 8
)
BEGIN
    ALTER TABLE dbo.RT_ARUR_HEADER
    ALTER COLUMN WHCode nvarchar(4) NULL;
END
GO
