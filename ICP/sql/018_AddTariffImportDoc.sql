-- Run against the ICP database before deploying REQ-202609-19.
-- Adds the relative attachment path for Import Doc; existing rows remain NULL.
IF COL_LENGTH(N'dbo.TariffData', N'ImportDoc') IS NULL
BEGIN
    ALTER TABLE dbo.TariffData ADD ImportDoc nvarchar(500) NULL;
END;
