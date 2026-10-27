-- Expand ECCN to 20 characters without changing existing values or nullability.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ICP_DETAIL')
      AND name = N'ECCN' AND max_length > 0 AND max_length < 40
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.ICP_DETAIL')
          AND name = N'ECCN' AND is_nullable = 0
    )
        ALTER TABLE dbo.ICP_DETAIL ALTER COLUMN ECCN NVARCHAR(20) NOT NULL;
    ELSE
        ALTER TABLE dbo.ICP_DETAIL ALTER COLUMN ECCN NVARCHAR(20) NULL;
END;
