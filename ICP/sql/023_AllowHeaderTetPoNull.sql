-- TET_PO is optional in both shipment headers and details.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ICP_HEADER')
      AND name = N'TET_PO' AND is_nullable = 0
)
    ALTER TABLE dbo.ICP_HEADER ALTER COLUMN TET_PO NVARCHAR(35) NULL;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ICP_DETAIL')
      AND name = N'TET_PO' AND is_nullable = 0
)
    ALTER TABLE dbo.ICP_DETAIL ALTER COLUMN TET_PO NVARCHAR(35) NULL;
