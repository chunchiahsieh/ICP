-- Match the existing application limit; preserve values and nullability.
IF COL_LENGTH(N'dbo.ICP_HEADER', N'SHIPPER') > 0
   AND COL_LENGTH(N'dbo.ICP_HEADER', N'SHIPPER') < 100
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ICP_HEADER') AND name = N'SHIPPER' AND is_nullable = 0)
        ALTER TABLE dbo.ICP_HEADER ALTER COLUMN SHIPPER NVARCHAR(50) NOT NULL;
    ELSE
        ALTER TABLE dbo.ICP_HEADER ALTER COLUMN SHIPPER NVARCHAR(50) NULL;
END;
