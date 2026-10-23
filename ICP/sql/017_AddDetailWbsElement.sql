-- Run on ICP before deploying BUG-202609-012.
IF COL_LENGTH(N'dbo.ICP_DETAIL', N'WBS_ELEMENT') IS NULL
    ALTER TABLE dbo.ICP_DETAIL ADD WBS_ELEMENT nvarchar(30) NULL;
-- Preserve the legacy header value for existing details, without overwriting detail values.
-- Compile the UPDATE only after ALTER TABLE has created the column.
-- Dynamic SQL also supports deployment runners that do not process GO separators.
EXEC sys.sp_executesql N'
UPDATE d SET d.WBS_ELEMENT = h.WBS_ELEMENT
FROM dbo.ICP_DETAIL d
INNER JOIN dbo.ICP_HEADER h ON h.INVOICE_NO = d.INVOICE_NO AND h.TET_PO = d.TET_PO
WHERE d.WBS_ELEMENT IS NULL AND h.WBS_ELEMENT IS NOT NULL;';
