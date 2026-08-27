USE [ILC];
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.RT_ARUR_HEADER')
      AND name = N'CreateSys'
      AND max_length < 20
)
BEGIN
    ALTER TABLE dbo.RT_ARUR_HEADER
    ALTER COLUMN CreateSys nvarchar(10) NULL;
END
GO
