-- TET_PO is optional on shipment detail rows. Preserve existing data and length.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ICP_DETAIL')
      AND name = N'TET_PO' AND is_nullable = 0
)
    ALTER TABLE dbo.ICP_DETAIL ALTER COLUMN TET_PO NVARCHAR(35) NULL;
